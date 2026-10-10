using System.Collections.Immutable;
using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;

namespace ChapterTool.Wasm.Services;

/// <summary>
/// Browser-side workspace that mirrors Avalonia main-window load / grid / frames / expression / save flow.
/// Clip combine/append/select transitions use the shared Core session kernel.
/// </summary>
public sealed partial class WasmWorkspace : IDisposable
{
    public const long MaxLoadBytes = PortableInputPolicy.MaxBytes;

    private const decimal DefaultFrameAccuracyTolerance = 0.15m;

    private readonly WasmChapterService wasmChapterService;
    private readonly FrameRateService frameRateService = new();
    private readonly IChapterExpressionEngine expressionEngine;
    private readonly ChapterEditingService editingService;
    private readonly ChapterContentCandidateBuilder candidateBuilder;
    private readonly ChapterWorkspace session = new();
    private readonly WasmLocalizer localizer;
    private readonly long maxLoadBytes;
    private readonly List<WasmLogEntry> logs = [];
    private readonly HashSet<int> selectedRowIndexes = [];
    private IReadOnlyList<DiagnosticView> diagnostics = [];
    private ChapterTrackId? expressionPreviewTrackId;
    private bool expressionPreviewStale;

    private ChapterImportResult? importResult;
    private int activeGroupIndex;
    private List<ChapterRowModel> rows = [];
    private int selectedFrameRateIndex;
    private decimal exactFramesPerSecond;
    private FrameRateOption? appliedFrameRateOption;
    private int selectionAnchor = -1;
    private LoadedSourceSnapshot? lastLoadedSource;
    private string chapterNameTemplateText = string.Empty;
    private string chapterNameTemplateStatus;
    private string? statusLocalizationKey;
    private object[] statusLocalizationArgs = [];

    private ChapterSet? BaseChapterSet
    {
        get => session.CurrentChapterSet;
    }

    private ClipSession? ClipSessionState
    {
        get => session.ClipSession;
        set
        {
            if (value is null)
            {
                session.ClearSession();
            }
            else
            {
                session.ReplaceSession(value);
            }
        }
    }

    public WasmWorkspace(WasmChapterService wasmChapterService, WasmLocalizer? localizer = null, long? maxLoadBytes = null)
    {
        this.wasmChapterService = wasmChapterService;
        this.localizer = localizer ?? new WasmLocalizer();
        this.localizer.CultureChanged += OnCultureChanged;
        this.maxLoadBytes = maxLoadBytes is > 0 and var limit ? limit : MaxLoadBytes;
        expressionEngine = new LuaExpressionScriptService();
        AuthoringService = new ExpressionAuthoringService(expressionEngine);
        editingService = new ChapterEditingService(wasmChapterService.TimeFormatter);
        candidateBuilder = new ChapterContentCandidateBuilder(editingService);
        SaveFormatIndex = 0;
        ChapterNameModeIndex = 0;
        XmlLanguage = wasmChapterService.XmlLanguages.Contains("und", StringComparer.OrdinalIgnoreCase)
            ? "und"
            : wasmChapterService.XmlLanguages.FirstOrDefault() ?? "und";
        Expression = "t";
        ExpressionPresetId = string.Empty;
        TextEncoding = OutputTextEncoding.Utf8;
        EmitBom = false;
        FrameAccuracyTolerance = DefaultFrameAccuracyTolerance;
        EditingOptions = ChapterEditingOptions.Default;
        selectedFrameRateIndex = 0;
        SetLocalizedStatus("Status.Ready");
        chapterNameTemplateStatus = this.localizer.T("Status.TemplateNotSelected");
    }

    public string SourcePath { get; private set; } = string.Empty;

    public IExpressionAuthoringService AuthoringService { get; }

    public string ExpressionSourceName { get; set; } = string.Empty;

    public string StatusText { get; private set; } = string.Empty;

    public double Progress { get; private set; }

    public bool IsBusy { get; private set; }

    public bool CanSave => BaseChapterSet is not null && rows.Count > 0 && !IsBusy;

    public bool CanPreview => CanSave;

    public bool CanUndo
    {
        get
        {
            var history = session.ContentSession?.GetHistorySnapshot();
            return history is not null && history.CursorId != history.RootId && !IsBusy;
        }
    }

    public bool HasHistory => session.ContentSession is not null;

    public bool HasUnexportedChanges
    {
        get
        {
            if (BaseChapterSet is null)
            {
                return false;
            }

            var snapshot = session.CaptureExportSnapshot(BaseChapterSet, CreateExportOptions(), session.CurrentTrackIndex);
            return !session.IsExported(snapshot);
        }
    }

    public bool HasActiveDraft => ContentPreview is not null || ExpressionPreview is not null;

    public bool RequiresSessionLossConfirmation
    {
        get
        {
            var history = session.ContentSession?.GetHistorySnapshot();
            return (history is not null && history.Nodes.Length > 1) || HasActiveDraft || HasUnexportedChanges;
        }
    }

    public Func<CancellationToken, ValueTask<bool>>? SessionLossConfirmation { get; set; }

    public bool CanRedo => PreferredRedoNode is not null && !IsBusy;

    public ICollection<WasmHistoryEntry> HistoryEntries
        => GetHistoryEntries(null);

    public ICollection<WasmHistoryEntry> GetHistoryEntries(IReadOnlySet<Guid>? expandedNodeIds)
    {
        var history = session.ContentSession?.GetHistorySnapshot();
        if (history is null)
        {
            return [];
        }

        var nodes = history.Nodes.ToDictionary(static node => node.Id);
        var rows = new List<WasmHistoryEntry>(nodes.Count);
        var pending = new Stack<(Guid Id, int Depth)>();
        pending.Push((history.RootId, 0));
        while (pending.TryPop(out var item))
        {
            if (!nodes.TryGetValue(item.Id, out var node))
            {
                continue;
            }

            var hiddenCurrent = expandedNodeIds?.Contains(node.Id) == false
                && node.Id != history.CursorId
                && IsAncestorOf(node.Id, history.CursorId, nodes);
            rows.Add(new WasmHistoryEntry(node.Id, node.ParentId, node.Description, item.Depth, node.Id == history.CursorId,
                !node.ChildIds.IsEmpty, hiddenCurrent));
            if (expandedNodeIds?.Contains(node.Id) == false)
            {
                continue;
            }
            for (var index = node.ChildIds.Length - 1; index >= 0; index--)
            {
                pending.Push((node.ChildIds[index], item.Depth + 1));
            }
        }

        return rows;
    }

    public bool HasPendingContentPreview => ContentPreview is not null || ExpressionPreview is not null;

    public bool IsHistoryEnded => session.ContentSession?.IsEnded ?? true;

    public async ValueTask<HistoryInspectionOutcome> InspectHistoryAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        session.ContentSession is { } content
            ? await content.InspectHistoryNodeAsync(nodeId, cancellationToken)
            : new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.Ended, null, []);

    public async ValueTask RestoreHistoryAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        await NavigateHistoryAsync("restore", nodeId, cancellationToken);

    private static bool IsAncestorOf(Guid ancestorId, Guid nodeId,
        IReadOnlyDictionary<Guid, ChapterTool.Core.Session.SessionHistoryNodeSnapshot> nodes)
    {
        while (nodes.TryGetValue(nodeId, out var node) && node.ParentId is Guid parentId)
        {
            if (parentId == ancestorId)
            {
                return true;
            }
            nodeId = parentId;
        }
        return false;
    }

    private SessionHistoryNodeSnapshot? PreferredRedoNode
    {
        get
        {
            var history = session.ContentSession?.GetHistorySnapshot();
            var cursor = history?.Nodes.FirstOrDefault(node => node.Id == history.CursorId);
            var childId = cursor is null
                ? (Guid?)null
                : cursor.PreferredChildId ?? (cursor.ChildIds.IsEmpty ? null : cursor.ChildIds[0]);
            return childId is null ? null : history?.Nodes.FirstOrDefault(node => node.Id == childId.Value);
        }
    }

    public bool CanRefreshRows => BaseChapterSet is not null && !IsBusy;

    public bool CanReload => lastLoadedSource is not null && !IsBusy;

    public bool IsChapterGridEmpty => rows.Count == 0;

    public IReadOnlyList<ChapterRowModel> Rows => rows;

    public IReadOnlyList<ClipOption> ClipOptions { get; private set; } = [];

    public string? SelectedClipId { get; private set; }

    public bool IsClipSelectionVisible => ClipOptions.Count > 0 || IsClipCombined;

    public bool IsClipCombined => ClipSessionState?.IsCombined == true;

    public bool CanToggleClipCombine => !IsBusy && ClipSessionState?.CanCombine == true;

    public bool CanAppendMpls => !IsBusy && ClipSessionState?.CanAppendMpls == true;

    public IReadOnlyList<SaveFormatOption> SaveFormats => wasmChapterService.SaveFormats;

    public IReadOnlyList<string> ChapterNameModes { get; } =
    [
        "As is",
        "Auto generate",
        "Template"
    ];

    public IReadOnlyList<string> XmlLanguages => wasmChapterService.XmlLanguages;

    public IReadOnlyList<FrameRateChoice> FrameRateChoices { get; private set; } = [];

    public int SaveFormatIndex { get; set; }

    public int ChapterNameModeIndex { get; set; }

    public bool UseTemplateNames => ChapterNameModeIndex == 2;

    public bool AutoGenerateNames => ChapterNameModeIndex == 1;

    public string? ChapterNameTemplateText
    {
        get => chapterNameTemplateText;
        private set => chapterNameTemplateText = value ?? string.Empty;
    }

    public string? ChapterNameTemplateStatus
    {
        get => chapterNameTemplateStatus;
        private set => chapterNameTemplateStatus = value ?? string.Empty;
    }

    public int OrderShift { get; set; }

    public string XmlLanguage { get; set; }

    /// <summary>Gets the read-only candidate prepared from the current committed document.</summary>
    public ChapterContentPreview? ExpressionPreview { get; private set; }

    public ExpressionPreviewProjection? ExpressionPreviewProjection
    {
        get
        {
            if (ExpressionPreview is not { } preview)
            {
                return null;
            }

            var appliedRate = EffectiveFrameRateOption;
            return ExpressionPreviewProjector.Build(preview, expressionPreviewTrackId, ExactFramesPerSecond,
                RoundFrames ? 0 : EditingOptions.EffectiveFrameDecimalPlaces,
                appliedRate.Value == ExactFramesPerSecond ? appliedRate.ExactRate : null);
        }
    }

    public bool IsExpressionPreviewStale => expressionPreviewStale
        || (ExpressionPreview is { } preview
            && (session.ContentSession?.Snapshot.BaseToken != preview.BaseToken
                || CurrentTrackId != expressionPreviewTrackId));

    public string FormatExpressionTimeTicks(long ticks)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(localizer.Culture);
        var totalHours = ticks / TimeSpan.TicksPerHour;
        var minutes = (ticks / TimeSpan.TicksPerMinute) % 60;
        var seconds = (ticks / TimeSpan.TicksPerSecond) % 60;
        var fraction = Math.Abs(ticks % TimeSpan.TicksPerSecond).ToString("D7", System.Globalization.CultureInfo.InvariantCulture);
        var precision = ticks % 10_000 == 0 ? 3 : ticks % 10 == 0 ? 6 : 7;
        return string.Create(culture, $"{totalHours:00}:{minutes:00}:{seconds:00}{culture.NumberFormat.NumberDecimalSeparator}{fraction[..precision]}");
    }

    public string FormatExpressionPropertyName(string property) => property switch
    {
        "Title" => localizer.T("Expression.Property.Title"),
        "SourceName" => localizer.T("Expression.Property.Source"),
        "ImportFormat" => localizer.T("Expression.Property.ImportFormat"),
        "Duration" => localizer.T("Expression.Property.Duration"),
        "FrameRate" => localizer.T("Expression.Property.FrameRate"),
        "Name" => localizer.T("Expression.Property.Name"),
        "DisplayNumber" => localizer.T("Expression.Property.Number"),
        "Kind" => localizer.T("Expression.Property.Type"),
        "Presence" => localizer.T("Expression.Property.Presence"),
        "TrackOrder" => localizer.T("Expression.Property.TrackOrder"),
        _ => localizer.T("Expression.Property.Additional")
    };

    public string Expression
    {
        get;
        set
        {
            field = value;

            // Free-form edits clear the preset selection unless the text still matches the selected preset.
            if (!string.IsNullOrWhiteSpace(ExpressionPresetId))
            {
                var preset = ExpressionPresets.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, ExpressionPresetId, StringComparison.OrdinalIgnoreCase));
                if (preset is null
                    || !string.Equals(preset.ScriptText, value, StringComparison.Ordinal))
                {
                    ExpressionPresetId = string.Empty;
                }
            }
        }
    }

    /// <summary>Gets the built-in Core expression presets (same engine as desktop).</summary>
    public IReadOnlyList<ChapterExpressionPreset> ExpressionPresets => expressionEngine.Presets;

    /// <summary>Gets the selected expression preset id, or empty when the expression is free-form.</summary>
    public string ExpressionPresetId { get; private set; }

    public bool RoundFrames
    {
        get => EditingOptions.FrameDisplay == FrameDisplayMode.Round;
        set
        {
            if (value)
            {
                EditingOptions = EditingOptions with { FrameDisplay = FrameDisplayMode.Round };
            }
            else if (EditingOptions.FrameDisplay == FrameDisplayMode.Round)
            {
                EditingOptions = EditingOptions with { FrameDisplay = FrameDisplayMode.FullPrecision };
            }
        }
    }

    public OutputTextEncoding TextEncoding { get; set; }

    public bool EmitBom { get; set; }

    public decimal FrameAccuracyTolerance
    {
        get;
        set => field = value <= 0
            ? DefaultFrameAccuracyTolerance
            : Math.Clamp(value, 0.01m, 0.30m);
    }

    public ChapterEditingOptions EditingOptions { get; set; }

    public string OutputTextEncodingId => OutputTextEncodings.Id(TextEncoding);

    public int SelectedFrameRateIndex
    {
        get => selectedFrameRateIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, FrameRateChoices.Count - 1));
            if (selectedFrameRateIndex == clamped)
            {
                return;
            }

            selectedFrameRateIndex = clamped;
            RefreshDisplay(updateStatus: true, statusKey: null);
        }
    }

    public double FramesPerSecond { get; private set; }

    internal decimal ExactFramesPerSecond => exactFramesPerSecond;

    public FramePresentationParts FormatFramePresentation(
        long startTicks,
        ChapterFrameRate? sourceRate,
        string numericFallback,
        bool showRepeatingDecimals)
    {
        var selected = EffectiveFrameRateOption;
        ChapterFrameRate? exactRate = sourceRate;
        if (selected.ExactRate is { } selectedExact
            && selected.Value == exactFramesPerSecond
            && (sourceRate is null
                || Math.Abs((decimal)sourceRate.Value.Numerator / sourceRate.Value.Denominator - selected.Value) < 0.000001m))
        {
            exactRate = selectedExact;
        }

        var input = exactRate is { } rate
            ? FramePresentationInput.FromExactRate(startTicks, rate, numericFallback)
            : FramePresentationInput.FromApproximateRate(startTicks, exactFramesPerSecond, numericFallback);
        var policy = new FramePresentationPolicy(RoundFrames,
            showRepeatingDecimals && EditingOptions.FrameDisplay == FrameDisplayMode.FullPrecision,
            EditingOptions.EffectiveFrameDecimalPlaces);
        return FrameValueFormatter.Format(input, policy);
    }

    public FramePresentationParts FormatRowFramePresentation(int index, string numericFallback, bool showRepeatingDecimals)
    {
        if (BaseChapterSet is null || index < 0 || index >= BaseChapterSet.Chapters.Count)
        {
            return FrameValueFormatter.Format(FramePresentationInput.FromNumericText(numericFallback),
                new FramePresentationPolicy(RoundFrames,
                    showRepeatingDecimals && EditingOptions.FrameDisplay == FrameDisplayMode.FullPrecision,
                    EditingOptions.EffectiveFrameDecimalPlaces));
        }

        return FormatFramePresentation(BaseChapterSet.Chapters[index].StartTime.Ticks, null,
            numericFallback, showRepeatingDecimals);
    }

    public string FramesPerSecondDisplay =>
        FramesPerSecond > 0
            ? FramesPerSecond.ToString("0.######")
            : "—";

    public bool IsXmlLanguageEnabled =>
        ChapterContentService.FormatAt(SaveFormatIndex) == ChapterExportFormat.Xml;

    public IReadOnlyList<WasmLogEntry> Logs => logs;

    public int SelectedRowIndex { get; private set; } = -1;

    public IReadOnlyCollection<int> SelectedRowIndexes => selectedRowIndexes;

    public bool HasRowSelection => selectedRowIndexes.Count > 0;

    public int PreferredFrameRateIndex { get; set; }

    public IReadOnlyList<RelatedMediaItem> RelatedMediaReferences
    {
        get
        {
            if (ClipSessionState is null)
            {
                return [];
            }

            return
            [
                .. ClipSessionState.RelatedMedia
                    .Select(static media =>
                        new RelatedMediaItem(media.DisplayName, media.RelativePath, media.AbsolutePath))
            ];
        }
    }

    public bool CanOpenRelatedMedia => RelatedMediaReferences.Count > 0;

    public event Action? Changed;

    public void ClearLogs()
    {
        logs.Clear();
        Notify();
    }

    public void RecordAction(string message)
    {
        AddLog("Info", message);
        Notify();
    }

    public void SetStatusMessage(string message)
    {
        statusLocalizationKey = null;
        statusLocalizationArgs = [];
        StatusText = message;
        Notify();
    }

    public async ValueTask UndoAsync(CancellationToken cancellationToken = default) =>
        await NavigateHistoryAsync("undo", null, cancellationToken);

    public async ValueTask RedoAsync(CancellationToken cancellationToken = default) =>
        await NavigateHistoryAsync("redo", null, cancellationToken);

    public async ValueTask NavigateHistoryAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        await NavigateHistoryAsync("navigate", nodeId, cancellationToken);

    private async ValueTask NavigateHistoryAsync(string action, Guid? nodeId, CancellationToken cancellationToken)
    {
        var content = session.ContentSession;
        if (content is null || IsBusy)
        {
            return;
        }

        var outcome = action switch
        {
            "undo" => await content.UndoAsync(cancellationToken),
            "redo" => await content.RedoAsync(cancellationToken),
            _ => await content.NavigateToAsync(nodeId!.Value, cancellationToken)
        };
        if (outcome.Kind == HistoryNavigationOutcomeKind.Committed)
        {
            session.PublishContentDocument(outcome.Snapshot.Document);
            var statusKey = action switch
            {
                "undo" => "History.UndoApplied",
                "restore" => "History.RestoreApplied",
                _ => "History.RedoApplied"
            };
            RefreshDisplay(updateStatus: true, statusKey);
        }
        else if (outcome.Kind is not HistoryNavigationOutcomeKind.NoChange)
        {
            SetRawStatus(outcome.Errors.FirstOrDefault() ?? localizer.T("History.NavigationFailed"));
        }
    }

    public void Dispose()
    {
        localizer.CultureChanged -= OnCultureChanged;
        session.ClearSession();
    }

    public void SelectRow(int index, bool ctrl = false, bool shift = false)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }

        if (shift && selectionAnchor >= 0)
        {
            var start = Math.Min(selectionAnchor, index);
            var end = Math.Max(selectionAnchor, index);
            selectedRowIndexes.Clear();
            for (var i = start; i <= end; i++)
            {
                selectedRowIndexes.Add(i);
            }
        }
        else if (ctrl)
        {
            if (!selectedRowIndexes.Add(index))
            {
                selectedRowIndexes.Remove(index);
            }

            selectionAnchor = index;
        }
        else
        {
            selectedRowIndexes.Clear();
            selectedRowIndexes.Add(index);
            selectionAnchor = index;
        }

        SelectedRowIndex = selectedRowIndexes.Count > 0
            ? selectedRowIndexes.Contains(index) ? index : selectedRowIndexes.Max()
            : -1;
        SetLocalizedStatus("Status.SelectedRows", selectedRowIndexes.Count);
        AddLog("Info", StatusText);
        Notify();
    }

    public void EnsureRowSelected(int index)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }

        if (!selectedRowIndexes.Contains(index))
        {
            SelectRow(index);
        }
        else
        {
            SelectedRowIndex = index;
            Notify();
        }
    }

    public bool IsRowSelected(int index) => selectedRowIndexes.Contains(index);

    public void InsertBefore(int index)
    {
        if (BaseChapterSet is null || IsBusy)
        {
            return;
        }

        var track = session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex];
        var insertIndex = Math.Clamp(index, 0, track.Chapters.Length);
        var beforeId = insertIndex < track.Chapters.Length ? track.Chapters[insertIndex].Id : (ChapterId?)null;
        var outcome = session.ExecuteTrackCandidate("Insert chapter", document => candidateBuilder.InsertBefore(document, beforeId));
        CompleteCandidate(outcome, "Status.Inserted");
        SelectRow(Math.Clamp(index, 0, Math.Max(0, rows.Count)));
    }

    public void DuplicateRow(int index)
    {
        if (BaseChapterSet is null || index < 0 || index >= BaseChapterSet.Chapters.Count || IsBusy)
        {
            return;
        }

        var track = session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex];
        var source = track.Chapters[index];
        var beforeId = index + 1 < track.Chapters.Length ? track.Chapters[index + 1].Id : (ChapterId?)null;
        var sourceId = source.Id;
        var outcome = session.ExecuteTrackCandidate("Duplicate chapter", document =>
        {
            var inserted = candidateBuilder.InsertBefore(document, beforeId);
            if (!inserted.IsValid)
            {
                return inserted;
            }

            var chapters = inserted.Candidate.Tracks[0].Chapters;
            var newIndex = Array.FindIndex(chapters.ToArray(), chapter => chapter.Id != sourceId && !document.Tracks[0].Chapters.Any(original => original.Id == chapter.Id));
            if (newIndex < 0)
            {
                return new ChapterCandidateBuildResult(false, document, [], ["The duplicate candidate was not created."]);
            }

            var duplicate = chapters[newIndex] with
            {
                DisplayNumber = chapters[newIndex].DisplayNumber,
                StartTicks = source.StartTicks,
                Name = source.Name,
                FramesInfo = source.FramesInfo,
                EndTicks = source.EndTicks,
                FrameAccuracy = source.FrameAccuracy,
                Kind = source.Kind
            };
            var updated = chapters.SetItem(newIndex, duplicate);
            var updatedTrack = new EditableChapterTrack(inserted.Candidate.Tracks[0].Id,
                inserted.Candidate.Tracks[0].Name, updated, inserted.Candidate.Tracks[0].Segments);
            return inserted with { Candidate = new EditableChapterDocument(inserted.Candidate.Id,
                inserted.Candidate.Title, inserted.Candidate.SourceName, inserted.Candidate.ImportFormat,
                inserted.Candidate.Duration, inserted.Candidate.FrameRate, [updatedTrack]) };
        });
        CompleteCandidate(outcome, "Status.Duplicated", index + 1);
        SelectRow(index + 1);
        AddLog("Info", localizer.Format("Status.Duplicated", index + 1));
        RefreshDisplay(updateStatus: true, statusKey: "Status.Duplicated", statusArgs: [index + 1]);
    }

    public void DeleteSelectedRows()
    {
        if (BaseChapterSet is null || IsBusy)
        {
            return;
        }

        var indexes = ResolveEditIndexes();
        if (indexes.Count == 0)
        {
            SetLocalizedStatus("Status.NoSelection");
            Notify();
            return;
        }

        var track = session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex];
        var targetIds = indexes.Where(index => index >= 0 && index < track.Chapters.Length)
            .Select(index => track.Chapters[index].Id).ToHashSet();
        selectedRowIndexes.Clear();
        SelectedRowIndex = -1;
        selectionAnchor = -1;
        var outcome = session.ExecuteTrackCandidate("Delete chapters", document => candidateBuilder.Delete(document, targetIds, EditingOptions));
        CompleteCandidate(outcome, "Status.Deleted", indexes.Count);
    }

    public void DeleteRow(int index)
    {
        EnsureRowSelected(index);
        DeleteSelectedRows();
    }

    public string SelectedRowsText(bool includeTime = true)
    {
        var indexes = ResolveEditIndexes().OrderBy(static i => i).ToArray();
        if (indexes.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            indexes.Select(index => RowText(index, includeTime)).Where(static text => text.Length > 0));
    }

    public string RowText(int index, bool includeTime = true)
    {
        if (index < 0 || index >= rows.Count)
        {
            return string.Empty;
        }

        var row = rows[index];
        return includeTime ? $"{row.TimeText}\t{row.Name}" : row.Name;
    }

    public string CreateZonesForSelection()
    {
        if (BaseChapterSet is null || FramesPerSecond <= 0)
        {
            return string.Empty;
        }

        var indexes = ResolveEditIndexes();
        if (indexes.Count == 0)
        {
            SetLocalizedStatus("Status.NoSelection");
            Notify();
            return string.Empty;
        }

        var result = editingService.CreateZones(BaseChapterSet, indexes, ExactFramesPerSecond);
        RecordDiagnostics(result.Diagnostics);
        if (result.Zones.Length > 0)
        {
            SetLocalizedStatus("Status.ZonesGenerated");
            AddLog("Info", $"Generated zones for {indexes.Count} row(s).", result.Zones);
        }
        else
        {
            StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.ZonesEmpty");
        }

        Notify();

        return result.Zones;
    }

    public string CreateZones(int index)
    {
        EnsureRowSelected(index);
        return CreateZonesForSelection();
    }

    public void ShiftFramesForward(int frames)
    {
        if (BaseChapterSet is null || IsBusy)
        {
            return;
        }

        if (frames <= 0)
        {
            SetLocalizedStatus("Status.CannotShift");
            Notify();
            return;
        }

        var track = session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex];
        var fps = exactFramesPerSecond;
        var targets = track.Chapters.Select(static chapter => chapter.Id).ToHashSet();
        var exactRate = EffectiveFrameRateOption.ExactRate;
        var outcome = session.ExecuteTrackCandidate("Shift chapter frames", document => candidateBuilder.ShiftFrames(document, targets, frames, fps, exactRate));
        CompleteCandidate(outcome, "Status.Shifted", frames);
    }

    public async Task LoadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var operationRevision = session.BeginLoadOperation();
        BeginBusy("Status.Loading");
        try
        {
            Progress = 0.2;
            Notify();

            if (content.Length == 0)
            {
                SetLocalizedStatus("Status.DropEmpty");
                AddLog("Error", StatusText);
                return;
            }

            if (!PortableInputPolicy.IsWithinLimit(content.LongLength) || content.LongLength > maxLoadBytes)
            {
                SetLocalizedStatus("Status.DropTooLarge");
                AddLog("Error", StatusText);
                return;
            }

            var result = await wasmChapterService.ImportAsync(fileName, content, cancellationToken);
            AddLog("Info", $"Loading {fileName} ({content.Length:N0} bytes).");
            Progress = 0.8;
            Notify();

            if (!result.Success || result.Groups.Count == 0)
            {
                diagnostics = WasmWorkspaceProjection.ToDiagnostics(result.Diagnostics);
                StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.LoadFailed");
                AddLog("Error", StatusText);
                return;
            }

            var replacementToken = session.CaptureContentToken();
            var candidate = result.Groups[0];
            var candidateSession = ClipSessionTransitions.FromLoad(candidate);
            var candidateDocument = EditableChapterDocumentAdapter.FromChapterImportSource(candidate);
            while (RequiresSessionLossConfirmation && SessionLossConfirmation is not null)
            {
                if (!await SessionLossConfirmation(cancellationToken))
                {
                    SetLocalizedStatus("Status.SessionReplacementCanceled");
                    return;
                }

                if (session.IsContentTokenCurrent(replacementToken))
                {
                    break;
                }

                replacementToken = session.CaptureContentToken();
            }

            if (!ApplySuccessfulLoad(fileName, content, result, operationRevision, candidateSession, candidateDocument, replacementToken))
            {
                return;
            }

            Progress = 1;
            AddLog("Info", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            diagnostics = [];
            AddLog("Error", localizer.T("Status.LoadFailed"), ex.ToString());
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (lastLoadedSource is null)
        {
            SetLocalizedStatus("Status.NoReload");
            AddLog("Warning", StatusText);
            Notify();
            return;
        }

        await LoadAsync(lastLoadedSource.FileName, lastLoadedSource.Content, cancellationToken);
    }

    public async Task AppendMplsAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (!CanAppendMpls || ClipSessionState is null)
        {
            SetLocalizedStatus("Status.CannotAppend");
            AddLog("Warning", StatusText);
            Notify();
            return;
        }

        BeginBusy("Status.Appending");
        var operationRevision = session.CaptureRevision();
        var expectedSessionId = ClipSessionState.SessionId;
        var contentSession = session.ContentSession;
        if (contentSession is null)
        {
            SetLocalizedStatus("Status.CannotAppend");
            Notify();
            return;
        }
        var capturedContent = contentSession.Snapshot;
        try
        {
            Progress = 0.2;
            Notify();

            if (content.Length == 0)
            {
                SetLocalizedStatus("Status.DropEmpty");
                AddLog("Error", StatusText);
                return;
            }

            if (!PortableInputPolicy.IsWithinLimit(content.LongLength) || content.LongLength > maxLoadBytes)
            {
                SetLocalizedStatus("Status.DropTooLarge");
                AddLog("Error", StatusText);
                return;
            }

            var result = await wasmChapterService.ImportAsync(fileName, content, cancellationToken);
            Progress = 0.7;
            Notify();

            if (!result.Success || result.Groups.Count == 0)
            {
                RecordDiagnostics(result.Diagnostics);
                StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.AppendFailed");
                AddLog("Error", StatusText);
                return;
            }

            var appendedGroup = result.Groups[0];
            var append = session.AppendClipSource(operationRevision, expectedSessionId, capturedContent, appendedGroup, cancellationToken);
            if (append.IsStale)
            {
                return;
            }

            if (!append.Succeeded || append.Session is null || append.Document is null)
            {
                var message = string.Join("; ", append.Errors);
                StatusText = message.Length == 0 ? localizer.T("Status.AppendFailed") : message;
                AddLog("Error", StatusText);
                return;
            }

            if (importResult is not null)
            {
                var groups = importResult.Groups.ToList();
                groups[activeGroupIndex] = ClipSessionState.OriginalGroup;
                importResult = new ChapterImportResult(true, groups, result.Diagnostics);
            }

            SyncUiFromClipSession();
            ClearSelection();
            RebuildFrameRateChoices(BaseChapterSet!);
            RefreshDisplay(
                updateStatus: true,
                statusKey: "Status.Appended",
                statusArgs: [appendedGroup.Entries.Count, Path.GetFileName(fileName)]);
            Progress = 1;
            AddLog("Info", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            AddLog("Error", localizer.T("Status.AppendFailed"), ex.ToString());
        }
        finally
        {
            EndBusy();
        }
    }

    public void SelectClip(string? clipId)
    {
        if (IsClipCombined || ClipSessionState is null || importResult is null)
        {
            return;
        }

        if (string.Equals(SelectedClipId, clipId, StringComparison.Ordinal))
        {
            return;
        }

        var option = ClipOptions.FirstOrDefault(candidate => string.Equals(candidate.Id, clipId, StringComparison.Ordinal));
        if (option is null)
        {
            return;
        }

        if (option.GroupIndex != activeGroupIndex)
        {
            // Browser may surface multiple import groups; switch the shared session to that group.
            if (option.GroupIndex < 0 || option.GroupIndex >= importResult.Groups.Count)
            {
                return;
            }

            activeGroupIndex = option.GroupIndex;
            var sourceGroup = importResult.Groups[activeGroupIndex];
            var newSession = ClipSessionTransitions.FromLoad(sourceGroup);
            session.ReplaceSession(newSession, EditableChapterDocumentAdapter.FromChapterImportSource(sourceGroup));
            ClipSessionState = newSession;
            if (option.EntryIndex >= 0)
            {
                ClipSessionState = ClipSessionTransitions.Select(ClipSessionState, option.EntryIndex);
            }
        }
        else if (option.EntryIndex >= 0)
        {
            ClipSessionState = ClipSessionTransitions.Select(ClipSessionState, option.EntryIndex);
        }

        SyncUiFromClipSession();
        ClearSelection();
        RefreshDisplay(
            updateStatus: true,
            statusKey: "Status.SelectedClip",
            statusArgs: [option.DisplayText]);
    }

    public void ToggleClipCombine()
    {
        if (IsBusy || ClipSessionState is null || !CanToggleClipCombine)
        {
            return;
        }

        var transition = session.ToggleClipStructure();
        if (!transition.Succeeded || transition.Session is null)
        {
            RecordDiagnostics(transition.EditResult.Diagnostics);
            StatusText = WasmWorkspaceProjection.FirstError(transition.EditResult.Diagnostics) ?? localizer.T("Status.CombineFailed");
            Notify();
            return;
        }

        ClipSessionState = transition.Session;
        if (importResult is not null)
        {
            var groups = importResult.Groups.ToList();
            groups[activeGroupIndex] = ClipSessionState.OriginalGroup;
            importResult = importResult with { Groups = groups };
        }

        SyncUiFromClipSession();
        ClearSelection();
        if (transition.Restored)
        {
            AddLog("Info", localizer.T("Status.RestoredClips"));
            RefreshDisplay(updateStatus: true, statusKey: "Status.RestoredClips");
            return;
        }

        AddLog("Info", localizer.Format("Status.Combined", ClipSessionState.OriginalGroup.Entries.Count));
        RebuildFrameRateChoices(BaseChapterSet!);
        RefreshDisplay(updateStatus: true, statusKey: "Status.CombinedDone");
    }

    public void ChangeSelectedFrameRate()
    {
        if (!CanChangeSelectedFrameRate || BaseChapterSet is null)
        {
            return;
        }

        var sourceFps = (decimal)BaseChapterSet.FramesPerSecond;
        var target = ResolveSelectedFrameRateOption();
        var outcome = session.ExecuteTrackCandidate("Change chapter frame rate",
            document => candidateBuilder.ChangeFrameRate(document, sourceFps, target.Value));
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? localizer.T("Status.ChangeFpsFailed");
            Notify();
            return;
        }

        PreferredFrameRateIndex = selectedFrameRateIndex;
        AddLog("Info", localizer.Format("Status.ChangedFps", sourceFps, target.Value));
        RefreshDisplay(updateStatus: true, statusKey: "Status.ChangedFpsTo", statusArgs: [target.DisplayName]);
    }

    public bool CanChangeSelectedFrameRate =>
        BaseChapterSet is not null
        && BaseChapterSet.FramesPerSecond > 0
        && selectedFrameRateIndex > 0
        && ResolveSelectedFrameRateOption().IsValid;

    /// <summary>
    /// Refreshes derived display without applying content drafts.
    /// </summary>
    public void ApplyOptionsAndRefresh()
    {
        RefreshDisplay(updateStatus: false, statusKey: null);
    }

    /// <summary>
    /// Selects a built-in Core expression preset without changing the committed document.
    /// </summary>
    /// <param name="presetId">The preset identifier from <see cref="ExpressionPresets"/>.</param>
    /// <returns><see langword="true"/> when the preset was found and applied.</returns>
    public bool ApplyExpressionPreset(string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            ExpressionPresetId = string.Empty;
            return false;
        }

        var preset = expressionEngine.Presets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            SetLocalizedStatus("Status.ExpressionPresetUnknown", presetId);
            AddLog("Warning", StatusText);
            Notify();
            return false;
        }

        Expression = preset.ScriptText;
        ExpressionPresetId = preset.Id;
        AddLog("Info", localizer.Format("Status.ExpressionPresetApplied", preset.DisplayName));
        if (BaseChapterSet is null)
        {
            SetLocalizedStatus("Status.ExpressionPresetApplied", preset.DisplayName);
            Notify();
            return true;
        }

        return true;
    }

    /// <summary>Prepares a read-only expression candidate from the active session snapshot.</summary>
    public ChapterContentPreview? PrepareExpressionPreview(string expressionText)
    {
        Expression = expressionText ?? string.Empty;
        var content = session.ContentSession;
        if (string.IsNullOrWhiteSpace(Expression) || content is null || IsBusy)
        {
            ExpressionPreview = null;
            expressionPreviewTrackId = null;
            expressionPreviewStale = false;
            Notify();
            return null;
        }

        var trackIndex = session.CurrentTrackIndex;
        if (trackIndex < 0 || trackIndex >= content.Snapshot.Document.Tracks.Length)
        {
            ExpressionPreview = null;
            expressionPreviewTrackId = null;
            Notify();
            return null;
        }
        expressionPreviewTrackId = content.Snapshot.Document.Tracks[trackIndex].Id;
        expressionPreviewStale = false;
        ExpressionPreview = ChapterContentOperationSession.Prepare(content, "Apply chapter expression", document =>
        {
            var focused = AddDetectedFrameRate(FocusTrack(document, trackIndex));
            var built = candidateBuilder.ApplyExpression(focused, Expression.Trim());
            return built.IsValid
                ? built with { Candidate = ReplaceFocusedTrack(document, trackIndex, built.Candidate) }
                : built with { Candidate = document };
        });
        Notify();
        return ExpressionPreview;
    }

    /// <summary>Commits the latest valid expression candidate as one history transaction.</summary>
    public async ValueTask<bool> ApplyExpressionPreviewAsync(CancellationToken cancellationToken = default)
    {
        var preview = ExpressionPreview;
        var content = session.ContentSession;
        if (preview is null
            || !preview.IsValid
            || content is null
            || IsBusy
            || !ExpressionPreviewProjector.Build(preview, expressionPreviewTrackId).HasChanges)
        {
            return false;
        }

        var currentTrackIndex = session.CurrentTrackIndex;
        var currentTrackId = currentTrackIndex >= 0 && currentTrackIndex < content.Snapshot.Document.Tracks.Length
            ? content.Snapshot.Document.Tracks[currentTrackIndex].Id
            : (ChapterTrackId?)null;
        if (content.Snapshot.BaseToken != preview.BaseToken || currentTrackId != expressionPreviewTrackId)
        {
            expressionPreviewStale = true;
            Notify();
            return false;
        }

        var projection = ExpressionPreviewProjector.Build(preview, expressionPreviewTrackId);
        var outcome = await ChapterContentOperationSession.ApplyAsync(content, preview, cancellationToken);
        if (outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
        {
            session.PublishContentDocument(outcome.Snapshot.Document);
            ExpressionPreview = null;
            expressionPreviewTrackId = null;
            expressionPreviewStale = false;
            RefreshDisplay(updateStatus: false, statusKey: null);
            StatusText = localizer.Format("Expression.Preview.Applied",
                projection.AffectedChapterCount,
                projection.TimeChangedChapterCount,
                projection.FrameInformationChangedChapterCount);
            AddLog("Info", StatusText);
            Notify();
            return true;
        }

        if (outcome.Kind == TransactionOutcomeKind.Conflict)
        {
            expressionPreviewStale = true;
            Notify();
            return false;
        }

        StatusText = outcome.Errors.FirstOrDefault() ?? localizer.T("Status.ChangeFpsFailed");
        Notify();
        return false;
    }

    /// <summary>Discards the current expression candidate without changing the document.</summary>
    public void CancelExpressionPreview()
    {
        ExpressionPreview = null;
        expressionPreviewTrackId = null;
        expressionPreviewStale = false;
        Notify();
    }

    private static EditableChapterDocument FocusTrack(EditableChapterDocument source, int trackIndex)
    {
        var track = source.Tracks[trackIndex];
        var segment = track.Segments.FirstOrDefault();
        return new EditableChapterDocument(source.Id, segment?.Name ?? track.Name,
            segment?.SourceName ?? source.SourceName, segment?.ImportFormat ?? source.ImportFormat,
            EditableChapterDocumentAdapter.TrackDuration(source, trackIndex), segment?.FrameRate ?? source.FrameRate, [track]);
    }

    private EditableChapterDocument AddDetectedFrameRate(EditableChapterDocument source)
    {
        return EditableChapterDocumentAdapter.WithFallbackFrameRate(source,
            BaseChapterSet is null ? 0m : ApplyFrames(BaseChapterSet).FramesPerSecond);
    }

    private static EditableChapterDocument ReplaceFocusedTrack(
        EditableChapterDocument source,
        int trackIndex,
        EditableChapterDocument focused)
    {
        var tracks = source.Tracks.SetItem(trackIndex, focused.Tracks[0]);
        var rates = tracks.SelectMany(track => track.Segments.Select(segment => segment.FrameRate))
            .Where(rate => rate.HasValue).Select(rate => rate!.Value).Distinct().ToArray();
        return new EditableChapterDocument(source.Id, source.Title, source.SourceName, source.ImportFormat,
            EditableChapterDocumentAdapter.ReplacedTrackDuration(source, trackIndex, focused), rates.Length == 1 ? rates[0] : rates.Length == 0 ? source.FrameRate : null, tracks);
    }

    /// <summary>
    /// Clears the selected expression preset while keeping the current free-form expression text.
    /// </summary>
    public void ClearExpressionPresetSelection() => ExpressionPresetId = string.Empty;

    public void RefreshRows()
    {
        if (BaseChapterSet is null || IsBusy)
        {
            return;
        }

        RefreshDisplay(updateStatus: true, statusKey: "Status.RowsRefreshed", statusArgs: [FramesPerSecondDisplay, rows.Count]);
        AddLog("Info", StatusText);
    }

    public bool SetChapterNameTemplate(string fileName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetLocalizedStatus("Status.TemplateFailed");
            AddLog("Warning", StatusText);
            Notify();
            return false;
        }

        ChapterNameTemplateText = text;
        ChapterNameTemplateStatus = Path.GetFileName(fileName);
        ChapterNameModeIndex = 2;
        AddLog("Info", localizer.Format("Status.TemplateLoaded", ChapterNameTemplateStatus));
        Notify();
        return true;
    }

    public void ClearChapterNameTemplate()
    {
        ChapterNameTemplateText = string.Empty;
        ChapterNameTemplateStatus = localizer.T("Status.TemplateNotSelected");
        if (ChapterNameModeIndex == 2)
        {
            ChapterNameModeIndex = 0;
        }

        RefreshDisplay(updateStatus: false, statusKey: null);
    }

    public PreviewResult Preview()
    {
        if (!CanPreview || BaseChapterSet is null)
        {
            SetLocalizedStatus("Status.NoPreview");
            return new PreviewResult(false, StatusText);
        }

        try
        {
            var format = ChapterContentService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var export = wasmChapterService.Export(BaseChapterSet, options);
            diagnostics = WasmWorkspaceProjection.ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = WasmWorkspaceProjection.FirstError(export.Diagnostics) ?? localizer.T("Status.PreviewFailed");
                Notify();
                return new PreviewResult(false, StatusText);
            }

            var baseName = Path.GetFileNameWithoutExtension(SourcePath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "chapters";
            }

            var fileName = baseName + (export.FileExtension.StartsWith('.')
                ? export.FileExtension
                : wasmChapterService.FormatExtension(format));
            SetLocalizedStatus("Status.Previewed", wasmChapterService.FormatDisplayName(format), export.Content.Length);
            AddLog("Info", StatusText);
            Notify();
            return new PreviewResult(true, StatusText, export.Content, fileName);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            AddLog("Error", localizer.T("Status.PreviewFailed"), ex.ToString());
            Notify();
            return new PreviewResult(false, ex.Message);
        }
    }

    public SaveResult Save()
    {
        if (!CanSave || BaseChapterSet is null)
        {
            SetLocalizedStatus("Status.NothingToSave");
            return new SaveResult(false, StatusText);
        }

        try
        {
            // Ensure frames/FPS on the base set are current before export projection.
            var format = ChapterContentService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var exportSnapshot = session.CaptureExportSnapshot(BaseChapterSet, options, session.CurrentTrackIndex);
            var export = wasmChapterService.Export(BaseChapterSet, options);
            diagnostics = WasmWorkspaceProjection.ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = WasmWorkspaceProjection.FirstError(export.Diagnostics) ?? localizer.T("Status.SaveFailed");
                Notify();
                return new SaveResult(false, StatusText);
            }

            if (exportSnapshot is not null)
            {
                session.RecordSuccessfulExport(exportSnapshot);
            }

            var baseName = Path.GetFileNameWithoutExtension(SourcePath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "chapters";
            }

            var fileName = baseName + (export.FileExtension.StartsWith('.')
                ? export.FileExtension
                : wasmChapterService.FormatExtension(format));
            SetLocalizedStatus("Status.Saved", wasmChapterService.FormatDisplayName(format), fileName);
            AddLog("Info", StatusText);
            Notify();
            return new SaveResult(true, StatusText, export.Content, fileName);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            AddLog("Error", localizer.T("Status.SaveFailed"), ex.ToString());
            Notify();
            return new SaveResult(false, ex.Message);
        }
    }

    public void UpdateRow(int index, string? timeText, string? name)
    {
        if (BaseChapterSet is null || index < 0 || index >= BaseChapterSet.Chapters.Count)
        {
            return;
        }

        var track = session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex];
        var chapterId = track.Chapters[index].Id;
        var outcome = session.ExecuteTrackCandidate("Update chapter content", document =>
        {
            var candidate = new ChapterCandidateBuildResult(true, document, [chapterId], []);
            if (name is not null)
            {
                candidate = candidateBuilder.EditCell(candidate.Candidate, chapterId, ChapterCellField.Name, name, ExactFramesPerSecond);
            }

            if (candidate.IsValid && timeText is not null && track.Chapters[index].Kind != ChapterKind.Separator)
            {
                candidate = candidateBuilder.EditCell(candidate.Candidate, chapterId, ChapterCellField.StartTime, timeText, ExactFramesPerSecond);
            }

            return candidate;
        });
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? "The chapter edit was rejected.";
            Notify();
            return;
        }

        AddLog("Info", $"Edited row {index + 1}.");
        RefreshDisplay(updateStatus: false, statusKey: null);
    }

    public Task LoadSampleAsync(CancellationToken cancellationToken = default)
    {
        var sample = """
            CHAPTER01=00:00:00.000
            CHAPTER01NAME=Opening
            CHAPTER02=00:01:23.456
            CHAPTER02NAME=Act 1
            CHAPTER03=00:12:34.567
            CHAPTER03NAME=Credits
            """u8.ToArray();
        return LoadAsync("sample.txt", sample, cancellationToken);
    }

    private bool ApplySuccessfulLoad(
        string fileName,
        byte[] content,
        ChapterImportResult result,
        int operationRevision,
        ClipSession newSession,
        EditableChapterDocument document,
        SessionBaseToken? expectedToken)
    {
        var sourceGroup = result.Groups.Count > 0 ? result.Groups[0] : null;
        if (sourceGroup is null
            || !session.TryCommitLoad(operationRevision, new LocalPathChapterSource(fileName), newSession, document, expectedToken))
        {
            return false;
        }

        lastLoadedSource = new LoadedSourceSnapshot(fileName, content);
        var groups = result.Groups.ToList();
        groups[0] = newSession.OriginalGroup;
        importResult = result with { Groups = groups };
        activeGroupIndex = 0;
        SourcePath = fileName;
        selectedFrameRateIndex = Math.Max(0, PreferredFrameRateIndex);
        ClearSelection();
        diagnostics = [];
        SyncUiFromClipSession(rebuildAllGroupOptions: true);
        RebuildFrameRateChoices(BaseChapterSet ?? new ChapterSet(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []));
        RefreshDisplay(
            updateStatus: true,
            statusKey: "Status.Loaded",
            statusArgs: [BaseChapterSet?.Chapters.Count ?? 0, Path.GetFileName(fileName)]);
        return true;
    }

    private void SyncUiFromClipSession(bool rebuildAllGroupOptions = false)
    {
        if (ClipSessionState is null)
        {
            ClipOptions = [];
            SelectedClipId = null;
            return;
        }

        if (rebuildAllGroupOptions && importResult is not null && importResult.Groups.Count > 1 && !ClipSessionState.IsCombined)
        {
            ClipOptions = WasmWorkspaceProjection.BuildClipOptions(importResult, localizer);
            var selectedEntry = ClipSessionState.SelectedIndex >= 0 && ClipSessionState.SelectedIndex < ClipSessionState.ClipOptions.Count
                ? ClipSessionState.ClipOptions[ClipSessionState.SelectedIndex]
                : null;
            SelectedClipId = selectedEntry is null
                ? ClipOptions.FirstOrDefault()?.Id
                : ClipOptions.FirstOrDefault(option =>
                    option.GroupIndex == activeGroupIndex
                    && option.EntryIndex == ClipSessionState.SelectedIndex)?.Id
                  ?? ClipOptions.FirstOrDefault()?.Id;
        }
        else
        {
            ClipOptions = WasmWorkspaceProjection.BuildClipOptionsFromSession(ClipSessionState, activeGroupIndex, localizer);
            SelectedClipId = ClipOptions.ElementAtOrDefault(Math.Max(0, ClipSessionState.SelectedIndex))?.Id
                ?? ClipOptions.FirstOrDefault()?.Id;
        }

        if (BaseChapterSet is not null)
        {
            FramesPerSecond = BaseChapterSet.FramesPerSecond;
        }
    }

    private void RefreshDisplay(bool updateStatus, string? statusKey, params object[] statusArgs)
    {
        if (BaseChapterSet is null)
        {
            rows = [];
            FramesPerSecond = 0;
            exactFramesPerSecond = 0;
            appliedFrameRateOption = null;
            if (updateStatus && statusKey is not null)
            {
                SetLocalizedStatus(statusKey, statusArgs);
            }

            Notify();
            return;
        }

        var selectedIds = selectedRowIndexes.Where(index => index >= 0 && index < rows.Count)
            .Select(index => rows[index].Id).ToHashSet();
        var framed = ApplyFrames(BaseChapterSet);
        appliedFrameRateOption = framed.SelectedOption;
        exactFramesPerSecond = framed.FramesPerSecond;
        FramesPerSecond = (double)framed.FramesPerSecond;
        RebuildFrameRateChoices(BaseChapterSet);
        var displayContent = framed.Info;
        rows =
        [
            .. displayContent.Chapters
                .Select((chapter, index) => WasmWorkspaceProjection.ToRow(chapter, wasmChapterService.TimeFormatter,
                    session.ContentSession!.Snapshot.Document.Tracks[session.CurrentTrackIndex].Chapters[index].Id))
        ];
        selectedRowIndexes.Clear();
        for (var index = 0; index < rows.Count; index++)
        {
            if (selectedIds.Contains(rows[index].Id))
            {
                selectedRowIndexes.Add(index);
            }
        }
        SelectedRowIndex = selectedRowIndexes.Count > 0 ? selectedRowIndexes.Max() : -1;
        selectionAnchor = SelectedRowIndex;

        diagnostics = [];
        if (updateStatus && statusKey is not null)
        {
            SetLocalizedStatus(statusKey, statusArgs);
        }
        else if (updateStatus)
        {
            SetLocalizedStatus("Status.FramesUpdated", FramesPerSecondDisplay, rows.Count);
        }

        Notify();
    }

    private FrameInfoResult ApplyFrames(ChapterSet info)
    {
        var option = ResolveSelectedFrameRateOption();

        if (option.LegacyMplsCode == 0)
        {
            option = frameRateService.DetectDetailed(info, FrameAccuracyTolerance).Option;
        }
        return frameRateService.UpdateFrames(info, option, EditingOptions.EffectiveFrameDecimalPlaces, FrameAccuracyTolerance);
    }

    private FrameRateOption ResolveSelectedFrameRateOption()
    {
        var options = frameRateService.Options;
        if (selectedFrameRateIndex <= 0 || selectedFrameRateIndex >= options.Count)
        {
            return options[0]; // Auto
        }

        return options[selectedFrameRateIndex];
    }

    private FrameRateOption EffectiveFrameRateOption => appliedFrameRateOption ?? ResolveSelectedFrameRateOption();

    private void RebuildFrameRateChoices(ChapterSet info)
    {
        var options = frameRateService.Options;
        var choices = new List<FrameRateChoice>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (i == 0)
            {
                choices.Add(new FrameRateChoice(i, "Auto", option));
                continue;
            }

            if (option is { IsValid: false, LegacyMplsCode: 5 })
            {
                // Skip reserved placeholder (matches Avalonia combo useful entries).
                continue;
            }

            choices.Add(new FrameRateChoice(i, option.DisplayName, option));
        }

        FrameRateChoices = choices;

        // Keep selected index on a still-valid option.
        if (choices.All(choice => choice.Index != selectedFrameRateIndex))
        {
            selectedFrameRateIndex = 0;
        }

        // Prefer matching detected/source fps when Auto is not forced and source has fps.
        _ = info;
    }

    private ChapterExportOptions CreateExportOptions() =>
        new(
            Format: ChapterContentService.FormatAt(SaveFormatIndex),
            XmlLanguage: XmlLanguage,
            SourceFileName: BaseChapterSet?.SourceName,
            AutoGenerateNames: false,
            UseTemplateNames: false,
            ChapterNameTemplateText: string.Empty,
            OrderShift: 0,
            ApplyExpression: false,
            Expression: "t",
            ExpressionPresetId: string.Empty,
            ExpressionSourceName: string.Empty,
            TextEncoding: TextEncoding,
            EmitBom: EmitBom,
            ProjectOutput: false);

    private void ClearSession(bool keepPath = false, bool keepReload = false)
    {
        importResult = null;
        ClipSessionState = null;
        activeGroupIndex = 0;
        rows = [];
        ClipOptions = [];
        SelectedClipId = null;
        FramesPerSecond = 0;
        exactFramesPerSecond = 0;
        FrameRateChoices = [];
        ClearSelection();
        diagnostics = [];
        if (!keepPath)
        {
            SourcePath = string.Empty;
        }

        if (!keepReload)
        {
            lastLoadedSource = null;
        }
    }

    private void ClearSelection()
    {
        selectedRowIndexes.Clear();
        SelectedRowIndex = -1;
        selectionAnchor = -1;
    }

    private HashSet<int> ResolveEditIndexes()
    {
        if (selectedRowIndexes.Count > 0)
        {
            return [.. selectedRowIndexes.Where(index => index >= 0 && index < rows.Count)];
        }

        if (SelectedRowIndex >= 0 && SelectedRowIndex < rows.Count)
        {
            return [SelectedRowIndex];
        }

        return [];
    }

    private void BeginBusy(string status)
    {
        IsBusy = true;
        Progress = 0;
        SetLocalizedStatus(status);
        Notify();
    }

    private void EndBusy()
    {
        IsBusy = false;
        if (Progress >= 1)
        {
            Progress = 0;
        }

        Notify();
    }

    private void Notify() => Changed?.Invoke();

    private void CompleteCandidate(TransactionOutcome outcome, string statusKey, params object[] statusArgs)
    {
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? localizer.T("Status.CannotShift");
            Notify();
            return;
        }

        var status = localizer.Format(statusKey, statusArgs);
        AddLog("Info", status);
        RefreshDisplay(updateStatus: true, statusKey: statusKey, statusArgs: statusArgs);
    }

    private void SetLocalizedStatus(string key, params object[] args)
    {
        statusLocalizationKey = key;
        statusLocalizationArgs = args;
        StatusText = localizer.Format(key, args);
    }

    private void SetRawStatus(string message)
    {
        statusLocalizationKey = null;
        statusLocalizationArgs = [];
        StatusText = message;
    }

    private void OnCultureChanged()
    {
        if (ClipSessionState is not null)
        {
            ClipOptions = importResult is { Groups.Count: > 1 } && !ClipSessionState.IsCombined
                ? WasmWorkspaceProjection.BuildClipOptions(importResult, localizer)
                : WasmWorkspaceProjection.BuildClipOptionsFromSession(ClipSessionState, activeGroupIndex, localizer);
        }

        if (statusLocalizationKey is not null)
        {
            StatusText = localizer.Format(statusLocalizationKey, statusLocalizationArgs);
        }

        Notify();
    }

    private void RecordDiagnostics(IEnumerable<ChapterDiagnostic> diagnosticsValue)
    {
        this.diagnostics = WasmWorkspaceProjection.ToDiagnostics(diagnosticsValue);
        foreach (var diagnostic in this.diagnostics)
        {
            AddLog(diagnostic.Severity, $"{diagnostic.Code}: {diagnostic.Message}", diagnostic.Details);
        }
    }

    private void AddLog(string level, string message, string? details = null)
    {
        logs.Add(new WasmLogEntry(DateTimeOffset.Now, level, message, details));
        if (logs.Count > 200)
        {
            logs.RemoveRange(0, logs.Count - 200);
        }
    }

}

public sealed record FrameRateChoice(int Index, string DisplayName, FrameRateOption Option);

internal sealed record DiagnosticView(
    string Severity,
    string Code,
    string Message,
    string? Details);
