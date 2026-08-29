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
public sealed class WasmWorkspace : IDisposable
{
    public const long MaxLoadBytes = PortableInputPolicy.MaxBytes;

    private const decimal DefaultFrameAccuracyTolerance = 0.15m;

    private readonly WasmChapterService wasmChapterService;
    private readonly FrameRateService frameRateService = new();
    private readonly IChapterExpressionEngine expressionEngine;
    private readonly ChapterOutputProjectionService projectionService;
    private readonly ChapterEditingService editingService;
    private readonly ChapterWorkspace session = new();
    private readonly WasmLocalizer localizer;
    private readonly long maxLoadBytes;
    private readonly List<WasmLogEntry> logs = [];
    private readonly HashSet<int> selectedRowIndexes = [];
    private IReadOnlyList<DiagnosticView> diagnostics = [];

    private ChapterImportResult? importResult;
    private int activeGroupIndex;
    private List<ChapterRowModel> rows = [];
    private int selectedFrameRateIndex;
    private int selectionAnchor = -1;
    private LoadedSourceSnapshot? lastLoadedSource;
    private string chapterNameTemplateText = string.Empty;
    private string chapterNameTemplateStatus;
    private string? statusLocalizationKey;
    private object[] statusLocalizationArgs = [];

    private ChapterSet? BaseChapterSet
    {
        get => session.CurrentChapterSet;
        set => session.SetCurrentChapterSet(value);
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
        projectionService = new ChapterOutputProjectionService(expressionEngine);
        editingService = new ChapterEditingService(wasmChapterService.TimeFormatter);
        SaveFormatIndex = 0;
        ChapterNameModeIndex = 0;
        XmlLanguage = wasmChapterService.XmlLanguages.Contains("und", StringComparer.OrdinalIgnoreCase)
            ? "und"
            : wasmChapterService.XmlLanguages.FirstOrDefault() ?? "und";
        Expression = "t";
        ExpressionPresetId = string.Empty;
        RoundFrames = true;
        TextEncoding = OutputTextEncoding.Utf8;
        EmitBom = false;
        FrameAccuracyTolerance = DefaultFrameAccuracyTolerance;
        EditingOptions = ChapterEditingOptions.Default;
        selectedFrameRateIndex = 0;
        SetLocalizedStatus("Status.Ready");
        chapterNameTemplateStatus = this.localizer.T("Status.TemplateNotSelected");
    }

    public string SourcePath { get; private set; } = string.Empty;

    public string StatusText { get; private set; } = string.Empty;

    public double Progress { get; private set; }

    public bool IsBusy { get; private set; }

    public bool CanSave => BaseChapterSet is not null && rows.Count > 0 && !IsBusy;

    public bool CanPreview => CanSave;

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

    public bool ApplyExpression { get; set; }

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

    public ChapterEditingOptions EditingOptions { get; set; } = ChapterEditingOptions.Default;

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
        if (BaseChapterSet is null || IsBusy)
        {
            return;
        }

        var result = editingService.InsertBefore(BaseChapterSet, Math.Clamp(index, 0, BaseChapterSet.Chapters.Count));
        SelectRow(Math.Clamp(index, 0, Math.Max(0, rows.Count)));
        ApplyEditResult(result, "Status.Inserted");
    }

    public void DuplicateRow(int index)
    {
        if (BaseChapterSet is null || index < 0 || index >= BaseChapterSet.Chapters.Count || IsBusy)
        {
            return;
        }

        var source = BaseChapterSet.Chapters[index];
        var result = editingService.InsertBefore(BaseChapterSet, index + 1);
        var chapters = result.ChapterSet.Chapters.ToList();
        chapters[index + 1] = source with { DisplayNumber = 0 };
        SetBaseChapterSet(result.ChapterSet with { Chapters = chapters });
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

        var result = editingService.Delete(BaseChapterSet, indexes, EditingOptions);
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

        var result = editingService.CreateZones(BaseChapterSet, indexes, (decimal)FramesPerSecond);
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

        var fps = FramesPerSecond > 0 ? (decimal)FramesPerSecond : (decimal)BaseChapterSet.FramesPerSecond;
        var result = editingService.ShiftFramesForward(BaseChapterSet, frames, fps);
        if (result.Diagnostics.Count > 0 && result.ChapterSet.Chapters.Count == BaseChapterSet.Chapters.Count
            && result.Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error))
        {
            RecordDiagnostics(result.Diagnostics);
            StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.CannotShift");
            Notify();
            return;
        }

        ApplyEditResult(result, "Status.Shifted", frames);
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
                if (session.IsCurrentRevision(operationRevision))
                {
                    ClearSession();
                }

                diagnostics = WasmWorkspaceProjection.ToDiagnostics(result.Diagnostics);
                StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.LoadFailed");
                AddLog("Error", StatusText);
                return;
            }

            if (!ApplySuccessfulLoad(fileName, content, result, operationRevision))
            {
                return;
            }

            Progress = 1;
            AddLog("Info", StatusText);
        }
        catch (Exception ex)
        {
            if (session.IsCurrentRevision(operationRevision))
            {
                ClearSession();
            }

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
            var transition = ClipSessionTransitions.Append(ClipSessionState, appendedGroup);
            if (!transition.Succeeded || transition.Session is null)
            {
                // Keep current session on append failure.
                RecordDiagnostics(transition.EditResult.Diagnostics);
                StatusText = WasmWorkspaceProjection.FirstError(transition.EditResult.Diagnostics) ?? localizer.T("Status.AppendFailed");
                AddLog("Error", StatusText);
                return;
            }

            if (!session.TryCommitAppend(operationRevision, expectedSessionId, transition.Session))
            {
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
            ClipSessionState = ClipSessionTransitions.FromLoad(importResult.Groups[activeGroupIndex]);
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

        var transition = ClipSessionTransitions.ToggleCombine(ClipSessionState);
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
        var result = ChapterFpsTransformService.ChangeFps(BaseChapterSet, sourceFps, target.Value);
        if (!result.Success)
        {
            RecordDiagnostics(result.Diagnostics);
            StatusText = WasmWorkspaceProjection.FirstError(result.Diagnostics) ?? localizer.T("Status.ChangeFpsFailed");
            Notify();
            return;
        }

        SetBaseChapterSet(result.Info);
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
    /// Applies current option state (round frames, expression, order shift, naming) and refreshes the grid.
    /// </summary>
    public void ApplyOptionsAndRefresh()
    {
        if (BaseChapterSet is null)
        {
            Notify();
            return;
        }

        RefreshDisplay(updateStatus: false, statusKey: null);
    }

    /// <summary>
    /// Applies a built-in Core expression preset and refreshes the projected rows.
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
        ApplyExpression = true;
        AddLog("Info", localizer.Format("Status.ExpressionPresetApplied", preset.DisplayName));
        if (BaseChapterSet is null)
        {
            SetLocalizedStatus("Status.ExpressionPresetApplied", preset.DisplayName);
            Notify();
            return true;
        }

        RefreshDisplay(updateStatus: true, statusKey: "Status.ExpressionPresetApplied", statusArgs: [preset.DisplayName]);
        return true;
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
        if (!CanPreview || BaseChapterSet is null)
        {
            SetLocalizedStatus("Status.NoPreview");
            return new PreviewResult(false, StatusText);
        }

        try
        {
            var framed = ApplyFrames(BaseChapterSet);
            SetBaseChapterSet(framed.Info);
            FramesPerSecond = BaseChapterSet.FramesPerSecond;

            var format = wasmChapterService.FormatAt(SaveFormatIndex);
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
            var framed = ApplyFrames(BaseChapterSet);
            SetBaseChapterSet(framed.Info);
            FramesPerSecond = BaseChapterSet.FramesPerSecond;

            var format = wasmChapterService.FormatAt(SaveFormatIndex);
            var options = CreateExportOptions();
            var export = wasmChapterService.Export(BaseChapterSet, options);
            diagnostics = WasmWorkspaceProjection.ToDiagnostics(export.Diagnostics);
            if (!export.Success)
            {
                StatusText = WasmWorkspaceProjection.FirstError(export.Diagnostics) ?? localizer.T("Status.SaveFailed");
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
        if (BaseChapterSet is null || index < 0 || index >= BaseChapterSet.Chapters.Count)
        {
            return;
        }

        var chapters = BaseChapterSet.Chapters.ToList();
        var chapter = chapters[index];
        if (chapter.IsSeparator)
        {
            if (name is not null)
            {
                chapters[index] = chapter with { Name = name };
                SetBaseChapterSet(BaseChapterSet with { Chapters = chapters });
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
        SetBaseChapterSet(BaseChapterSet with { Chapters = chapters });
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
        int operationRevision)
    {
        var newSession = result.Groups.Count > 0
            ? ClipSessionTransitions.FromLoad(result.Groups[0])
            : null;
        if (newSession is null || !session.TryCommitLoad(operationRevision, fileName, newSession))
        {
            return false;
        }

        lastLoadedSource = new LoadedSourceSnapshot(fileName, content);
        importResult = result;
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
            BaseChapterSet = null;
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

        BaseChapterSet = ClipSessionState.CurrentChapterSet;
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
            if (updateStatus && statusKey is not null)
            {
                SetLocalizedStatus(statusKey, statusArgs);
            }

            Notify();
            return;
        }

        var framed = ApplyFrames(BaseChapterSet);
        SetBaseChapterSet(framed.Info);
        FramesPerSecond = BaseChapterSet.FramesPerSecond;
        RebuildFrameRateChoices(BaseChapterSet);

        var projection = projectionService.Project(BaseChapterSet, CreateExportOptions());
        rows =
        [
            .. projection.Info.Chapters
                .Select(chapter => WasmWorkspaceProjection.ToRow(chapter, wasmChapterService.TimeFormatter))
        ];

        // Drop selection indexes that no longer exist after edits.
        selectedRowIndexes.RemoveWhere(index => index < 0 || index >= rows.Count);
        if (SelectedRowIndex >= rows.Count)
        {
            SelectedRowIndex = selectedRowIndexes.Count > 0 ? selectedRowIndexes.Max() : -1;
        }

        var projectionDiagnostics = WasmWorkspaceProjection.ToDiagnostics(projection.Diagnostics);
        diagnostics = projectionDiagnostics;
        if (projectionDiagnostics.Count > 0)
        {
            AddLog(
                "Warning",
                $"{projectionDiagnostics.Count} diagnostic(s) reported.",
                string.Join(Environment.NewLine, projectionDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
        }

        // Expression failures always surface Core diagnostics to status/log even when a success key was requested.
        if (ApplyExpression && projectionDiagnostics.Count > 0)
        {
            var first = projectionDiagnostics[0];
            SetRawStatus($"{first.Severity}: {first.Message}");
        }
        else if (updateStatus && statusKey is not null)
        {
            SetLocalizedStatus(statusKey, statusArgs);
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
        return frameRateService.UpdateFrames(info, option, RoundFrames ? 0 : EditingOptions.EffectiveFrameDecimalPlaces, FrameAccuracyTolerance);
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
            ExpressionPresetId: ExpressionPresetId,
            ExpressionSourceName: !string.IsNullOrWhiteSpace(ExpressionPresetId)
                ? ExpressionPresets.FirstOrDefault(preset =>
                      string.Equals(preset.Id, ExpressionPresetId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                  ?? ExpressionPresetId
                : string.Empty,
            TextEncoding: TextEncoding,
            EmitBom: EmitBom,
            ProjectOutput: true);

    private void ClearSession(bool keepPath = false, bool keepReload = false)
    {
        importResult = null;
        BaseChapterSet = null;
        ClipSessionState = null;
        activeGroupIndex = 0;
        rows = [];
        ClipOptions = [];
        SelectedClipId = null;
        FramesPerSecond = 0;
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

    private void SetBaseChapterSet(ChapterSet value)
    {
        if (ClipSessionState is not null)
        {
            session.WriteBackCurrentChapterSet(value);
        }
        else
        {
            session.SetCurrentChapterSet(value);
        }
    }
}

public sealed record FrameRateChoice(int Index, string DisplayName, FrameRateOption Option);

internal sealed record DiagnosticView(
    string Severity,
    string Code,
    string Message,
    string? Details);
