using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Wasm.Services;

/// <summary>Provides chapter-content preview and editing operations for the browser workspace.</summary>
public sealed partial class WasmWorkspace
{
    private static readonly IReadOnlyDictionary<string, System.Globalization.CultureInfo> LanguageCultures =
        System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures)
            .SelectMany(culture => new[] { (Code: culture.TwoLetterISOLanguageName, Culture: culture), (Code: culture.ThreeLetterISOLanguageName, Culture: culture) })
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Culture, StringComparer.OrdinalIgnoreCase);

    private ChapterTrackId? contentPreviewTrackId;
    private object? contentPreviewDraft;

    /// <summary>Gets the current chapter-content preview, if one is prepared.</summary>
    public ChapterContentPreview? ContentPreview { get; private set; }

    /// <summary>Gets the projected changes and diagnostics for the current preview.</summary>
    public ExpressionPreviewProjection? ContentPreviewProjection => ContentPreview is { } preview
        ? ExpressionPreviewProjector.Build(preview, contentPreviewTrackId) : null;

    /// <summary>Gets whether the current preview no longer matches the workspace state.</summary>
    public bool IsContentPreviewStale => ContentPreview is { } preview
        && (session.ContentSession?.Snapshot.BaseToken != preview.BaseToken
            || CurrentTrackId != contentPreviewTrackId
            || !Equals(contentPreviewDraft, CaptureContentDraft(preview.Operation)));

    private object CaptureContentDraft(string operation) => operation switch
    {
        "Apply chapter options" => (ChapterNameModeIndex, OrderShift, ChapterNameTemplateText),
        "Shift chapter frames" => (SelectedFrameRateIndex, ExactFramesPerSecond),
        "Change chapter frame rate" => SelectedFrameRateIndex,
        _ => operation
    };

    private ChapterTrackId? CurrentTrackId => session.ContentSession?.Snapshot.Document.Tracks
        .ElementAtOrDefault(session.CurrentTrackIndex)?.Id;

    /// <summary>Prepares a preview of the current chapter naming and ordering options.</summary>
    public ChapterContentPreview? PrepareNamingPreview() => PrepareContentPreview("Apply chapter options", document =>
        OrderShift is < 0 or > 1000
            ? new(false, document, [], [localizer.T("Parity.InvalidNumber")])
            : candidateBuilder.ApplyOutputOptions(document, AutoGenerateNames, UseTemplateNames,
                ChapterNameTemplateText ?? string.Empty, OrderShift, false, "t"));

    /// <summary>Prepares a preview that shifts chapter frames by the specified amount.</summary>
    /// <param name="frames">The signed number of frames to shift.</param>
    public ChapterContentPreview? PrepareFrameShiftPreview(int frames) => PrepareContentPreview("Shift chapter frames", document =>
        frames is < -1000000 or > 1000000
            ? new(false, document, [], [localizer.T("Parity.InvalidShift")])
            : candidateBuilder.ShiftFrames(document,
                document.Tracks[0].Chapters.Where(chapter => chapter.Kind != ChapterKind.Separator)
                    .Select(chapter => chapter.Id).ToHashSet(), frames, ExactFramesPerSecond,
                EffectiveFrameRateOption.ExactRate));

    /// <summary>Prepares a preview that changes the chapter frame rate to the selected rate.</summary>
    public ChapterContentPreview? PrepareFrameRatePreview() => PrepareContentPreview("Change chapter frame rate", document =>
        candidateBuilder.ChangeFrameRate(document, ResolveSourceFrameRate(),
            ResolveSelectedFrameRateOption().Value));

    private decimal ResolveSourceFrameRate()
    {
        if (selectedFrameRateIndex <= 0 && EffectiveFrameRateOption.IsValid)
        {
            return EffectiveFrameRateOption.Value;
        }

        if (BaseChapterSet is { FramesPerSecond: > 0 } chapterSet)
        {
            return (decimal)chapterSet.FramesPerSecond;
        }

        var selected = ResolveSelectedFrameRateOption();
        return selected.IsValid ? selected.Value : EffectiveFrameRateOption.Value;
    }

    private ChapterContentPreview? PrepareContentPreview(string operation,
        Func<EditableChapterDocument, ChapterCandidateBuildResult> build)
    {
        var content = session.ContentSession;
        if (content is null || IsBusy)
        {
            return null;
        }
        var trackIndex = session.CurrentTrackIndex;
        contentPreviewTrackId = CurrentTrackId;
        contentPreviewDraft = CaptureContentDraft(operation);
        ContentPreview = ChapterContentOperationSession.Prepare(content, operation, document =>
        {
            var focused = FocusTrack(document, trackIndex);
            var built = build(focused);
            return built with { Candidate = built.IsValid
                ? ReplaceFocusedTrack(document, trackIndex, built.Candidate) : document };
        });
        Notify();
        return ContentPreview;
    }

    /// <summary>Applies the current valid preview as a content-session transaction.</summary>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns><see langword="true"/> if the preview was committed or made no changes.</returns>
    public async ValueTask<bool> ApplyContentPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (ContentPreview is not { IsValid: true } preview || IsBusy || IsContentPreviewStale
            || ContentPreviewProjection is not { HasChanges: true } || session.ContentSession is not { } content)
        {
            return false;
        }
        var outcome = await ChapterContentOperationSession.ApplyAsync(content, preview, cancellationToken);
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? localizer.T("History.NavigationFailed");
            Notify();
            return false;
        }
        session.PublishContentDocument(outcome.Snapshot.Document);
        if (preview.Operation == "Apply chapter options")
        {
            ChapterNameModeIndex = 0;
            OrderShift = 0;
        }
        CancelContentPreview();
        CompleteCandidate(outcome, "Status.Updated");
        return true;
    }

    /// <summary>Discards the current chapter-content preview.</summary>
    public void CancelContentPreview()
    {
        ContentPreview = null;
        contentPreviewTrackId = null;
        contentPreviewDraft = null;
        Notify();
    }

    /// <summary>Begins editing a chapter cell and captures the current session version.</summary>
    /// <param name="index">The chapter row index.</param>
    /// <param name="field">The cell field to edit.</param>
    /// <returns>A draft for the cell, or <see langword="null"/> if editing cannot begin.</returns>
    public WasmCellDraft? BeginCellEdit(int index, ChapterCellField field)
    {
        if (IsBusy || session.ContentSession is not { } content
            || index < 0 || index >= rows.Count || rows[index].IsSeparator || CurrentTrackId is not { } trackId)
        {
            return null;
        }
        var chapter = content.Snapshot.Document.Tracks[session.CurrentTrackIndex].Chapters[index];
        var original = field switch
        {
            ChapterCellField.Name => rows[index].Name,
            ChapterCellField.StartTime => rows[index].TimeText,
            ChapterCellField.Frame => rows[index].FramesInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        return new(chapter.Id, trackId, content.Snapshot.BaseToken, field, original);
    }

    /// <summary>Validates and commits an edited chapter cell.</summary>
    /// <param name="draft">The draft created when editing began.</param>
    /// <param name="value">The proposed cell value.</param>
    /// <returns>A localized validation error, or <see langword="null"/> when the edit succeeds.</returns>
    public async ValueTask<string?> CommitCellEditAsync(WasmCellDraft draft, string value)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (IsBusy || session.ContentSession is not { } content || CurrentTrackId != draft.TrackId
            || content.Snapshot.BaseToken != draft.BaseToken)
        {
            return localizer.T("Expression.State.Stale");
        }
        if (value == draft.OriginalValue)
        {
            return null;
        }
        var trackIndex = session.CurrentTrackIndex;
        var preview = ChapterContentOperationSession.Prepare(content, "Update chapter content", document =>
        {
            var focused = FocusTrack(document, trackIndex);
            var built = candidateBuilder.EditCell(focused, draft.ChapterId, draft.Field, value, (decimal)FramesPerSecond);
            return built with { Candidate = built.IsValid ? ReplaceFocusedTrack(document, trackIndex, built.Candidate) : document };
        });
        var outcome = await ChapterContentOperationSession.ApplyAsync(content, preview);
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            return localizer.T(draft.Field switch
            {
                ChapterCellField.Frame => "Diagnostic.FrameText.Invalid",
                ChapterCellField.StartTime => "Diagnostic.TimeText.Invalid",
                _ => "History.NavigationFailed"
            });
        }
        session.PublishContentDocument(outcome.Snapshot.Document);
        RefreshDisplay(updateStatus: false, statusKey: null);
        return null;
    }

    /// <summary>Selects the next or previous clip option in the requested direction.</summary>
    /// <param name="direction">A positive value selects forward; a negative value selects backward.</param>
    public void SelectAdjacentClip(int direction)
    {
        if (IsBusy || ClipOptions.Count == 0)
        {
            return;
        }
        var index = ClipOptions.ToList().FindIndex(clip => clip.Id == SelectedClipId);
        var next = index + Math.Sign(direction);
        if (next >= 0 && next < ClipOptions.Count)
        {
            SelectClip(ClipOptions[next].Id);
        }
    }

    /// <summary>Restores a saved naming draft and clears any prepared preview.</summary>
    /// <param name="mode">The naming mode index.</param>
    /// <param name="shift">The chapter ordering shift.</param>
    /// <param name="template">The chapter naming template.</param>
    /// <param name="status">The template validation status.</param>
    public void RestoreNamingDraft(int mode, int shift, string template, string status)
    {
        ChapterNameModeIndex = mode;
        OrderShift = shift;
        ChapterNameTemplateText = template;
        ChapterNameTemplateStatus = status;
        CancelContentPreview();
    }

    /// <summary>Returns a language code with its localized display name when available.</summary>
    /// <param name="code">The language code to resolve.</param>
    public string XmlLanguageName(string code)
    {
        var resource = code switch
        {
            "zh" or "zho" => "XmlLanguage.Chinese",
            "ja" or "jpn" => "Language.Japanese",
            "en" or "eng" => "Language.English",
            "de" or "deu" => "XmlLanguage.German",
            "fr" or "fra" => "XmlLanguage.French",
            "it" or "ita" => "XmlLanguage.Italian",
            "es" or "spa" => "XmlLanguage.Spanish",
            _ => null
        };
        if (resource is not null)
        {
            return $"{code} — {localizer.T(resource)}";
        }
        if (code == "und")
        {
            return $"{code} — {localizer.T("XmlLanguage.Undetermined")}";
        }
        if (!LanguageCultures.TryGetValue(code, out var culture))
        {
            return code;
        }
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(localizer.Culture);
            return $"{code} — {culture.DisplayName}";
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }
}

/// <summary>Captures the chapter cell and session version being edited.</summary>
/// <param name="ChapterId">The identifier of the edited chapter.</param>
/// <param name="TrackId">The identifier of the track containing the chapter.</param>
/// <param name="BaseToken">The session version captured when editing began.</param>
/// <param name="Field">The chapter cell field being edited.</param>
/// <param name="OriginalValue">The cell value captured when editing began.</param>
public sealed record WasmCellDraft(ChapterId ChapterId, ChapterTrackId TrackId,
    SessionBaseToken BaseToken, ChapterCellField Field, string OriginalValue);
