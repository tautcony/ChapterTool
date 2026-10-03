using System.Collections.Immutable;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>Identifies one immutable document state that a transaction was based on.</summary>
public readonly record struct SessionBaseToken(
    ChapterDocumentId DocumentId,
    Guid StateIdentity,
    long MutationRevision);

/// <summary>A consistent view of the document and its mutation revision.</summary>
public sealed record SessionSnapshot(
    EditableChapterDocument Document,
    Guid StateIdentity,
    long MutationRevision)
{
    /// <summary>Gets a token that can be supplied to a transaction.</summary>
    public SessionBaseToken BaseToken => new(Document.Id, StateIdentity, MutationRevision);
}

/// <summary>Describes the result of a document mutation transaction.</summary>
public enum TransactionOutcomeKind
{
    /// <summary>The candidate was published.</summary>
    Committed,
    /// <summary>The candidate matched the current canonical document.</summary>
    NoChange,
    /// <summary>The base token no longer identifies the current document state.</summary>
    Conflict,
    /// <summary>The request was cancelled before publication.</summary>
    Cancelled,
    /// <summary>The candidate failed validation or computation.</summary>
    Invalid,
    /// <summary>Resources were exhausted before publication.</summary>
    ResourceFailure,
    /// <summary>The transaction ID is already bound to a different request.</summary>
    TransactionIdConflict
}

/// <summary>The stable result returned for a transaction and its retries.</summary>
public sealed record TransactionOutcome(
    Guid TransactionId,
    TransactionOutcomeKind Kind,
    SessionSnapshot Snapshot,
    ImmutableArray<string> Errors)
{
    /// <summary>Gets a value indicating whether the transaction published a new document.</summary>
    public bool WasApplied => Kind == TransactionOutcomeKind.Committed;
}

/// <summary>
/// Owns one immutable document and serializes candidate publication.
/// </summary>
public sealed class SessionState
{
    private readonly SemaphoreSlim commandQueue = new(1, 1);
    private readonly Action<SessionHistoryFailurePoint>? historyFailureInjector;
    private SessionVersion current;

    /// <summary>Creates a session for a validated document.</summary>
    public SessionState(EditableChapterDocument document)
        : this(document, null)
    {
    }

    internal SessionState(EditableChapterDocument document, Action<SessionHistoryFailurePoint>? historyFailureInjector)
    {
        ArgumentNullException.ThrowIfNull(document);
        var validation = EditableChapterDocumentValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new ArgumentException("The initial document is invalid.", nameof(document));
        }

        this.historyFailureInjector = historyFailureInjector;
        var history = SessionHistoryTree.Create(document);
        current = new SessionVersion(
            document,
            Guid.NewGuid(),
            0,
            ImmutableDictionary<Guid, CachedTransaction>.Empty,
            history);
    }

    /// <summary>Gets a consistent snapshot of the current document.</summary>
    public SessionSnapshot Snapshot
    {
        get
        {
            var version = Volatile.Read(ref current);
            return new SessionSnapshot(version.Document, version.StateIdentity, version.MutationRevision);
        }
    }

    /// <summary>Gets the root, cursor, and all retained history nodes.</summary>
    public SessionHistorySnapshot GetHistorySnapshot() => Volatile.Read(ref current).History.Snapshot();

    /// <summary>
    /// Computes and atomically publishes a candidate. The request fingerprint must identify
    /// all transaction inputs that affect the candidate.
    /// </summary>
    public async ValueTask<TransactionOutcome> ExecuteAsync(
        SessionBaseToken baseToken,
        Guid transactionId,
        string requestFingerprint,
        Func<EditableChapterDocument, CancellationToken, ValueTask<EditableChapterDocument>> createCandidate,
        CancellationToken cancellationToken = default,
        string? operationDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentNullException.ThrowIfNull(createCandidate);

        var queueEntered = false;
        try
        {
            await commandQueue.WaitAsync(cancellationToken).ConfigureAwait(false);
            queueEntered = true;

            var version = Volatile.Read(ref current);
            if (version.Transactions.TryGetValue(transactionId, out var cached))
            {
                return string.Equals(cached.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                    ? cached.Outcome
                    : new TransactionOutcome(
                        transactionId,
                        TransactionOutcomeKind.TransactionIdConflict,
                        new SessionSnapshot(version.Document, version.StateIdentity, version.MutationRevision),
                        ["The transaction ID is already bound to a different request."]);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Cancelled, []);
            }

            if (!IsCurrent(version, baseToken))
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Conflict, ["The transaction base token is stale."]);
            }

            EditableChapterDocument candidate;
            try
            {
                candidate = await createCandidate(version.Document, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Cancelled, []);
            }
            catch (OutOfMemoryException)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.ResourceFailure, ["Resources were exhausted before the transaction could be published."]);
            }
            catch (Exception exception) when (exception is not StackOverflowException)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Invalid, [exception.Message]);
            }

            if (candidate is null)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Invalid, ["The candidate document must not be null."]);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Cancelled, []);
            }

            if (!candidate.Id.Equals(version.Document.Id))
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Invalid, ["The candidate must preserve the document identity."]);
            }

            var validation = EditableChapterDocumentValidator.Validate(candidate);
            if (!validation.IsValid)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Invalid, validation.Errors);
            }

            if (CanonicalEquals(version.Document, candidate))
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.NoChange, []);
            }

            if (version.MutationRevision == long.MaxValue)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.ResourceFailure, ["The session mutation revision is exhausted."]);
            }

            try
            {
                historyFailureInjector?.Invoke(SessionHistoryFailurePoint.ChangeSetConstruction);
                var changeSet = DomainChangeSet.Create(version.Document, candidate);
                historyFailureInjector?.Invoke(SessionHistoryFailurePoint.NodePublication);
                var nextIdentity = Guid.NewGuid();
                var nextRevision = version.MutationRevision + 1;
                var history = version.History.Append(nextIdentity, changeSet, NormalizeDescription(operationDescription));
                var nextSnapshot = new SessionSnapshot(candidate, nextIdentity, nextRevision);
                var outcome = new TransactionOutcome(transactionId, TransactionOutcomeKind.Committed, nextSnapshot, []);
                var transactions = version.Transactions.Add(transactionId, new CachedTransaction(requestFingerprint, outcome));
                var nextVersion = new SessionVersion(candidate, nextIdentity, nextRevision, transactions, history);

                if (cancellationToken.IsCancellationRequested)
                {
                    return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Cancelled, []);
                }

                // This reference write is the transaction's publication point.
                Volatile.Write(ref current, nextVersion);
                return outcome;
            }
            catch (OutOfMemoryException)
            {
                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.ResourceFailure, ["Resources were exhausted before the transaction could be published."]);
            }
        }
        catch (OperationCanceledException) when (!queueEntered && cancellationToken.IsCancellationRequested)
        {
            // Wait for an atomic queue boundary so the cancellation result is also bound
            // to this transaction ID and cannot race a request with the same ID.
            await commandQueue.WaitAsync().ConfigureAwait(false);
            try
            {
                var version = Volatile.Read(ref current);
                if (version.Transactions.TryGetValue(transactionId, out var cached))
                {
                    return string.Equals(cached.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                        ? cached.Outcome
                        : new TransactionOutcome(
                            transactionId,
                            TransactionOutcomeKind.TransactionIdConflict,
                            new SessionSnapshot(version.Document, version.StateIdentity, version.MutationRevision),
                            ["The transaction ID is already bound to a different request."]);
                }

                return CreateAndCache(version, transactionId, requestFingerprint, TransactionOutcomeKind.Cancelled, []);
            }
            finally
            {
                commandQueue.Release();
            }
        }
        finally
        {
            if (queueEntered)
            {
                commandQueue.Release();
            }
        }
    }

    /// <summary>Moves to the parent state. Returns <see cref="HistoryNavigationOutcomeKind.NoChange"/> at the root.</summary>
    public ValueTask<HistoryNavigationOutcome> UndoAsync(CancellationToken cancellationToken = default)
    {
        return NavigateAsync(tree => tree.Nodes[tree.CursorId].ParentId, cancellationToken);
    }

    /// <summary>Moves to the preferred child state. Returns no change at a leaf.</summary>
    public ValueTask<HistoryNavigationOutcome> RedoAsync(CancellationToken cancellationToken = default)
    {
        return NavigateAsync(tree =>
        {
            var node = tree.Nodes[tree.CursorId];
            return node.PreferredChildId ?? (node.Children.IsEmpty ? null : node.Children[0]);
        }, cancellationToken);
    }

    /// <summary>Moves to a retained direct child of the current node.</summary>
    public ValueTask<HistoryNavigationOutcome> SelectRedoBranchAsync(Guid childId, CancellationToken cancellationToken = default)
    {
        return NavigateAsync(tree => tree.Nodes[tree.CursorId].Children.Contains(childId) ? childId : null, cancellationToken, childId, requireDirectChild: true);
    }

    /// <summary>Moves to any retained history node, reconstructing privately before publication.</summary>
    public ValueTask<HistoryNavigationOutcome> NavigateToAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        NavigateAsync(_ => nodeId, cancellationToken, nodeId);

    private async ValueTask<HistoryNavigationOutcome> NavigateAsync(
        Func<SessionHistoryTree, Guid?> selectTarget,
        CancellationToken cancellationToken,
        Guid? requestedId = null,
        bool requireDirectChild = false)
    {
        await commandQueue.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = Volatile.Read(ref current);
            var tree = version.History;
            var targetId = selectTarget(tree);
            if (targetId is null)
            {
                var kind = requestedId is null ? HistoryNavigationOutcomeKind.NoChange : HistoryNavigationOutcomeKind.NotFound;
                return HistoryOutcome(kind, version, kind == HistoryNavigationOutcomeKind.NoChange ? [] : ["The requested history node is not a child of the current node."]);
            }

            if (!tree.Nodes.ContainsKey(targetId.Value))
            {
                return HistoryOutcome(HistoryNavigationOutcomeKind.NotFound, version, ["The requested history node does not exist."]);
            }

            if (targetId == tree.CursorId)
            {
                return HistoryOutcome(HistoryNavigationOutcomeKind.NoChange, version, []);
            }

            if ((requestedId is Guid requested && requested != targetId)
                || (requireDirectChild && !tree.Nodes[tree.CursorId].Children.Contains(targetId.Value)))
            {
                return HistoryOutcome(HistoryNavigationOutcomeKind.NotFound, version, ["The requested history node is not a child of the current node."]);
            }

            if (version.MutationRevision == long.MaxValue)
            {
                return HistoryOutcome(HistoryNavigationOutcomeKind.ResourceFailure, version, ["The session mutation revision is exhausted."]);
            }

            try
            {
                historyFailureInjector?.Invoke(SessionHistoryFailurePoint.Reconstruction);
                var document = Reconstruct(tree, version.Document, tree.CursorId, targetId.Value);
                var nextIdentity = Guid.NewGuid();
                var nextRevision = version.MutationRevision + 1;
                var nextTree = tree.MoveTo(targetId.Value);
                var nextVersion = version with
                {
                    Document = document,
                    StateIdentity = nextIdentity,
                    MutationRevision = nextRevision,
                    History = nextTree
                };
                var outcome = new HistoryNavigationOutcome(
                    HistoryNavigationOutcomeKind.Committed,
                    new SessionSnapshot(document, nextIdentity, nextRevision),
                    targetId.Value,
                    []);
                Volatile.Write(ref current, nextVersion);
                return outcome;
            }
            catch (OutOfMemoryException)
            {
                return HistoryOutcome(HistoryNavigationOutcomeKind.ResourceFailure, version, ["Resources were exhausted before history navigation could be published."]);
            }
        }
        finally
        {
            commandQueue.Release();
        }
    }

    private static EditableChapterDocument Reconstruct(SessionHistoryTree tree, EditableChapterDocument sourceDocument, Guid fromId, Guid targetId)
    {
        if (tree.Nodes[fromId].ParentId == targetId)
        {
            return tree.Nodes[fromId].ChangeSet!.Apply(sourceDocument, forward: false);
        }

        if (tree.Nodes[targetId].ParentId == fromId)
        {
            return tree.Nodes[targetId].ChangeSet!.Apply(sourceDocument, forward: true);
        }

        var targetAncestors = new HashSet<Guid>();
        var currentId = targetId;
        while (true)
        {
            targetAncestors.Add(currentId);
            if (currentId == tree.RootId)
            {
                break;
            }

            currentId = tree.Nodes[currentId].ParentId!.Value;
        }

        var sourcePath = new List<Guid>();
        currentId = fromId;
        while (!targetAncestors.Contains(currentId))
        {
            sourcePath.Add(currentId);
            currentId = tree.Nodes[currentId].ParentId!.Value;
        }

        var commonAncestor = currentId;
        var document = sourceDocument;
        foreach (var id in sourcePath)
        {
            document = tree.Nodes[id].ChangeSet!.Apply(document, false);
        }

        var targetPath = new List<Guid>();
        currentId = targetId;
        while (currentId != commonAncestor)
        {
            targetPath.Add(currentId);
            currentId = tree.Nodes[currentId].ParentId!.Value;
        }

        targetPath.Reverse();
        foreach (var id in targetPath)
        {
            document = tree.Nodes[id].ChangeSet!.Apply(document, true);
        }

        return document;
    }

    private static HistoryNavigationOutcome HistoryOutcome(
        HistoryNavigationOutcomeKind kind,
        SessionVersion version,
        IEnumerable<string> errors) => new(
            kind,
            new SessionSnapshot(version.Document, version.StateIdentity, version.MutationRevision),
            version.History.CursorId,
            errors.ToImmutableArray());

    private static string NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? "Edit" : description.Trim();

    private TransactionOutcome CreateAndCache(
        SessionVersion version,
        Guid transactionId,
        string requestFingerprint,
        TransactionOutcomeKind kind,
        IEnumerable<string> errors)
    {
        var outcome = new TransactionOutcome(
            transactionId,
            kind,
            new SessionSnapshot(version.Document, version.StateIdentity, version.MutationRevision),
            errors.ToImmutableArray());
        var transactions = version.Transactions.Add(transactionId, new CachedTransaction(requestFingerprint, outcome));
        Volatile.Write(ref current, version with { Transactions = transactions });
        return outcome;
    }

    private static bool IsCurrent(SessionVersion version, SessionBaseToken token) =>
        version.Document.Id.Equals(token.DocumentId)
        && version.StateIdentity == token.StateIdentity
        && version.MutationRevision == token.MutationRevision;

    private static bool CanonicalEquals(EditableChapterDocument left, EditableChapterDocument right)
    {
        if (!left.Id.Equals(right.Id)
            || !string.Equals(left.Title, right.Title, StringComparison.Ordinal)
            || !string.Equals(left.SourceName, right.SourceName, StringComparison.Ordinal)
            || left.ImportFormat != right.ImportFormat
            || left.Duration != right.Duration
            || left.FrameRate != right.FrameRate
            || left.Tracks.Length != right.Tracks.Length)
        {
            return false;
        }

        for (var trackIndex = 0; trackIndex < left.Tracks.Length; trackIndex++)
        {
            var leftTrack = left.Tracks[trackIndex];
            var rightTrack = right.Tracks[trackIndex];
            if (!leftTrack.Id.Equals(rightTrack.Id)
                || !string.Equals(leftTrack.Name, rightTrack.Name, StringComparison.Ordinal)
                || !leftTrack.Segments.SequenceEqual(rightTrack.Segments)
                || leftTrack.Chapters.Length != rightTrack.Chapters.Length)
            {
                return false;
            }

            for (var chapterIndex = 0; chapterIndex < leftTrack.Chapters.Length; chapterIndex++)
            {
                if (leftTrack.Chapters[chapterIndex] != rightTrack.Chapters[chapterIndex])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private sealed record CachedTransaction(string RequestFingerprint, TransactionOutcome Outcome);

    private sealed record SessionVersion(
        EditableChapterDocument Document,
        Guid StateIdentity,
        long MutationRevision,
        ImmutableDictionary<Guid, CachedTransaction> Transactions,
        SessionHistoryTree History);
}
