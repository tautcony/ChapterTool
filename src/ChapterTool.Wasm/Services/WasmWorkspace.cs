using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Wasm.Services;

/// <summary>
/// Browser-side workspace that mirrors Avalonia main-window load / grid / frames / expression / save flow.
/// </summary>
public sealed class WasmWorkspace : IDisposable
{
    public const long MaxLoadBytes = 64 * 1024 * 1024;

    private const decimal DefaultFrameAccuracyTolerance = 0.15m;

    private readonly WasmChapterService wasmChapterService;
    private readonly FrameRateService frameRateService = new();
    private readonly ChapterOutputProjectionService projectionService = new();
    private readonly ChapterEditingService editingService;
    private readonly WasmLocalizer localizer;
    private readonly List<WasmLogEntry> logs = [];
    private readonly HashSet<int> selectedRowIndexes = [];

    private ChapterImportResult? importResult;
    private ChapterSet? baseChapterSet;
    private ChapterSet? combinedChapterSet;
    private IReadOnlyList<ClipOption>? splitClipOptions;
    private string? splitSelectedClipId;
    private int activeGroupIndex;
    private bool isClipCombined;
    private List<ChapterRowModel> rows = [];
    private int selectedFrameRateIndex;
    private int selectionAnchor = -1;
    private LoadedSourceSnapshot? lastLoadedSource;
    private string chapterNameTemplateText = string.Empty;
    private string chapterNameTemplateStatus;
    private string? statusLocalizationKey;
    private object[] statusLocalizationArgs = [];

    public WasmWorkspace(WasmChapterService wasmChapterService, WasmLocalizer? localizer = null)
    {
        this.wasmChapterService = wasmChapterService;
        this.localizer = localizer ?? new WasmLocalizer();
        this.localizer.CultureChanged += OnCultureChanged;
        editingService = new ChapterEditingService(wasmChapterService.TimeFormatter);
        SaveFormatIndex = 0;
        ChapterNameModeIndex = 0;
        XmlLanguage = wasmChapterService.XmlLanguages.Contains("und", StringComparer.OrdinalIgnoreCase)
            ? "und"
            : wasmChapterService.XmlLanguages.FirstOrDefault() ?? "und";
        Expression = "t";
        RoundFrames = true;
        TextEncoding = OutputTextEncoding.Utf8;
        EmitBom = true;
        FrameAccuracyTolerance = DefaultFrameAccuracyTolerance;
        selectedFrameRateIndex = 0;
        SetLocalizedStatus("Status.Ready");
        chapterNameTemplateStatus = this.localizer.T("Status.TemplateNotSelected");
    }

    public string SourcePath { get; private set; } = string.Empty;

    public string StatusText { get; private set; } = string.Empty;

    public double Progress { get; private set; }

    public bool IsBusy { get; private set; }

    public bool CanSave => baseChapterSet is not null && rows.Count > 0 && !IsBusy;

    public bool CanPreview => CanSave;

    public bool CanRefreshRows => baseChapterSet is not null && !IsBusy;

    public bool CanReload => lastLoadedSource is not null && !IsBusy;

    public bool IsChapterGridEmpty => rows.Count == 0;

    public IReadOnlyList<ChapterRowModel> Rows => rows;

    public IReadOnlyList<ClipOption> ClipOptions { get; private set; } = [];

    public string? SelectedClipId { get; private set; }

    public bool IsClipSelectionVisible => ClipOptions.Count > 1 || isClipCombined;

    public bool IsClipCombined => isClipCombined;

    public bool CanToggleClipCombine
    {
        get
        {
            if (isClipCombined)
            {
                return true;
            }

            if (importResult is null || string.IsNullOrWhiteSpace(SelectedClipId))
            {
                return false;
            }

            var clip = ClipOptions.FirstOrDefault(option => option.Id == SelectedClipId);
            if (clip is null || clip.EntryIndex < 0 || clip.GroupIndex < 0 || clip.GroupIndex >= importResult.Groups.Count)
            {
                return false;
            }

            var group = importResult.Groups[clip.GroupIndex];
            return group.Entries.Count > 1
                && group.Entries.All(entry => entry.ChapterSet.ImportFormat == group.Entries[0].ChapterSet.ImportFormat)
                && group.Entries[0].ChapterSet.ImportFormat is ChapterImportFormat.Mpls or ChapterImportFormat.DvdIfo;
        }
    }

    public bool CanAppendMpls
    {
        get
        {
            if (IsBusy || importResult is null)
            {
                return false;
            }

            var group = ResolveActiveGroup();
            return group is not null
                && group.Entries.Any(static entry => entry.ChapterSet.ImportFormat == ChapterImportFormat.Mpls);
        }
    }

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

    public bool ApplyExpression { get; set; }

    public string Expression { get; set; }

    public bool RoundFrames { get; set; }

    public OutputTextEncoding TextEncoding { get; set; }

    public bool EmitBom { get; set; }

    public decimal FrameAccuracyTolerance
    {
        get;
        set => field = value <= 0
            ? DefaultFrameAccuracyTolerance
            : Math.Clamp(value, 0.01m, 0.30m);
    }

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

    public string FramesPerSecondDisplay =>
        FramesPerSecond > 0
            ? FramesPerSecond.ToString("0.######")
            : "—";

    public bool IsXmlLanguageEnabled =>
        wasmChapterService.FormatAt(SaveFormatIndex) == ChapterExportFormat.Xml;

    public IReadOnlyList<DiagnosticView> Diagnostics { get; private set; } = [];

    public IReadOnlyList<WasmLogEntry> Logs => logs;

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public int SelectedRowIndex { get; private set; } = -1;

    public IReadOnlyCollection<int> SelectedRowIndexes => selectedRowIndexes;

    public bool HasRowSelection => selectedRowIndexes.Count > 0;

    public int PreferredFrameRateIndex { get; set; }

    public IReadOnlyList<RelatedMediaItem> RelatedMediaReferences
    {
        get
        {
            if (importResult is null || isClipCombined)
            {
                // Combined view may still expose media from the original group entries.
                var group = ResolveActiveGroup();
                if (group is null)
                {
                    return [];
                }

                return group.Entries
                    .SelectMany(static entry => entry.ReferencedMediaFiles ?? [])
                    .Select(static media => new RelatedMediaItem(media.DisplayName, media.RelativePath, media.AbsolutePath))
                    .DistinctBy(static item => item.DisplayName + "|" + item.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var clip = ClipOptions.FirstOrDefault(option => option.Id == SelectedClipId);
            if (clip is null || clip.EntryIndex < 0 || clip.GroupIndex < 0 || clip.GroupIndex >= importResult.Groups.Count)
            {
                return [];
            }

            var entry = importResult.Groups[clip.GroupIndex].Entries[clip.EntryIndex];
            return (entry.ReferencedMediaFiles ?? [])
                .Select(static media => new RelatedMediaItem(media.DisplayName, media.RelativePath, media.AbsolutePath))
                .ToArray();
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

    public void Dispose()
    {
        localizer.CultureChanged -= OnCultureChanged;
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
        if (baseChapterSet is null || IsBusy)
        {
            return;
        }

        var result = editingService.InsertBefore(baseChapterSet, Math.Clamp(index, 0, baseChapterSet.Chapters.Count));
        SelectRow(Math.Clamp(index, 0, Math.Max(0, rows.Count)));
        ApplyEditResult(result, "Status.Inserted");
    }

    public void DuplicateRow(int index)
    {
        if (baseChapterSet is null || index < 0 || index >= baseChapterSet.Chapters.Count || IsBusy)
        {
            return;
        }

        var source = baseChapterSet.Chapters[index];
        var result = editingService.InsertBefore(baseChapterSet, index + 1);
        var chapters = result.ChapterSet.Chapters.ToList();
        chapters[index + 1] = source with { DisplayNumber = 0 };
        SetBaseChapterSet(result.ChapterSet with { Chapters = chapters });
        SelectRow(index + 1);
        AddLog("Info", localizer.Format("Status.Duplicated", index + 1));
        RefreshDisplay(updateStatus: true, statusKey: "Status.Duplicated", statusArgs: [index + 1]);
    }

    public void DeleteSelectedRows()
    {
        if (baseChapterSet is null || IsBusy)
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

        var result = editingService.Delete(baseChapterSet, indexes);
        selectedRowIndexes.Clear();
        SelectedRowIndex = -1;
        selectionAnchor = -1;
        AddLog("Info", localizer.Format("Status.Deleted", indexes.Count));
        ApplyEditResult(result, "Status.Deleted", indexes.Count);
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
        if (baseChapterSet is null || FramesPerSecond <= 0)
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

        var result = editingService.CreateZones(baseChapterSet, indexes, (decimal)FramesPerSecond);
        RecordDiagnostics(result.Diagnostics);
        if (result.Zones.Length > 0)
        {
            SetLocalizedStatus("Status.ZonesGenerated");
            AddLog("Info", $"Generated zones for {indexes.Count} row(s).", result.Zones);
            Notify();
        }
        else
        {
            StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.ZonesEmpty");
            Notify();
        }

        return result.Zones;
    }

    public string CreateZones(int index)
    {
        EnsureRowSelected(index);
        return CreateZonesForSelection();
    }

    public void ShiftFramesForward(int frames)
    {
        if (baseChapterSet is null || IsBusy)
        {
            return;
        }

        if (frames <= 0)
        {
            SetLocalizedStatus("Status.CannotShift");
            Notify();
            return;
        }

        var fps = FramesPerSecond > 0 ? (decimal)FramesPerSecond : (decimal)baseChapterSet.FramesPerSecond;
        var result = editingService.ShiftFramesForward(baseChapterSet, frames, fps);
        if (result.Diagnostics.Count > 0 && result.ChapterSet.Chapters.Count == baseChapterSet.Chapters.Count
            && result.Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error))
        {
            RecordDiagnostics(result.Diagnostics);
            StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.CannotShift");
            Notify();
            return;
        }

        ApplyEditResult(result, "Status.Shifted", frames);
    }

    public async Task LoadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
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

            if (content.LongLength > MaxLoadBytes)
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
                ClearSession();
                Diagnostics = ToDiagnostics(result.Diagnostics);
                StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.LoadFailed");
                AddLog("Error", StatusText);
                return;
            }

            ApplySuccessfulLoad(fileName, content, result);
            Progress = 1;
            AddLog("Info", StatusText);
        }
        catch (Exception ex)
        {
            ClearSession();
            StatusText = ex.Message;
            Diagnostics = [];
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
        if (!CanAppendMpls)
        {
            SetLocalizedStatus("Status.CannotAppend");
            AddLog("Warning", StatusText);
            Notify();
            return;
        }

        var existingGroup = ResolveActiveGroup();
        if (existingGroup is null)
        {
            SetLocalizedStatus("Status.CannotAppend");
            AddLog("Warning", StatusText);
            Notify();
            return;
        }

        BeginBusy("Status.Appending");
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

            var result = await wasmChapterService.ImportAsync(fileName, content, cancellationToken);
            Progress = 0.7;
            Notify();

            if (!result.Success || result.Groups.Count == 0)
            {
                RecordDiagnostics(result.Diagnostics);
                StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.AppendFailed");
                AddLog("Error", StatusText);
                return;
            }

            var appendedGroup = result.Groups[0];
            var edit = ChapterSegmentService.Append(existingGroup, appendedGroup);
            if (edit.Diagnostics.Count > 0)
            {
                // Keep current session on append failure.
                RecordDiagnostics(edit.Diagnostics);
                StatusText = FirstError(edit.Diagnostics) ?? localizer.T("Status.AppendFailed");
                AddLog("Error", StatusText);
                return;
            }

            var mergedEntries = existingGroup.Entries.Concat(appendedGroup.Entries).ToList();
            var mergedGroup = existingGroup with { Entries = mergedEntries };
            if (importResult is not null)
            {
                var groups = importResult.Groups.ToList();
                groups[activeGroupIndex] = mergedGroup;
                importResult = new ChapterImportResult(true, groups, result.Diagnostics);
            }

            splitClipOptions = null;
            splitSelectedClipId = null;
            isClipCombined = true;
            combinedChapterSet = edit.ChapterSet;
            SelectedClipId = $"combined:{activeGroupIndex}";
            ClipOptions = [new ClipOption(SelectedClipId, $"{mergedEntries[0].DisplayName} (Combined)", activeGroupIndex, -1)];
            SetBaseChapterSet(combinedChapterSet);
            ClearSelection();
            RebuildFrameRateChoices(combinedChapterSet);
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
        if (isClipCombined)
        {
            return;
        }

        if (string.Equals(SelectedClipId, clipId, StringComparison.Ordinal))
        {
            return;
        }

        SelectedClipId = clipId;
        LoadBaseFromSelectedClip();
        ClearSelection();
        RefreshDisplay(
            updateStatus: true,
            statusKey: "Status.SelectedClip",
            statusArgs: [ClipOptions.FirstOrDefault(c => c.Id == clipId)?.DisplayText ?? clipId ?? string.Empty]);
    }

    public void ToggleClipCombine()
    {
        if (IsBusy || importResult is null || !CanToggleClipCombine)
        {
            return;
        }

        if (isClipCombined)
        {
            isClipCombined = false;
            combinedChapterSet = null;
            ClipOptions = splitClipOptions ?? [];
            SelectedClipId = splitSelectedClipId ?? ClipOptions.FirstOrDefault()?.Id;
            splitClipOptions = null;
            splitSelectedClipId = null;
            LoadBaseFromSelectedClip();
            ClearSelection();
            AddLog("Info", localizer.T("Status.RestoredClips"));
            RefreshDisplay(updateStatus: true, statusKey: "Status.RestoredClips");
            return;
        }

        var clip = ClipOptions.First(option => option.Id == SelectedClipId);
        var group = importResult.Groups[clip.GroupIndex];
        var result = ChapterSegmentService.Combine(group);
        if (result.Diagnostics.Count > 0)
        {
            RecordDiagnostics(result.Diagnostics);
            StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.CombineFailed");
            Notify();
            return;
        }

        splitClipOptions = ClipOptions;
        splitSelectedClipId = SelectedClipId;
        activeGroupIndex = clip.GroupIndex;
        combinedChapterSet = result.ChapterSet;
        isClipCombined = true;
        SelectedClipId = $"combined:{clip.GroupIndex}";
        ClipOptions = [new ClipOption(SelectedClipId, $"{group.Entries[0].DisplayName} (Combined)", clip.GroupIndex, -1)];
        SetBaseChapterSet(combinedChapterSet);
        ClearSelection();
        AddLog("Info", localizer.Format("Status.Combined", group.Entries.Count));
        RebuildFrameRateChoices(combinedChapterSet);
        RefreshDisplay(updateStatus: true, statusKey: "Status.CombinedDone");
    }

    public void ChangeSelectedFrameRate()
    {
        if (!CanChangeSelectedFrameRate || baseChapterSet is null)
        {
            return;
        }

        var sourceFps = (decimal)baseChapterSet.FramesPerSecond;
        var target = ResolveSelectedFrameRateOption();
        var result = ChapterFpsTransformService.ChangeFps(baseChapterSet, sourceFps, target.Value);
        if (!result.Success)
        {
            RecordDiagnostics(result.Diagnostics);
            StatusText = FirstError(result.Diagnostics) ?? localizer.T("Status.ChangeFpsFailed");
            Notify();
            return;
        }

        SetBaseChapterSet(result.Info);
        PreferredFrameRateIndex = selectedFrameRateIndex;
        AddLog("Info", localizer.Format("Status.ChangedFps", sourceFps, target.Value));
        RefreshDisplay(updateStatus: true, statusKey: "Status.ChangedFpsTo", statusArgs: [target.DisplayName]);
    }

    public bool CanChangeSelectedFrameRate =>
        baseChapterSet is not null
        && baseChapterSet.FramesPerSecond > 0
        && selectedFrameRateIndex > 0
        && ResolveSelectedFrameRateOption().IsValid;

    /// <summary>
    /// Applies current option state (round frames, expression, order shift, naming) and refreshes the grid.
    /// </summary>
    public void ApplyOptionsAndRefresh()
    {
        if (baseChapterSet is null)
        {
            Notify();
            return;
        }

        RefreshDisplay(updateStatus: false, statusKey: null);
    }

    public void RefreshRows()
    {
        if (baseChapterSet is null || IsBusy)
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
        RefreshDisplay(updateStatus: true, statusKey: "Status.TemplateLoaded", statusArgs: [ChapterNameTemplateStatus]);
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
        if (!CanPreview || baseChapterSet is null)
        {
            SetLocalizedStatus("Status.NoPreview");
            return new PreviewResult(false, StatusText);
        }

        try
        {
            var framed = ApplyFrames(baseChapterSet);
            SetBaseChapterSet(framed.Info);
            FramesPerSecond = baseChapterSet.FramesPerSecond;

            var format = wasmChapterService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var export = wasmChapterService.Export(baseChapterSet, options);
            Diagnostics = ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = FirstError(export.Diagnostics) ?? localizer.T("Status.PreviewFailed");
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
        if (!CanSave || baseChapterSet is null)
        {
            SetLocalizedStatus("Status.NothingToSave");
            return new SaveResult(false, StatusText);
        }

        try
        {
            // Ensure frames/FPS on the base set are current before export projection.
            var framed = ApplyFrames(baseChapterSet);
            SetBaseChapterSet(framed.Info);
            FramesPerSecond = baseChapterSet.FramesPerSecond;

            var format = wasmChapterService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var export = wasmChapterService.Export(baseChapterSet, options);
            Diagnostics = ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = FirstError(export.Diagnostics) ?? localizer.T("Status.SaveFailed");
                Notify();
                return new SaveResult(false, StatusText);
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
        if (baseChapterSet is null || index < 0 || index >= baseChapterSet.Chapters.Count)
        {
            return;
        }

        var chapters = baseChapterSet.Chapters.ToList();
        var chapter = chapters[index];
        if (chapter.IsSeparator)
        {
            if (name is not null)
            {
                chapters[index] = chapter with { Name = name };
                SetBaseChapterSet(baseChapterSet with { Chapters = chapters });
                RefreshDisplay(updateStatus: false, statusKey: null);
            }

            return;
        }

        if (timeText is not null)
        {
            var start = wasmChapterService.TimeFormatter.ParseOrZero(timeText);
            chapter = chapter with { StartTime = start };
        }

        if (name is not null)
        {
            chapter = chapter with { Name = name };
        }

        chapters[index] = chapter;
        SetBaseChapterSet(baseChapterSet with { Chapters = chapters });
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

    private void ApplySuccessfulLoad(string fileName, byte[] content, ChapterImportResult result)
    {
        lastLoadedSource = new LoadedSourceSnapshot(fileName, content);
        importResult = result;
        isClipCombined = false;
        combinedChapterSet = null;
        splitClipOptions = null;
        splitSelectedClipId = null;
        activeGroupIndex = 0;
        SourcePath = fileName;
        ClipOptions = BuildClipOptions(result);
        SelectedClipId = ClipOptions.FirstOrDefault()?.Id;
        if (ClipOptions.Count > 0)
        {
            activeGroupIndex = ClipOptions[0].GroupIndex;
        }

        selectedFrameRateIndex = Math.Max(0, PreferredFrameRateIndex);
        ClearSelection();
        Diagnostics = [];
        LoadBaseFromSelectedClip();
        RefreshDisplay(
            updateStatus: true,
            statusKey: "Status.Loaded",
            statusArgs: [baseChapterSet?.Chapters.Count ?? 0, Path.GetFileName(fileName)]);
    }

    private void LoadBaseFromSelectedClip()
    {
        if (isClipCombined && combinedChapterSet is not null)
        {
            SetBaseChapterSet(combinedChapterSet);
            RebuildFrameRateChoices(combinedChapterSet);
            return;
        }

        if (importResult is null || ClipOptions.Count == 0)
        {
            ClearSession(keepPath: true, keepReload: true);
            return;
        }

        var clip = ClipOptions.FirstOrDefault(option => option.Id == SelectedClipId) ?? ClipOptions[0];
        SelectedClipId = clip.Id;
        activeGroupIndex = clip.GroupIndex;
        SetBaseChapterSet(importResult.Groups[clip.GroupIndex].Entries[clip.EntryIndex].ChapterSet);
        RebuildFrameRateChoices(baseChapterSet!);
    }

    private ChapterImportSource? ResolveActiveGroup()
    {
        if (importResult is null || importResult.Groups.Count == 0)
        {
            return null;
        }

        if (isClipCombined)
        {
            return importResult.Groups[Math.Clamp(activeGroupIndex, 0, importResult.Groups.Count - 1)];
        }

        var clip = ClipOptions.FirstOrDefault(option => option.Id == SelectedClipId);
        if (clip is null)
        {
            return importResult.Groups[0];
        }

        activeGroupIndex = clip.GroupIndex;
        return importResult.Groups[clip.GroupIndex];
    }

    private void RefreshDisplay(bool updateStatus, string? statusKey, params object[] statusArgs)
    {
        if (baseChapterSet is null)
        {
            rows = [];
            FramesPerSecond = 0;
            if (updateStatus && statusKey is not null)
            {
                SetLocalizedStatus(statusKey, statusArgs);
            }

            Notify();
            return;
        }

        var framed = ApplyFrames(baseChapterSet);
        SetBaseChapterSet(framed.Info);
        FramesPerSecond = baseChapterSet.FramesPerSecond;
        RebuildFrameRateChoices(baseChapterSet);

        var projection = projectionService.Project(baseChapterSet, CreateExportOptions());
        rows = projection.Info.Chapters
            .Select(chapter => ToRow(chapter, wasmChapterService.TimeFormatter))
            .ToList();

        // Drop selection indexes that no longer exist after edits.
        selectedRowIndexes.RemoveWhere(index => index < 0 || index >= rows.Count);
        if (SelectedRowIndex >= rows.Count)
        {
            SelectedRowIndex = selectedRowIndexes.Count > 0 ? selectedRowIndexes.Max() : -1;
        }

        var projectionDiagnostics = ToDiagnostics(projection.Diagnostics);
        Diagnostics = projectionDiagnostics;
        if (projectionDiagnostics.Count > 0)
        {
            AddLog("Warning", $"{projectionDiagnostics.Count} diagnostic(s) reported.", string.Join(Environment.NewLine, projectionDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
        }

        if (updateStatus && statusKey is not null)
        {
            SetLocalizedStatus(statusKey, statusArgs);
        }
        else if (projectionDiagnostics.Count > 0)
        {
            var first = projectionDiagnostics[0];
            SetRawStatus($"{first.Severity}: {first.Message}");
        }
        else if (ApplyExpression)
        {
            SetLocalizedStatus("Status.ExpressionApplied", FramesPerSecondDisplay, rows.Count);
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

        // Auto (LegacyMplsCode == 0): detect when rounding, otherwise still need a valid option for fps.
        return frameRateService.UpdateFrames(info, option, RoundFrames, FrameAccuracyTolerance);
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
            Format: wasmChapterService.FormatAt(SaveFormatIndex),
            XmlLanguage: XmlLanguage,
            SourceFileName: SourcePath,
            AutoGenerateNames: ChapterNameModeIndex == 1,
            UseTemplateNames: ChapterNameModeIndex == 2,
            ChapterNameTemplateText: ChapterNameModeIndex == 2 ? ChapterNameTemplateText : string.Empty,
            OrderShift: OrderShift,
            ApplyExpression: ApplyExpression,
            Expression: string.IsNullOrWhiteSpace(Expression) ? "t" : Expression.Trim(),
            TextEncoding: TextEncoding,
            EmitBom: EmitBom,
            ProjectOutput: true);

    private void ClearSession(bool keepPath = false, bool keepReload = false)
    {
        importResult = null;
        baseChapterSet = null;
        combinedChapterSet = null;
        splitClipOptions = null;
        splitSelectedClipId = null;
        isClipCombined = false;
        activeGroupIndex = 0;
        rows = [];
        ClipOptions = [];
        SelectedClipId = null;
        FramesPerSecond = 0;
        FrameRateChoices = [];
        ClearSelection();
        Diagnostics = [];
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
            return selectedRowIndexes.Where(index => index >= 0 && index < rows.Count).ToHashSet();
        }

        if (SelectedRowIndex >= 0 && SelectedRowIndex < rows.Count)
        {
            return [SelectedRowIndex];
        }

        return [];
    }

    private static List<ClipOption> BuildClipOptions(ChapterImportResult result)
    {
        var options = new List<ClipOption>();
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var id = $"{groupIndex}:{entryIndex}:{entry.Id}";
                var display = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? $"Entry {entryIndex + 1}"
                    : entry.DisplayName;
                if (result.Groups.Count > 1)
                {
                    display = $"{Path.GetFileName(group.SourcePath)} · {display}";
                }

                options.Add(new ClipOption(id, display, groupIndex, entryIndex));
            }
        }

        return options;
    }

    private static ChapterRowModel ToRow(Chapter chapter, IChapterTimeFormatter formatter) =>
        new()
        {
            Number = chapter.DisplayNumber,
            TimeText = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.StartTime),
            Name = chapter.Name,
            FramesInfo = chapter.FramesInfo,
            IsSeparator = chapter.IsSeparator,
            IsFrameAccurate = chapter.FrameAccuracy == FrameAccuracy.Accurate,
            IsFrameInexact = chapter.FrameAccuracy == FrameAccuracy.Inexact
        };

    private static IReadOnlyList<DiagnosticView> ToDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic => new DiagnosticView(
            diagnostic.Severity.ToString(),
            diagnostic.DisplayCode,
            diagnostic.Message,
            diagnostic.Details)).ToArray();

    private static string? FirstError(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.FirstOrDefault(static d => d.Severity == DiagnosticSeverity.Error)?.Message
        ?? diagnostics.FirstOrDefault()?.Message;

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

    private void ApplyEditResult(ChapterEditResult result, string statusKey, params object[] statusArgs)
    {
        SetBaseChapterSet(result.ChapterSet);
        RecordDiagnostics(result.Diagnostics);
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
        if (statusLocalizationKey is not null)
        {
            StatusText = localizer.Format(statusLocalizationKey, statusLocalizationArgs);
            Notify();
        }
    }

    private void RecordDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics)
    {
        Diagnostics = ToDiagnostics(diagnostics);
        foreach (var diagnostic in Diagnostics)
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

    private void SetBaseChapterSet(ChapterSet value)
    {
        baseChapterSet = value;
        if (isClipCombined)
        {
            combinedChapterSet = value;
        }
    }
}

public sealed record FrameRateChoice(int Index, string DisplayName, FrameRateOption Option);

public sealed record DiagnosticView(
    string Severity,
    string Code,
    string Message,
    string? Details);
