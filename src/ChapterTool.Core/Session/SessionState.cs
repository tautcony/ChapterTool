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
    private SessionVersion current;

    /// <summary>Creates a session for a validated document.</summary>
    public SessionState(EditableChapterDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var validation = EditableChapterDocumentValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new ArgumentException("The initial document is invalid.", nameof(document));
        }

        current = new SessionVersion(
            document,
            Guid.NewGuid(),
            0,
            ImmutableDictionary<Guid, CachedTransaction>.Empty);
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

    /// <summary>
    /// Computes and atomically publishes a candidate. The request fingerprint must identify
    /// all transaction inputs that affect the candidate.
    /// </summary>
    public async ValueTask<TransactionOutcome> ExecuteAsync(
        SessionBaseToken baseToken,
        Guid transactionId,
        string requestFingerprint,
        Func<EditableChapterDocument, CancellationToken, ValueTask<EditableChapterDocument>> createCandidate,
        CancellationToken cancellationToken = default)
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
                var nextIdentity = Guid.NewGuid();
                var nextRevision = version.MutationRevision + 1;
                var nextSnapshot = new SessionSnapshot(candidate, nextIdentity, nextRevision);
                var outcome = new TransactionOutcome(transactionId, TransactionOutcomeKind.Committed, nextSnapshot, []);
                var transactions = version.Transactions.Add(transactionId, new CachedTransaction(requestFingerprint, outcome));
                var nextVersion = new SessionVersion(candidate, nextIdentity, nextRevision, transactions);

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
        ImmutableDictionary<Guid, CachedTransaction> Transactions);
}
