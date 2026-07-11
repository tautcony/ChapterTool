using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Session;

/// <summary>
/// Explicit chapter workspace/session owner for the Avalonia shell.
/// Owns source metadata, typed clip session, edit buffer, projection cache,
/// and async revision / session-token commit rules.
/// </summary>
public sealed class ChapterWorkspace
{
    private int currentRevision;

    /// <summary>Loaded source path (empty when no session).</summary>
    public string CurrentPath { get; private set; } = string.Empty;

    /// <summary>Display-friendly path (typically file name).</summary>
    public string DisplayPath { get; private set; } = string.Empty;

    /// <summary>Typed multi-clip session, or null when no source is loaded.</summary>
    public ClipSession? ClipSession { get; private set; }

    /// <summary>Working edit buffer for the active chapter set.</summary>
    public ChapterSet? CurrentChapterSet { get; private set; }

    /// <summary>Monotonic operation revision used for anti-stale load/append commits.</summary>
    public int CurrentRevision => Volatile.Read(ref currentRevision);

    /// <summary>Last successful expression projection retained for mid-edit invalid expressions.</summary>
    public ChapterOutputProjectionResult? LastSuccessfulExpressionProjection { get; set; }

    /// <summary>Increments revision for a new load operation; returns the operation id to bind progress/result.</summary>
    public int BeginLoadOperation() => Interlocked.Increment(ref currentRevision);

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
    {
        if (!IsCurrentRevision(operationRevision))
        {
            return false;
        }

        CurrentPath = path;
        DisplayPath = Path.GetFileName(path);
        ReplaceSession(session);
        return true;
    }

    /// <summary>
    /// Commits an append result only when revision and session identity still match.
    /// </summary>
    public bool TryCommitAppend(int operationRevision, Guid expectedSessionId, ClipSession session)
    {
        if (!IsCurrentRevision(operationRevision)
            || ClipSession is null
            || ClipSession.SessionId != expectedSessionId)
        {
            return false;
        }

        ReplaceSession(session);
        return true;
    }

    /// <summary>
    /// Replaces clip session and syncs the edit buffer from the selected entry.
    /// Does not advance revision (structural session change within the same load).
    /// </summary>
    public void ReplaceSession(ClipSession session)
    {
        ClipSession = session;
        CurrentChapterSet = session.CurrentChapterSet;
    }

    /// <summary>Updates the working edit buffer without changing clip ownership.</summary>
    public void SetCurrentChapterSet(ChapterSet? chapterSet) => CurrentChapterSet = chapterSet;

    /// <summary>
    /// Writes the edit buffer back into the typed clip session by mode
    /// and refreshes the buffer from the written entry.
    /// </summary>
    public void WriteBackCurrentChapterSet(ChapterSet info)
    {
        if (ClipSession is null)
        {
            CurrentChapterSet = info;
            return;
        }

        ClipSession = ClipSessionTransitions.WriteBack(ClipSession, info);
        CurrentChapterSet = ClipSession.CurrentChapterSet ?? info;
    }

    /// <summary>Selects a clip index; preserves session identity for append anti-stale checks.</summary>
    public void SelectClip(int index)
    {
        if (ClipSession is null)
        {
            return;
        }

        ClipSession = ClipSessionTransitions.Select(ClipSession, index);
        CurrentChapterSet = ClipSession.CurrentChapterSet;
    }

    /// <summary>Clears expression projection cache (e.g. when chapter set becomes null).</summary>
    public void ClearProjectionCache() => LastSuccessfulExpressionProjection = null;

    /// <summary>
    /// Builds an export-preference snapshot for save/preview from the workspace session
    /// and the current shell preference values.
    /// </summary>
    public ChapterExportOptions CreateExportOptions(ExportPreferenceInputs inputs) =>
        new(
            Format: inputs.Format,
            XmlLanguage: inputs.XmlLanguage,
            SourceFileName: CurrentChapterSet?.SourceName,
            AutoGenerateNames: inputs.AutoGenerateNames,
            UseTemplateNames: inputs.UseTemplateNames,
            ChapterNameTemplateText: inputs.ChapterNameTemplateText,
            OrderShift: inputs.OrderShift,
            ApplyExpression: inputs.ApplyExpression,
            Expression: inputs.Expression,
            ExpressionPresetId: inputs.ExpressionPresetId,
            ExpressionSourceName: inputs.ExpressionSourceName,
            TextEncoding: inputs.TextEncoding,
            EmitBom: inputs.EmitBom);

    /// <summary>
    /// Export options for already-projected chapter sets (expression/naming/order already applied).
    /// </summary>
    public ChapterExportOptions CreateExportOptionsForProjectedInfo(ExportPreferenceInputs inputs) =>
        CreateExportOptions(inputs) with
        {
            ApplyExpression = false,
            AutoGenerateNames = false,
            UseTemplateNames = false,
            ChapterNameTemplateText = string.Empty,
            OrderShift = 0,
            ProjectOutput = false
        };
}

/// <summary>
/// Shell-side preference inputs used to build a workspace export snapshot.
/// Bindable fields remain on the ViewModel until binding-authority work consolidates them.
/// </summary>
public readonly record struct ExportPreferenceInputs(
    ChapterExportFormat Format,
    string XmlLanguage,
    bool AutoGenerateNames,
    bool UseTemplateNames,
    string ChapterNameTemplateText,
    int OrderShift,
    bool ApplyExpression,
    string Expression,
    string ExpressionPresetId,
    string ExpressionSourceName,
    OutputTextEncoding TextEncoding,
    bool EmitBom);
