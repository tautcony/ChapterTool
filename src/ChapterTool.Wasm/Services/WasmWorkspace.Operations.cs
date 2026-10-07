using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Wasm.Services;

public sealed partial class WasmWorkspace
{
    private static readonly IReadOnlyDictionary<string, System.Globalization.CultureInfo> LanguageCultures =
        System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.NeutralCultures)
            .SelectMany(culture => new[] { (Code: culture.TwoLetterISOLanguageName, Culture: culture), (Code: culture.ThreeLetterISOLanguageName, Culture: culture) })
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Culture, StringComparer.OrdinalIgnoreCase);

    private ChapterContentPreview? contentPreview;
    private ChapterTrackId? contentPreviewTrackId;
    private object? contentPreviewDraft;

    public ChapterContentPreview? ContentPreview => contentPreview;

    public ExpressionPreviewProjection? ContentPreviewProjection => contentPreview is { } preview
        ? ExpressionPreviewProjector.Build(preview, contentPreviewTrackId) : null;

    public bool IsContentPreviewStale => contentPreview is { } preview
        && (session.ContentSession?.Snapshot.BaseToken != preview.BaseToken
            || CurrentTrackId != contentPreviewTrackId
            || !Equals(contentPreviewDraft, CaptureContentDraft(preview.Operation)));

    private object CaptureContentDraft(string operation) => operation switch
    {
        "Apply chapter options" => (ChapterNameModeIndex, OrderShift, ChapterNameTemplateText),
        "Shift chapter frames" => (SelectedFrameRateIndex, FramesPerSecond),
        "Change chapter frame rate" => SelectedFrameRateIndex,
        _ => operation
    };

    private ChapterTrackId? CurrentTrackId => session.ContentSession?.Snapshot.Document.Tracks
        .ElementAtOrDefault(session.CurrentTrackIndex)?.Id;

    public ChapterContentPreview? PrepareNamingPreview() => PrepareContentPreview("Apply chapter options", document =>
        OrderShift is < 0 or > 1000
            ? new(false, document, [], [localizer.T("Parity.InvalidNumber")])
            : candidateBuilder.ApplyOutputOptions(document, AutoGenerateNames, UseTemplateNames,
                ChapterNameTemplateText ?? string.Empty, OrderShift, false, "t"));

    public ChapterContentPreview? PrepareFrameShiftPreview(int frames) => PrepareContentPreview("Shift chapter frames", document =>
        frames is < -1000000 or > 1000000
            ? new(false, document, [], [localizer.T("Parity.InvalidShift")])
            : candidateBuilder.ShiftFrames(document,
                document.Tracks[0].Chapters.Where(chapter => chapter.Kind != ChapterKind.Separator)
                    .Select(chapter => chapter.Id).ToHashSet(), frames, (decimal)FramesPerSecond));

    public ChapterContentPreview? PrepareFrameRatePreview() => PrepareContentPreview("Change chapter frame rate", document =>
        candidateBuilder.ChangeFrameRate(document, (decimal)(BaseChapterSet?.FramesPerSecond ?? 0),
            ResolveSelectedFrameRateOption().Value));

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
        contentPreview = ChapterContentOperationSession.Prepare(content, operation, document =>
        {
            var focused = FocusTrack(document, trackIndex);
            var built = build(focused);
            return built with { Candidate = built.IsValid
                ? ReplaceFocusedTrack(document, trackIndex, built.Candidate) : document };
        });
        Notify();
        return contentPreview;
    }

    public async ValueTask<bool> ApplyContentPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (contentPreview is not { IsValid: true } preview || IsBusy || IsContentPreviewStale
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

    public void CancelContentPreview()
    {
        contentPreview = null;
        contentPreviewTrackId = null;
        contentPreviewDraft = null;
        Notify();
    }

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

    public void RestoreNamingDraft(int mode, int shift, string template, string status)
    {
        ChapterNameModeIndex = mode;
        OrderShift = shift;
        ChapterNameTemplateText = template;
        ChapterNameTemplateStatus = status;
        CancelContentPreview();
    }

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

public sealed record WasmCellDraft(ChapterId ChapterId, ChapterTrackId TrackId,
    SessionBaseToken BaseToken, ChapterCellField Field, string OriginalValue);
