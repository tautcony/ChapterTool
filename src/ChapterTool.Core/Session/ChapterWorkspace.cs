using System.Collections.Immutable;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>
/// Explicit chapter workspace/session owner for the Avalonia shell.
/// Owns source metadata, typed clip session, edit buffer, projection state,
/// export preferences, and async revision / session-token commit rules.
/// </summary>
public sealed class ChapterWorkspace
{
    private readonly object commitSync = new();
    private int currentRevision;

    /// <summary>Gets typed source identity (null when no session).</summary>
    public ChapterSourceDocument? CurrentSource { get; private set; }

    /// <summary>Gets loaded source path (empty when the source has no local path).</summary>
    public string CurrentPath { get; private set; } = string.Empty;

    /// <summary>Gets display-friendly path (typically file name).</summary>
    public string DisplayPath { get; private set; } = string.Empty;

    /// <summary>Gets typed multi-clip session, or null when no source is loaded.</summary>
    public ClipSession? ClipSession { get; private set; }

    /// <summary>Gets working edit buffer for the active chapter set.</summary>
    public ChapterSet? CurrentChapterSet { get; private set; }

    /// <summary>Gets the undoable content session for the selected chapter set.</summary>
    public SessionState? ContentSession { get; private set; }

    /// <summary>Gets the track index projected into the active legacy ChapterSet view.</summary>
    public int CurrentTrackIndex => ClipSession?.IsCombined == true ? 0 : ClipSession?.SelectedIndex ?? 0;

    /// <summary>Gets monotonic operation revision used for anti-stale load/append commits.</summary>
    public int CurrentRevision => Volatile.Read(ref currentRevision);

    /// <summary>Gets projection state (naming, order, expression, last-good cache).</summary>
    public ProjectionState Projection { get; } = new();

    /// <summary>Gets export preference snapshot (format, language, encoding, BOM, save dir).</summary>
    public ExportPreferences ExportPreferences { get; } = new();

    /// <summary>Gets or sets last successful expression projection retained for mid-edit invalid expressions.</summary>
    public ChapterOutputProjectionResult? LastSuccessfulExpressionProjection
    {
        get => Projection.LastSuccessfulExpressionProjection;
        set => Projection.LastSuccessfulExpressionProjection = value;
    }

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
    {
        lock (commitSync)
        {
            if (!IsCurrentRevision(operationRevision))
            {
                return false;
            }

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
        var preview = ChapterContentOperationSession.Prepare(ContentSession,
            wasCombined ? "Split by boundaries" : "Merge clips",
            document => wasCombined
                ? document.Tracks.Length == 1
                    ? ChapterClipCandidateBuilder.SplitByBoundaries(document, document.Tracks[0].Id)
                    : new ChapterCandidateBuildResult(false, document, [], ["The combined document must have one track."])
                : ChapterClipCandidateBuilder.MergeTracks(document, document.Tracks.Select(static track => track.Id)));
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
        CancellationToken cancellationToken = default)
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
                "Append clips").AsTask().GetAwaiter().GetResult();
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
        ClipSession = session;
        ContentSession = new SessionState(document);
        SyncCurrentChapterSet();
    }

    /// <summary>Clears the loaded session and edit buffer.</summary>
    public void ClearSession()
    {
        CurrentSource = null;
        CurrentPath = string.Empty;
        DisplayPath = string.Empty;
        ClipSession = null;
        CurrentChapterSet = null;
        ContentSession = null;
    }

    /// <summary>Updates the working edit buffer without changing clip ownership.</summary>
    public void SetCurrentChapterSet(ChapterSet? chapterSet)
    {
        if (chapterSet is not null && ContentSession is not null)
        {
            WriteBackCurrentChapterSet(chapterSet);
            return;
        }

        if (chapterSet is null)
        {
            CurrentChapterSet = null;
            ContentSession = null;
            return;
        }

        CurrentChapterSet = chapterSet;
        ContentSession = new SessionState(EditableChapterDocumentAdapter.FromChapterSet(chapterSet));
    }

    /// <summary>
    /// Writes the edit buffer back into the typed clip session by mode
    /// and refreshes the buffer from the written entry.
    /// </summary>
    public void WriteBackCurrentChapterSet(ChapterSet info)
    {
        CommitLegacyResultToContentSession(info);
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

    /// <summary>Clears expression projection cache (e.g. when chapter set becomes null).</summary>
    public void ClearProjectionCache() => Projection.ClearProjectionCache();

    /// <summary>
    /// Atomically applies expression session fields on the workspace projection state.
    /// Callers should refresh rows once after this method returns.
    /// </summary>
    public void ApplyExpressionFields(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName) =>
        Projection.ApplyExpressionFields(expression, applyExpression, expressionPresetId, expressionSourceName);

    /// <summary>
    /// Builds export options for save/preview from workspace projection + export preferences.
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
            Expression: Projection.Expression,
            ExpressionPresetId: Projection.ExpressionPresetId,
            ExpressionSourceName: Projection.ExpressionSourceName,
            TextEncoding: ExportPreferences.TextEncoding,
            EmitBom: ExportPreferences.EmitBom,
            ProjectOutput: false);

    /// <summary>
    /// Export options for already-projected chapter sets (expression/naming/order already applied).
    /// </summary>
    public ChapterExportOptions CreateExportOptionsForProjectedInfo() =>
        CreateExportOptions() with
        {
            ApplyExpression = false,
            AutoGenerateNames = false,
            UseTemplateNames = false,
            ChapterNameTemplateText = string.Empty,
            OrderShift = 0,
            ProjectOutput = false
        };

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

    private void CommitLegacyResultToContentSession(ChapterSet info)
    {
        if (ContentSession is null)
        {
            ContentSession = new SessionState(EditableChapterDocumentAdapter.FromChapterSet(info));
            SyncCurrentChapterSet();
            return;
        }

        var snapshot = ContentSession.Snapshot;
        var trackIndex = ClipSession?.IsCombined == true ? 0 : ClipSession?.SelectedIndex ?? 0;
        var candidate = EditableChapterDocumentAdapter.ApplyTrackChapterSetResult(snapshot.Document, trackIndex, info);
        var outcome = ContentSession.ExecuteAsync(
            snapshot.BaseToken,
            Guid.NewGuid(),
            $"legacy-writeback:{snapshot.MutationRevision}:{info.Chapters.Count}",
            (_, _) => ValueTask.FromResult(candidate),
            operationDescription: "Update chapter content")
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            throw new InvalidOperationException(string.Join("; ", outcome.Errors));
        }

        PublishContentDocument(outcome.Snapshot.Document);
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
