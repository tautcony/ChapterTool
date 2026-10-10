using System.Collections.Immutable;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>
/// Explicit chapter workspace/session owner for the Avalonia shell.
/// Owns source metadata, typed clip session, committed content session, operation drafts,
/// export preferences, and async revision / session-token commit rules.
/// </summary>
public sealed class ChapterWorkspace
{
    private readonly object commitSync = new();
    private readonly Dictionary<ChapterTrackId, ChapterExportBaseline> exportBaselines = [];
    private int currentRevision;
    private Guid sessionGeneration = Guid.NewGuid();

    /// <summary>Gets typed source identity (null when no session).</summary>
    public ChapterSourceDocument? CurrentSource { get; private set; }

    /// <summary>Gets loaded source path (empty when the source has no local path).</summary>
    public string CurrentPath { get; private set; } = string.Empty;

    /// <summary>Gets display-friendly path (typically file name).</summary>
    public string DisplayPath { get; private set; } = string.Empty;

    /// <summary>Gets typed multi-clip session, or null when no source is loaded.</summary>
    public ClipSession? ClipSession { get; private set; }

    /// <summary>Gets a transient compatibility view of the selected track.</summary>
    public ChapterSet? CurrentChapterSet { get; private set; }

    /// <summary>Gets the undoable content session for the selected chapter set.</summary>
    public SessionState? ContentSession { get; private set; }

    /// <summary>Gets the track index represented by the active ChapterSet compatibility view.</summary>
    public int CurrentTrackIndex => ClipSession?.IsCombined == true ? 0 : ClipSession?.SelectedIndex ?? 0;

    /// <summary>Gets monotonic operation revision used for anti-stale load/append commits.</summary>
    public int CurrentRevision => Volatile.Read(ref currentRevision);

    /// <summary>Gets whether the current content differs from its successful export baseline.</summary>
    public bool HasUnexportedChanges
    {
        get
        {
            var document = ContentSession?.Snapshot.Document;
            if (document is null)
            {
                return false;
            }

            return document.Tracks.Select((track, index) =>
            {
                var chapterSet = EditableChapterDocumentAdapter.ToChapterSet(document, index);
                var options = CreateExportOptions() with { SourceFileName = chapterSet.SourceName };
                return !exportBaselines.TryGetValue(track.Id, out var baseline)
                    || baseline.Digest != ChapterExportFingerprint.Digest(chapterSet, options)
                    || baseline.FormatFingerprint != ChapterExportFingerprint.FormatFingerprint(options);
            }).Any(static unexported => unexported);
        }
    }

    /// <summary>Captures one track and its format options without retaining the history owner.</summary>
    public ChapterExportSnapshot? CaptureExportSnapshot(ChapterSet chapterSet, ChapterExportOptions options, int trackIndex)
    {
        ArgumentNullException.ThrowIfNull(chapterSet);
        ArgumentNullException.ThrowIfNull(options);
        var contentSession = ContentSession;
        if (contentSession is null)
        {
            return null;
        }

        var snapshot = contentSession.Snapshot;
        if (trackIndex < 0 || trackIndex >= snapshot.Document.Tracks.Length)
        {
            return null;
        }

        var copy = chapterSet with { Chapters = Array.AsReadOnly(chapterSet.Chapters.ToArray()) };
        return new ChapterExportSnapshot(
            sessionGeneration,
            snapshot,
            snapshot.Document.Tracks[trackIndex].Id,
            copy,
            options,
            ChapterExportFingerprint.Digest(copy, options),
            ChapterExportFingerprint.FormatFingerprint(options));
    }

    /// <summary>Records a successful export only for the session and captured track that produced it.</summary>
    public bool RecordSuccessfulExport(ChapterExportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var session = ContentSession;
        if (session is null || sessionGeneration != snapshot.SessionGeneration
            || session.Snapshot.Document.Id != snapshot.Content.Document.Id
            || !session.Snapshot.Document.Tracks.Any(track => track.Id == snapshot.TrackId))
        {
            return false;
        }

        exportBaselines[snapshot.TrackId] = new ChapterExportBaseline(
            snapshot.Content.StateIdentity,
            snapshot.Digest,
            snapshot.FormatFingerprint);
        return true;
    }

    /// <summary>Gets the successful export baseline for a track, if one exists.</summary>
    public ChapterExportBaseline? GetExportBaseline(ChapterTrackId trackId) =>
        exportBaselines.TryGetValue(trackId, out var baseline) ? baseline : null;

    /// <summary>Checks whether a captured export matches the baseline for its track.</summary>
    public bool IsExported(ChapterExportSnapshot? snapshot) => snapshot is not null
        && sessionGeneration == snapshot.SessionGeneration
        && exportBaselines.TryGetValue(snapshot.TrackId, out var baseline)
        && baseline.Digest == snapshot.Digest
        && baseline.FormatFingerprint == snapshot.FormatFingerprint;

    /// <summary>Gets operation parameters kept as workspace-local drafts.</summary>
    public ContentOperationDraftState OperationDrafts { get; } = new();

    /// <summary>Gets export preference snapshot (format, language, encoding, BOM, save dir).</summary>
    public ExportPreferences ExportPreferences { get; } = new();

    /// <summary>Increments revision for a new load operation; returns the operation id to bind progress/result.</summary>
    public int BeginLoadOperation()
    {
        lock (commitSync)
        {
            return Interlocked.Increment(ref currentRevision);
        }
    }

    /// <summary>Reads the current revision without incrementing.</summary>
    public int CaptureRevision() => Volatile.Read(ref currentRevision);

    /// <summary>Whether an async operation's revision still matches the workspace.</summary>
    public bool IsCurrentRevision(int operationRevision) =>
        operationRevision == Volatile.Read(ref currentRevision);

    /// <summary>Captures the active content token before a replacement confirmation.</summary>
    public SessionBaseToken? CaptureContentToken() => ContentSession?.Snapshot.BaseToken;

    /// <summary>Checks that the active document did not change during replacement confirmation.</summary>
    public bool IsContentTokenCurrent(SessionBaseToken? token) =>
        token is null ? ContentSession is null : ContentSession?.Snapshot.BaseToken == token;

    /// <summary>
    /// Commits a successful load: replaces path, clip session, and edit buffer atomically
    /// only when <paramref name="operationRevision"/> is still current.
    /// </summary>
    public bool TryCommitLoad(int operationRevision, string path, ClipSession session)
        => TryCommitLoad(operationRevision, new LocalPathChapterSource(path), session);

    /// <summary>Commits a typed source load when the operation revision is current.</summary>
    public bool TryCommitLoad(int operationRevision, ChapterSourceDocument source, ClipSession session)
        => TryCommitLoad(operationRevision, source, session, EditableChapterDocumentAdapter.FromChapterImportSource(session.OriginalGroup));

    /// <summary>Commits imported clip metadata and the complete multi-track editable document atomically.</summary>
    public bool TryCommitLoad(
        int operationRevision,
        ChapterSourceDocument source,
        ClipSession session,
        EditableChapterDocument document)
        => TryCommitLoad(operationRevision, source, session, document, expectedContentToken: null, verifyExpectedContentToken: false);

    /// <summary>Commits only if the active content token still matches the confirmation snapshot.</summary>
    public bool TryCommitLoad(
        int operationRevision,
        ChapterSourceDocument source,
        ClipSession session,
        EditableChapterDocument document,
        SessionBaseToken? expectedContentToken)
        => TryCommitLoad(operationRevision, source, session, document, expectedContentToken, verifyExpectedContentToken: true);

    private bool TryCommitLoad(
        int operationRevision,
        ChapterSourceDocument source,
        ClipSession session,
        EditableChapterDocument document,
        SessionBaseToken? expectedContentToken,
        bool verifyExpectedContentToken)
    {
        lock (commitSync)
        {
            if (!IsCurrentRevision(operationRevision)
                || (verifyExpectedContentToken && !IsContentTokenCurrent(expectedContentToken)))
            {
                return false;
            }

            EndContentSession();
            CurrentSource = source;
            CurrentPath = source is LocalPathChapterSource local ? local.Path : string.Empty;
            DisplayPath = source.DisplayName;
            ReplaceSession(session, document);
            return true;
        }
    }

    /// <summary>
    /// Commits an append result only when revision and session identity still match.
    /// </summary>
    public bool TryCommitAppend(int operationRevision, Guid expectedSessionId, ClipSession session)
    {
        lock (commitSync)
        {
            if (!IsCurrentRevision(operationRevision)
                || ClipSession is null
                || ClipSession.SessionId != expectedSessionId)
            {
                return false;
            }

            ClipSession = session;
            SyncCurrentChapterSet();
            return true;
        }
    }

    /// <summary>Builds and atomically publishes a merge or split transaction.</summary>
    public ClipCombineTransitionResult ToggleClipStructure()
    {
        if (ClipSession is null || ContentSession is null)
        {
            return new ClipCombineTransitionResult(null,
                new ChapterEditResult(new ChapterSet(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []), []),
                false, false);
        }

        var basis = ClipSession;
        var wasCombined = basis.IsCombined;
        var operationKind = wasCombined ? "workspace.split" : "workspace.merge";
        var preview = ChapterContentOperationSession.Prepare(ContentSession,
            wasCombined ? "Split by boundaries" : "Merge clips",
            document => wasCombined
                ? document.Tracks.Length == 1
                    ? ChapterClipCandidateBuilder.SplitByBoundaries(document, document.Tracks[0].Id)
                    : new ChapterCandidateBuildResult(false, document, [], ["The combined document must have one track."])
                : ChapterClipCandidateBuilder.MergeTracks(document, document.Tracks.Select(static track => track.Id)),
            new HistoryOperationDescriptor(operationKind,
            [
                new HistoryOperationParameter("trackIds", string.Join(",", ContentSession.Snapshot.Document.Tracks.Select(static track => track.Id.ToString()))),
                new HistoryOperationParameter("trackCount", ContentSession.Snapshot.Document.Tracks.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), "integer")
            ]));
        if (!preview.IsValid)
        {
            return new ClipCombineTransitionResult(null,
                new ChapterEditResult(EditableChapterDocumentAdapter.ToChapterSet(ContentSession.Snapshot.Document),
                    [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidStructure, string.Join("; ", preview.Errors))]),
                wasCombined, false);
        }

        var outcome = ChapterContentOperationSession.ApplyAsync(ContentSession, preview).AsTask().GetAwaiter().GetResult();
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            return new ClipCombineTransitionResult(null,
                new ChapterEditResult(EditableChapterDocumentAdapter.ToChapterSet(outcome.Snapshot.Document),
                    [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidStructure, string.Join("; ", outcome.Errors))]),
                wasCombined, false);
        }

        var next = ClipSessionTransitions.FromDocument(basis, outcome.Snapshot.Document, combined: !wasCombined);
        ClipSession = next;
        SyncCurrentChapterSet();
        return new ClipCombineTransitionResult(next, new ChapterEditResult(
            EditableChapterDocumentAdapter.ToChapterSet(outcome.Snapshot.Document), []), wasCombined, true);
    }

    /// <summary>Appends a loaded source only if the captured session token and workspace revision remain current.</summary>
    public ClipAppendCommitResult AppendClipSource(
        int operationRevision,
        Guid expectedSessionId,
        SessionSnapshot capturedContent,
        ChapterImportSource appended,
        CancellationToken cancellationToken = default,
        HistoryOperationDescriptor? operationDescriptor = null)
    {
        ArgumentNullException.ThrowIfNull(capturedContent);
        ArgumentNullException.ThrowIfNull(appended);
        var imported = EditableChapterDocumentAdapter.FromChapterImportSource(appended);
        var candidate = ChapterClipCandidateBuilder.Append(capturedContent.Document, imported);
        if (!candidate.IsValid)
        {
            return ClipAppendCommitResult.Failed(candidate.Errors);
        }

        lock (commitSync)
        {
            if (!IsCurrentRevision(operationRevision)
                || ClipSession is null
                || ClipSession.SessionId != expectedSessionId
                || ContentSession is null
                || ContentSession.Snapshot.BaseToken != capturedContent.BaseToken)
            {
                return ClipAppendCommitResult.Stale;
            }

            var contentOutcome = ContentSession.ExecuteAsync(
                capturedContent.BaseToken,
                Guid.NewGuid(),
                $"append:{expectedSessionId:N}:{appended.SourcePath}",
                (_, _) => ValueTask.FromResult(candidate.Candidate),
                cancellationToken,
                "Append clips",
                operationDescriptor).AsTask().GetAwaiter().GetResult();
            if (contentOutcome.Kind == TransactionOutcomeKind.Conflict)
            {
                return ClipAppendCommitResult.Stale;
            }

            if (contentOutcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
            {
                return ClipAppendCommitResult.Failed(contentOutcome.Errors);
            }

            var next = ClipSessionTransitions.FromDocument(ClipSession, contentOutcome.Snapshot.Document, combined: true) as CombinedClipSession;
            if (next is null)
            {
                return ClipAppendCommitResult.Failed(["The appended document did not produce a combined clip session."]);
            }
            ClipSession = next;
            SyncCurrentChapterSet();
            return ClipAppendCommitResult.Success(next, contentOutcome.Snapshot.Document);
        }
    }

    /// <summary>
    /// Replaces clip session and syncs the edit buffer from the selected entry.
    /// Does not advance revision (structural session change within the same load).
    /// </summary>
    public void ReplaceSession(ClipSession session)
    {
        ClipSession = session;
        SyncCurrentChapterSet();
    }

    /// <summary>Replaces the active source group and its canonical content document.</summary>
    public void ReplaceSession(ClipSession session, EditableChapterDocument document)
    {
        EndContentSession();
        sessionGeneration = Guid.NewGuid();
        exportBaselines.Clear();
        ClipSession = session;
        ContentSession = new SessionState(document);
        SyncCurrentChapterSet();
    }

    /// <summary>Clears the loaded session and edit buffer.</summary>
    public void ClearSession()
    {
        Interlocked.Increment(ref currentRevision);
        EndContentSession();
        sessionGeneration = Guid.NewGuid();
        exportBaselines.Clear();
        CurrentSource = null;
        CurrentPath = string.Empty;
        DisplayPath = string.Empty;
        ClipSession = null;
        CurrentChapterSet = null;
        ContentSession = null;
    }

    private void EndContentSession()
    {
        ContentSession?.EndSession();
        ContentSession = null;
    }

    /// <summary>Converts one legacy service result to an immutable candidate and commits it atomically.</summary>
    public TransactionOutcome CommitNonStructuralChapterSetResult(
        ChapterSet result,
        string operation = "Update chapter content",
        HistoryOperationDescriptor? operationDescriptor = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var content = ContentSession;
        if (content is null)
        {
            throw new InvalidOperationException("No chapter content session is active.");
        }

        var snapshot = content.Snapshot;
        var trackIndex = CurrentTrackIndex;
        var candidate = EditableChapterDocumentAdapter.ApplyNonStructuralChapterSetResult(
            FocusTrack(snapshot.Document, trackIndex), result);
        candidate = ReplaceFocusedTrack(snapshot.Document, trackIndex, candidate);
        var validation = EditableChapterDocumentValidator.Validate(candidate);
        if (!validation.IsValid)
        {
            return new TransactionOutcome(Guid.NewGuid(), TransactionOutcomeKind.Invalid, snapshot, validation.Errors);
        }

        var outcome = content.ExecuteAsync(
            snapshot.BaseToken,
            Guid.NewGuid(),
            $"{operation}:{snapshot.MutationRevision}",
            (_, _) => ValueTask.FromResult(candidate),
            operationDescription: operation,
            operation: operationDescriptor).AsTask().GetAwaiter().GetResult();
        if (outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
        {
            PublishContentDocument(outcome.Snapshot.Document);
        }

        return outcome;
    }

    /// <summary>Builds and commits one candidate against the selected track in the current document.</summary>
    public TransactionOutcome ExecuteTrackCandidate(
        string operation,
        Func<EditableChapterDocument, ChapterCandidateBuildResult> buildCandidate,
        CancellationToken cancellationToken = default,
        HistoryOperationDescriptor? operationDescriptor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(buildCandidate);
        var content = ContentSession ?? throw new InvalidOperationException("No chapter content session is active.");
        var snapshot = content.Snapshot;
        var trackIndex = CurrentTrackIndex;
        var focused = FocusTrack(snapshot.Document, trackIndex);
        ChapterCandidateBuildResult built;
        try
        {
            built = buildCandidate(focused);
        }
        catch (Exception exception) when (exception is not StackOverflowException)
        {
            built = new ChapterCandidateBuildResult(false, focused, [], [exception.Message]);
        }

        if (!built.IsValid || built.Candidate.Tracks.Length != 1)
        {
            return new TransactionOutcome(Guid.NewGuid(), TransactionOutcomeKind.Invalid, snapshot, built.Errors);
        }

        var candidate = ReplaceFocusedTrack(snapshot.Document, trackIndex, built.Candidate);
        var validation = EditableChapterDocumentValidator.Validate(candidate);
        if (!validation.IsValid)
        {
            return new TransactionOutcome(Guid.NewGuid(), TransactionOutcomeKind.Invalid, snapshot, validation.Errors);
        }

        var preview = new ChapterContentPreview(Guid.NewGuid(), snapshot.BaseToken, operation,
            snapshot.Document, candidate, built.TargetIds, [], [], operationDescriptor);
        var outcome = ChapterContentOperationSession.ApplyAsync(content, preview, cancellationToken)
            .AsTask().GetAwaiter().GetResult();
        if (outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
        {
            PublishContentDocument(outcome.Snapshot.Document);
        }

        return outcome;
    }

    /// <summary>Selects a clip index; preserves session identity for append anti-stale checks.</summary>
    public void SelectClip(int index)
    {
        if (ClipSession is null)
        {
            return;
        }

        ClipSession = ClipSessionTransitions.Select(ClipSession, index);
        SyncCurrentChapterSet();
    }

    /// <summary>
    /// Atomically applies expression values to the operation drafts.
    /// </summary>
    public void SetExpressionOperationDrafts(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName) =>
        OperationDrafts.ApplyExpressionFields(expression, applyExpression, expressionPresetId, expressionSourceName);

    /// <summary>
    /// Builds export options for committed content and current export preferences.
    /// </summary>
    public ChapterExportOptions CreateExportOptions() =>
        new(
            Format: ExportPreferences.Format,
            XmlLanguage: ExportPreferences.XmlLanguage,
            SourceFileName: CurrentChapterSet?.SourceName,
            AutoGenerateNames: false,
            UseTemplateNames: false,
            ChapterNameTemplateText: string.Empty,
            OrderShift: 0,
            ApplyExpression: false,
            Expression: OperationDrafts.Expression,
            ExpressionPresetId: OperationDrafts.ExpressionPresetId,
            ExpressionSourceName: OperationDrafts.ExpressionSourceName,
            TextEncoding: ExportPreferences.TextEncoding,
            EmitBom: ExportPreferences.EmitBom,
            ProjectOutput: false);

    /// <summary>Publishes a document already committed to <see cref="ContentSession"/> into the active clip.</summary>
    public void PublishContentDocument(EditableChapterDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (ContentSession is null || ContentSession.Snapshot.Document.Id != document.Id)
        {
            throw new InvalidOperationException("The content document does not belong to the active workspace session.");
        }

        var combined = document.Tracks.Length == 1 && document.Tracks[0].Segments.Length > 1;
        ClipSession = ClipSessionTransitions.FromDocument(ClipSession!, document, combined, preserveSessionId: true);
        SyncCurrentChapterSet();
    }

    private void SyncCurrentChapterSet()
    {
        if (ContentSession is null || ClipSession is null)
        {
            CurrentChapterSet = null;
            return;
        }

        var document = ContentSession.Snapshot.Document;
        var index = ClipSession.IsCombined ? 0 : ClipSession.SelectedIndex;
        CurrentChapterSet = index >= 0 && index < document.Tracks.Length
            ? EditableChapterDocumentAdapter.ToChapterSet(document, index)
            : null;
    }

    private static EditableChapterDocument FocusTrack(EditableChapterDocument source, int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= source.Tracks.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        }

        var track = source.Tracks[trackIndex];
        var segment = track.Segments.FirstOrDefault();
        return new EditableChapterDocument(source.Id, segment?.Name ?? track.Name,
            segment?.SourceName ?? source.SourceName, segment?.ImportFormat ?? source.ImportFormat,
            EditableChapterDocumentAdapter.TrackDuration(source, trackIndex), segment?.FrameRate ?? source.FrameRate, [track]);
    }

    private static EditableChapterDocument ReplaceFocusedTrack(
        EditableChapterDocument source,
        int trackIndex,
        EditableChapterDocument focused)
    {
        var tracks = source.Tracks.SetItem(trackIndex, focused.Tracks[0]);
        return new EditableChapterDocument(source.Id, source.Title, source.SourceName, source.ImportFormat,
            source.Duration, source.FrameRate, tracks);
    }
}

/// <summary>Result of an append against the captured workspace document.</summary>
public sealed record ClipAppendCommitResult(bool Succeeded, bool IsStale, CombinedClipSession? Session, EditableChapterDocument? Document, System.Collections.Immutable.ImmutableArray<string> Errors)
{
    /// <summary>Gets the stale result.</summary>
    public static ClipAppendCommitResult Stale { get; } = new(false, true, null, null, []);

    /// <summary>Creates a successful append result.</summary>
    public static ClipAppendCommitResult Success(CombinedClipSession session, EditableChapterDocument document) =>
        new(true, false, session, document, []);

    /// <summary>Creates a failed append result.</summary>
    public static ClipAppendCommitResult Failed(IEnumerable<string> errors) =>
        new(false, false, null, null, errors.ToImmutableArray());
}
