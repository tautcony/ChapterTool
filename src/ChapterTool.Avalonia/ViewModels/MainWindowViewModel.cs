using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class MainWindowViewModel : ObservableViewModel
{
    private readonly IChapterLoadService loadService;
    private readonly IChapterSaveService saveService;
    private readonly IChapterEditingService editingService;
    private readonly ChapterSegmentService segmentService;
    private readonly IWindowService windowService;
    private readonly IChapterTimeFormatter formatter;
    private readonly IFrameRateService frameRateService;
    private readonly ChapterOutputProjectionService outputProjectionService;
    private readonly IApplicationLogService logService;
    private readonly IAppLocalizer localizer;
    private readonly IShellService? shellService;
    private readonly ISettingsStore<AppSettings>? appSettingsStore;

    private ChapterInfoGroup? currentGroup;
    private ChapterInfo? currentInfo;
    private FrameRateOption selectedFrameRateOption;
    private bool currentInfoBelongsToSelectedClip;
    private string currentPath = string.Empty;
    private string displayPath = string.Empty;
    private int selectedClipIndex;
    private IReadOnlySet<int> selectedRowIndexes = new HashSet<int>();
    private bool roundFrames = true;
    private int selectedFrameRateIndex = -1;
    private bool isAdvancedPanelExpanded;
    private ChapterExportFormat saveFormat = ChapterExportFormat.Txt;
    private string xmlLanguage = "und";
    private string uiLanguage = "";
    private bool autoGenerateNames;
    private bool useTemplateNames;
    private string chapterNameTemplateText = string.Empty;
    private string chapterNameTemplateStatus = string.Empty;
    private int orderShift;
    private bool applyExpression;
    private string expression = "t";
    private string? saveDirectory;
    private string statusText = string.Empty;
    private LocalizedMessage? currentStatusMessage;
    private double progress;

    public MainWindowViewModel(
        IChapterLoadService loadService,
        IChapterSaveService saveService,
        IChapterEditingService editingService,
        ChapterSegmentService segmentService,
        IWindowService windowService,
        IChapterTimeFormatter formatter,
        IApplicationLogService? logService = null,
        IShellService? shellService = null,
        ISettingsStore<AppSettings>? appSettingsStore = null,
        IFrameRateService? frameRateService = null,
        IAppLocalizer? localizer = null)
    {
        this.loadService = loadService;
        this.saveService = saveService;
        this.editingService = editingService;
        this.segmentService = segmentService;
        this.windowService = windowService;
        this.formatter = formatter;
        this.frameRateService = frameRateService ?? new FrameRateService();
        outputProjectionService = new ChapterOutputProjectionService(new ExpressionService());
        this.logService = logService ?? new InMemoryApplicationLogService();
        this.localizer = localizer ?? new AppLocalizationManager();
        this.shellService = shellService;
        this.appSettingsStore = appSettingsStore;
        chapterNameTemplateStatus = this.localizer.GetString("Status.TemplateNotSelected");
        statusText = this.localizer.GetString("Status.Ready");
        this.localizer.CultureChanged += (_, _) => RefreshLocalizedState();
        selectedFrameRateOption = this.frameRateService.Options[0];
        ClipOptions.CollectionChanged += OnClipOptionsChanged;
        Rows.CollectionChanged += OnRowsChanged;

        LoadCommand = new UiCommand(async (parameter, token) =>
        {
            if (parameter is string path)
            {
                await LoadPathAsync(path, token);
            }
        });
        ReloadCommand = new UiCommand(async (_, token) => await LoadPathAsync(CurrentPath, token), _ => !string.IsNullOrWhiteSpace(CurrentPath));
        AppendMplsCommand = new UiCommand(async (parameter, token) =>
        {
            if (parameter is string path)
            {
                await AppendMplsAsync(path, token);
            }
        }, parameter => CanAppendMpls && parameter is string path && !string.IsNullOrWhiteSpace(path));
        DropPathLoadCommand = new UiCommand(async (parameter, token) => await LoadPathAsync(parameter?.ToString() ?? string.Empty, token));
        SaveCommand = new UiCommand(async (_, token) => await SaveAsync(null, token), _ => currentInfo is not null);
        SaveDirectoryCommand = new UiCommand(async (parameter, token) => await SaveAsync(parameter?.ToString() ?? SaveDirectory, token), _ => currentInfo is not null);
        RefreshCommand = new UiCommand((_, _) =>
        {
            ApplyFrameInfo();
            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
        SelectClipCommand = new UiCommand((parameter, _) =>
        {
            SelectClip(Convert.ToInt32(parameter));
            return ValueTask.CompletedTask;
        }, parameter => parameter is int index && index >= 0 && index < ClipOptions.Count);
        CombineCommand = new UiCommand((_, _) =>
        {
            CombineSegments();
            return ValueTask.CompletedTask;
        }, _ => CanCombine);
        EditTimeCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Time));
        EditNameCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Name));
        EditFrameCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Frame));
        DeleteCommand = new UiCommand(parameter =>
        {
            if (currentInfo is not null && parameter is IReadOnlySet<int> indexes)
            {
                ApplyEdit(editingService.Delete(currentInfo, indexes), $"Delete rows: indexes={string.Join(",", indexes.Order())}");
            }

            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
        InsertCommand = new UiCommand(parameter =>
        {
            if (currentInfo is not null)
            {
                var index = parameter is int value ? value : Rows.Count;
                ApplyEdit(editingService.InsertBefore(currentInfo, index), $"Insert row: index={index}");
            }

            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);

        PreviewCommand = WindowCommand("preview");
        LogCommand = WindowCommand("log");
        ColorSettingsCommand = WindowCommand("color-settings");
        LanguageCommand = WindowCommand("language");
        ExpressionCommand = WindowCommand("expression");
        TemplateNamesCommand = WindowCommand("template-names");
        FileAssociationCommand = WindowCommand("file-association");
        ZonesCommand = WindowCommand("zones");
        ForwardShiftCommand = WindowCommand("forward-shift");
        OpenRelatedMediaCommand = new UiCommand(async (parameter, token) => await OpenRelatedMediaAsync(parameter, token), _ => RelatedMediaReferences.Count > 0);
    }

    public string CurrentPath
    {
        get => currentPath;
        private set => SetProperty(ref currentPath, value);
    }

    public string DisplayPath
    {
        get => displayPath;
        private set => SetProperty(ref displayPath, value);
    }

    public ObservableCollection<ChapterRowViewModel> Rows { get; } = [];

    public ObservableCollection<ChapterSourceOption> ClipOptions { get; } = [];

    public int SelectedClipIndex
    {
        get => selectedClipIndex;
        set
        {
            if (SetProperty(ref selectedClipIndex, value))
            {
                OnPropertyChanged(nameof(RelatedMediaReferences));
            }
        }
    }

    public IReadOnlySet<int> SelectedRowIndexes
    {
        get => selectedRowIndexes;
        private set => SetProperty(ref selectedRowIndexes, value);
    }

    public bool RoundFrames
    {
        get => roundFrames;
        set => SetProperty(ref roundFrames, value);
    }

    public int SelectedFrameRateIndex
    {
        get => selectedFrameRateIndex;
        private set => SetProperty(ref selectedFrameRateIndex, value);
    }

    public bool IsClipSelectionVisible => ClipOptions.Count > 1;

    public bool IsAdvancedPanelExpanded
    {
        get => isAdvancedPanelExpanded;
        set => SetProperty(ref isAdvancedPanelExpanded, value);
    }

    public ChapterExportFormat SaveFormat
    {
        get => saveFormat;
        set
        {
            if (SetProperty(ref saveFormat, value))
            {
                OnPropertyChanged(nameof(SaveFormatIndex));
                OnPropertyChanged(nameof(IsXmlLanguageEnabled));
            }
        }
    }

    public int SaveFormatIndex
    {
        get => (int)SaveFormat;
        set => SaveFormat = (ChapterExportFormat)Math.Max(0, value);
    }

    public string XmlLanguage
    {
        get => xmlLanguage;
        set => SetProperty(ref xmlLanguage, value);
    }

    public bool IsXmlLanguageEnabled => SaveFormat == ChapterExportFormat.Xml;

    public string UiLanguage
    {
        get => uiLanguage;
        private set => SetProperty(ref uiLanguage, value);
    }

    public IAppLocalizer Localizer => localizer;

    public bool AutoGenerateNames
    {
        get => autoGenerateNames;
        set
        {
            if (SetProperty(ref autoGenerateNames, value))
            {
                if (value && useTemplateNames)
                {
                    useTemplateNames = false;
                    OnPropertyChanged(nameof(UseTemplateNames));
                }

                OnPropertyChanged(nameof(ChapterNameModeIndex));
                RefreshRows();
            }
        }
    }

    public bool UseTemplateNames
    {
        get => useTemplateNames;
        set
        {
            if (SetProperty(ref useTemplateNames, value))
            {
                if (value && autoGenerateNames)
                {
                    autoGenerateNames = false;
                    OnPropertyChanged(nameof(AutoGenerateNames));
                }

                OnPropertyChanged(nameof(ChapterNameModeIndex));
                RefreshRows();
            }
        }
    }

    public string ChapterNameTemplateText
    {
        get => chapterNameTemplateText;
        set
        {
            if (SetProperty(ref chapterNameTemplateText, value))
            {
                OnPropertyChanged(nameof(ChapterNameModeIndex));
                RefreshRows();
            }
        }
    }

    public string ChapterNameTemplateStatus
    {
        get => chapterNameTemplateStatus;
        set => SetProperty(ref chapterNameTemplateStatus, value);
    }

    public int ChapterNameModeIndex
    {
        get
        {
            if (UseTemplateNames && !string.IsNullOrWhiteSpace(ChapterNameTemplateText))
            {
                return 2;
            }

            if (UseTemplateNames)
            {
                return 1;
            }

            return 0;
        }
        set
        {
            AutoGenerateNames = false;
            UseTemplateNames = value is 1 or 2;
            if (value != 2)
            {
                ChapterNameTemplateText = string.Empty;
                ChapterNameTemplateStatus = localizer.GetString("Status.TemplateNotSelected");
            }

            OnPropertyChanged();
        }
    }

    public int OrderShift
    {
        get => orderShift;
        set
        {
            if (SetProperty(ref orderShift, value))
            {
                RefreshRows();
            }
        }
    }

    public bool ApplyExpression
    {
        get => applyExpression;
        set
        {
            if (SetProperty(ref applyExpression, value))
            {
                RefreshRows();
            }
        }
    }

    public string Expression
    {
        get => expression;
        set
        {
            if (SetProperty(ref expression, value))
            {
                RefreshRows();
            }
        }
    }

    public string? SaveDirectory
    {
        get => saveDirectory;
        set => SetProperty(ref saveDirectory, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public double Progress
    {
        get => progress;
        private set => SetProperty(ref progress, value);
    }

    public IReadOnlyList<SourceMediaReference> RelatedMediaReferences =>
        currentGroup is null || SelectedClipIndex < 0 || SelectedClipIndex >= currentGroup.Options.Count
            ? Array.Empty<SourceMediaReference>()
            : currentGroup.Options[SelectedClipIndex].MediaReferences ?? Array.Empty<SourceMediaReference>();

    public bool CanAppendMpls => currentGroup?.Options.Any(static option => option.ChapterInfo.SourceType == "MPLS") == true;

    public bool CanCombine => currentGroup is not null
        && currentGroup.Options.Count > 1
        && currentGroup.Options[0].ChapterInfo.SourceType is "MPLS" or "DVD"
        && currentGroup.Options.All(option => option.ChapterInfo.SourceType == currentGroup.Options[0].ChapterInfo.SourceType);

    public bool CanSave => currentInfo is not null;

    public bool CanRefreshRows => currentInfo is not null;

    public bool CanEditRows => currentInfo is not null;

    public bool CanOpenRelatedMedia => RelatedMediaReferences.Count > 0;

    public UiCommand LoadCommand { get; }
    public UiCommand ReloadCommand { get; }
    public UiCommand AppendMplsCommand { get; }
    public UiCommand DropPathLoadCommand { get; }
    public UiCommand SaveCommand { get; }
    public UiCommand SaveDirectoryCommand { get; }
    public UiCommand RefreshCommand { get; }
    public UiCommand SelectClipCommand { get; }
    public UiCommand CombineCommand { get; }
    public UiCommand EditTimeCommand { get; }
    public UiCommand EditNameCommand { get; }
    public UiCommand EditFrameCommand { get; }
    public UiCommand DeleteCommand { get; }
    public UiCommand InsertCommand { get; }
    public UiCommand PreviewCommand { get; }
    public UiCommand LogCommand { get; }
    public UiCommand ColorSettingsCommand { get; }
    public UiCommand LanguageCommand { get; }
    public UiCommand ExpressionCommand { get; }
    public UiCommand TemplateNamesCommand { get; }
    public UiCommand FileAssociationCommand { get; }
    public UiCommand ZonesCommand { get; }
    public UiCommand ForwardShiftCommand { get; }
    public UiCommand OpenRelatedMediaCommand { get; }

    public void SetFrameOptions(int frameRateIndex, bool roundFrames)
    {
        RoundFrames = roundFrames;
        var option = FrameRateOptionForComboIndex(frameRateIndex);
        if (option is not null)
        {
            selectedFrameRateOption = option;
            SelectedFrameRateIndex = frameRateIndex;
            return;
        }

        selectedFrameRateOption = currentInfo is null
            ? frameRateService.Options[0]
            : frameRateService.FindByValue((decimal)currentInfo.FramesPerSecond);
        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
    }

    public async ValueTask LoadSettingsAsync(CancellationToken cancellationToken)
    {
        if (appSettingsStore is null)
        {
            return;
        }

        var settings = await appSettingsStore.LoadAsync(cancellationToken);
        SaveDirectory = settings.SavingPath;
        UiLanguage = AppLanguage.Normalize(settings.Language);
        localizer.SetCulture(UiLanguage);
        Log("Log.SettingsLoaded",
            ("savingPath", SaveDirectory ?? string.Empty),
            ("language", UiLanguage));
        NotifyStateChanged();
    }

    public async ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
    {
        UiLanguage = AppLanguage.Normalize(language);
        localizer.SetCulture(UiLanguage);
        if (appSettingsStore is null)
        {
            return;
        }

        var current = await appSettingsStore.LoadAsync(cancellationToken);
        await appSettingsStore.SaveAsync(current with { Language = UiLanguage }, cancellationToken);
        Log("Log.LanguageSet", ("language", UiLanguage));
        NotifyStateChanged();
    }

    public string BuildPreview()
    {
        if (currentInfo is null)
        {
            return string.Empty;
        }

        var projection = CurrentOutputProjection();
        var options = CurrentExportOptionsForProjectedInfo();
        var result = new ChapterExportService(formatter, new ExpressionService()).Export(projection.Info, options);
        if (!result.Success)
        {
            return string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        return result.Content;
    }

    public string LogText() => logService.Format(FormatLogEntry);

    public void ClearLog() => logService.Clear();

    public void UpdateSelectedRows(IReadOnlySet<int> indexes)
    {
        SelectedRowIndexes = indexes.Where(index => index >= 0).ToHashSet();
        NotifyCommandStates();
    }

    public string CreateZonesText()
    {
        if (currentInfo is null)
        {
            return string.Empty;
        }

        var indexes = SelectedRowIndexes.Count == 0
            ? Enumerable.Range(0, currentInfo.Chapters.Count).ToHashSet()
            : SelectedRowIndexes;
        var result = editingService.CreateZones(currentInfo, indexes, (decimal)currentInfo.FramesPerSecond);
        SetStatus(result.Diagnostics.Count == 0 ? "Status.ZonesGenerated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.CreateZones", ("selectedRows", indexes.Count), ("chapters", currentInfo.Chapters.Count));
        LogDiagnostics("Create zones", result.Diagnostics);
        LogStatus();
        NotifyStateChanged();
        return result.Zones;
    }

    public void ShiftFramesForward(int frames)
    {
        if (currentInfo is null)
        {
            return;
        }

        ApplyEdit(editingService.ShiftFramesForward(currentInfo, frames, (decimal)currentInfo.FramesPerSecond), $"Shift frames forward: frames={frames}");
    }

    public ChapterExportOptions CurrentExportOptions() =>
        new(
            Format: SaveFormat,
            XmlLanguage: XmlLanguage,
            SourceFileName: currentInfo?.SourceName,
            AutoGenerateNames: AutoGenerateNames,
            UseTemplateNames: UseTemplateNames,
            ChapterNameTemplateText: ChapterNameTemplateText,
            OrderShift: OrderShift,
            ApplyExpression: ApplyExpression,
            Expression: Expression);

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status.NoSourceSelected");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.LoadingSource", ("path", path));
        var result = await loadService.LoadAsync(path, cancellationToken);
        LogImportSummary("Load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.LoadFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            Progress = 0;
            LogStatus();
            LogDiagnostics("Load", result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        CurrentPath = path;
        DisplayPath = Path.GetFileName(path);
        currentGroup = result.Groups[0];
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var option in currentGroup.Options)
        {
            ClipOptions.Add(option);
        }

        SelectClip(Math.Clamp(currentGroup.DefaultOptionIndex, 0, ClipOptions.Count - 1));
        SetStatus("Status.LoadedChapters", ("count", Rows.Count));
        Progress = 1;
        Log("Log.StatusFromPath", ("status", StatusText), ("path", path));
        LogDiagnostics("Load", result.Diagnostics);
        NotifyStateChanged();
    }

    private async ValueTask SaveAsync(string? directory, CancellationToken cancellationToken)
    {
        if (currentInfo is null)
        {
            return;
        }

        var projection = CurrentOutputProjection();
        var options = CurrentExportOptionsForProjectedInfo();
        Log("Log.SavingChapters",
            ("format", options.Format),
            ("directory", directory ?? string.Empty),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("chapters", projection.Info.Chapters.Count),
            ("applyExpression", ApplyExpression),
            ("expression", Expression));
        LogDiagnostics("Output projection", projection.Diagnostics);
        var result = await saveService.SaveAsync(projection.Info, options, directory, cancellationToken);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            SaveDirectory = directory;
            if (appSettingsStore is not null)
            {
                var current = await appSettingsStore.LoadAsync(cancellationToken);
                await appSettingsStore.SaveAsync(current with { SavingPath = directory }, cancellationToken);
            }
        }

        SetStatus(result.Success ? "Status.Saved" : "Status.SaveFailed", diagnostic: result.Diagnostics.FirstOrDefault());
        LogStatus();
        LogDiagnostics("Save", result.Diagnostics);
        NotifyStateChanged();
    }

    private void SelectClip(int index)
    {
        if (index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        SelectedClipIndex = index;
        currentInfo = ClipOptions[index].ChapterInfo;
        currentInfoBelongsToSelectedClip = true;
        Log("Log.SelectedSourceOption",
            ("index", index),
            ("label", ClipOptions[index].DisplayName),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("sourceType", currentInfo.SourceType),
            ("chapters", currentInfo.Chapters.Count),
            ("fps", $"{currentInfo.FramesPerSecond:0.###}"));
        selectedFrameRateOption = frameRateService.FindByValue((decimal)currentInfo.FramesPerSecond);
        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        ApplyFrameInfo();
    }

    private ValueTask EditCell(object? parameter, EditKind kind)
    {
        if (currentInfo is null || parameter is not ChapterCellEdit edit)
        {
            return ValueTask.CompletedTask;
        }

        var result = kind switch
        {
            EditKind.Time => editingService.EditTime(currentInfo, edit.Index, edit.Value),
            EditKind.Name => editingService.Rename(currentInfo, edit.Index, edit.Value),
            EditKind.Frame => editingService.EditFrame(currentInfo, edit.Index, edit.Value, (decimal)currentInfo.FramesPerSecond),
            _ => new ChapterEditResult(currentInfo, Array.Empty<Core.Diagnostics.ChapterDiagnostic>())
        };
        ApplyEdit(result, $"Edit {kind}: row={edit.Index}, value='{edit.Value}'");
        return ValueTask.CompletedTask;
    }

    private void CombineSegments()
    {
        if (currentGroup is null)
        {
            return;
        }

        var result = segmentService.Combine(currentGroup);
        ApplyEdit(result, $"Combine segments: options={currentGroup.Options.Count}, sourceType={currentGroup.Options[0].ChapterInfo.SourceType}");
    }

    private async ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken)
    {
        if (currentGroup is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", path));
        var result = await loadService.LoadAsync(path, cancellationToken);
        LogImportSummary("Append load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.AppendFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics("Append load", result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var edit = segmentService.Append(currentGroup, result.Groups[0]);
        if (edit.Diagnostics.Count > 0)
        {
            SetStatus(null, diagnostic: edit.Diagnostics[0]);
            LogStatus();
            LogDiagnostics("Append edit", edit.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var options = currentGroup.Options.ToList();
        options.AddRange(result.Groups[0].Options);
        currentGroup = new ChapterInfoGroup(currentGroup.SourcePath, options, currentGroup.DefaultOptionIndex);
        ClipOptions.Clear();
        foreach (var option in currentGroup.Options)
        {
            ClipOptions.Add(option);
        }

        currentInfo = edit.ChapterInfo;
        currentInfoBelongsToSelectedClip = false;
        ApplyFrameInfo();
        SetStatus("Status.AppendedMplsSegments", ("count", result.Groups[0].Options.Count));
        LogStatus();
        LogDiagnostics("Append load", result.Diagnostics);
        NotifyStateChanged();
    }

    private void ApplyEdit(ChapterEditResult result, string action = "Edit chapters")
    {
        var before = currentInfo?.Chapters.Count ?? 0;
        currentInfo = result.ChapterInfo;
        ApplyFrameInfo();
        SetStatus(result.Diagnostics.Count == 0 ? "Status.Updated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.EditChapters", ("action", action), ("before", before), ("after", currentInfo.Chapters.Count));
        LogDiagnostics(action, result.Diagnostics);
        LogStatus();
        NotifyStateChanged();
    }

    private void ApplyFrameInfo()
    {
        if (currentInfo is null)
        {
            RefreshRows();
            return;
        }

        FrameRateOption appliedOption;
        FrameRateDetectionResult? detection = null;

        if (selectedFrameRateOption.LegacyMplsCode == 0)
        {
            detection = frameRateService.DetectDetailed(currentInfo, tolerance: 0.01m);
            appliedOption = detection.Option;
        }
        else
        {
            appliedOption = selectedFrameRateOption;
        }

        var result = frameRateService.UpdateFrames(currentInfo, appliedOption, RoundFrames, tolerance: 0.01m);
        currentInfo = result.Info;

        if (detection is not null)
        {
            selectedFrameRateOption = frameRateService.Options[0];
            SetStatus("Status.DetectedFrameRate", ("displayName", detection.Option.DisplayName), ("confidence", detection.Confidence));
            Log("Log.AutoFrameRateDetection",
                ("option", detection.Option.DisplayName),
                ("confidence", detection.Confidence),
                ("accurate", detection.AccurateChapterCount),
                ("evaluated", detection.EvaluatedChapterCount),
                ("deviation", $"{detection.CumulativeDeviation:0.######}"));
        }
        else
        {
            selectedFrameRateOption = result.SelectedOption;
        }

        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        Log("Log.FrameInfoUpdated",
            ("option", appliedOption.DisplayName),
            ("fps", $"{result.FramesPerSecond:0.###}"),
            ("round", RoundFrames),
            ("chapters", currentInfo.Chapters.Count));
        if (currentInfoBelongsToSelectedClip)
        {
            UpdateCurrentClipOption(currentInfo);
        }
        RefreshRows();
        NotifyStateChanged();
    }

    private void UpdateCurrentClipOption(ChapterInfo info)
    {
        if (currentGroup is null)
        {
            return;
        }

        var index = SelectedClipIndex;
        if (index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        var options = currentGroup.Options.ToArray();
        if (index >= options.Length)
        {
            return;
        }

        var updatedOption = options[index] with { ChapterInfo = info };
        options[index] = updatedOption;
        ClipOptions[index] = updatedOption;
        currentGroup = currentGroup with { Options = options };

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void RefreshRows()
    {
        Rows.Clear();
        if (currentInfo is null)
        {
            return;
        }

        var projection = CurrentOutputProjection();
        foreach (var chapter in projection.OutputChapters)
        {
            Rows.Add(new ChapterRowViewModel(chapter, formatter));
        }
    }

    private ChapterOutputProjectionResult CurrentOutputProjection() =>
        currentInfo is null
            ? new ChapterOutputProjectionResult(
                new ChapterInfo(string.Empty, null, 0, string.Empty, 0, TimeSpan.Zero, Array.Empty<Chapter>()),
                Array.Empty<ChapterDiagnostic>())
            : outputProjectionService.Project(currentInfo, CurrentExportOptions());

    private ChapterExportOptions CurrentExportOptionsForProjectedInfo() =>
        CurrentExportOptions() with
        {
            ApplyExpression = false,
            AutoGenerateNames = false,
            UseTemplateNames = false,
            ChapterNameTemplateText = string.Empty,
            OrderShift = 0,
            ProjectOutput = false
        };

    private void OnClipOptionsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsClipSelectionVisible));
        OnPropertyChanged(nameof(RelatedMediaReferences));
        NotifyCommandStates();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        NotifyCommandStates();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsClipSelectionVisible));
        OnPropertyChanged(nameof(RelatedMediaReferences));
        OnPropertyChanged(nameof(CanAppendMpls));
        OnPropertyChanged(nameof(CanCombine));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRefreshRows));
        OnPropertyChanged(nameof(CanEditRows));
        OnPropertyChanged(nameof(CanOpenRelatedMedia));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        ReloadCommand.RaiseCanExecuteChanged();
        AppendMplsCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        SaveDirectoryCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        SelectClipCommand.RaiseCanExecuteChanged();
        CombineCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        InsertCommand.RaiseCanExecuteChanged();
        OpenRelatedMediaCommand.RaiseCanExecuteChanged();
        PreviewCommand.RaiseCanExecuteChanged();
        LogCommand.RaiseCanExecuteChanged();
        ColorSettingsCommand.RaiseCanExecuteChanged();
        LanguageCommand.RaiseCanExecuteChanged();
        ExpressionCommand.RaiseCanExecuteChanged();
        TemplateNamesCommand.RaiseCanExecuteChanged();
        FileAssociationCommand.RaiseCanExecuteChanged();
        ZonesCommand.RaiseCanExecuteChanged();
        ForwardShiftCommand.RaiseCanExecuteChanged();
    }

    private static int ComboIndexFor(FrameRateOption option)
    {
        if (option.LegacyMplsCode == 0)
        {
            return 0;
        }

        return option.IsValid ? option.LegacyMplsCode : -1;
    }

    private FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
    {
        if (frameRateIndex == 0)
        {
            return frameRateService.Options[0];
        }

        if (frameRateIndex < 1)
        {
            return null;
        }

        var legacyCode = frameRateIndex;
        if (legacyCode == 5)
        {
            return null;
        }

        return frameRateService.Options.FirstOrDefault(option => option.LegacyMplsCode == legacyCode);
    }

    private UiCommand WindowCommand(string id) =>
        new(async (_, token) => await windowService.ShowAsync(id, this, token));

    private async ValueTask OpenRelatedMediaAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (shellService is null)
        {
            SetStatus("Status.ShellUnavailable");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        var reference = parameter as SourceMediaReference ?? RelatedMediaReferences.FirstOrDefault();
        var target = reference?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(target) && reference is not null && !string.IsNullOrWhiteSpace(CurrentPath))
        {
            var baseDirectory = Directory.Exists(CurrentPath) ? CurrentPath : Path.GetDirectoryName(CurrentPath);
            target = baseDirectory is null ? reference.RelativePath : Path.GetFullPath(Path.Combine(baseDirectory, reference.RelativePath));
        }

        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            SetStatus("Status.RelatedMediaNotFound");
            Log("Log.RelatedMediaNotFound",
                ("status", StatusText),
                ("reference", reference?.RelativePath ?? string.Empty),
                ("resolved", target ?? string.Empty));
            NotifyStateChanged();
            return;
        }

        await shellService.OpenAsync(target, cancellationToken);
        SetStatus("Status.OpenedFile", ("fileName", Path.GetFileName(target)));
        Log("Log.OpenedPath", ("status", StatusText), ("path", target));
        NotifyStateChanged();
    }

    private void SetStatus(string? key, params (string Name, object? Value)[] arguments)
    {
        currentStatusMessage = key is null
            ? null
            : new LocalizedMessage(
                key,
                arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
        StatusText = currentStatusMessage is null ? string.Empty : localizer.Format(currentStatusMessage);
    }

    private void SetStatus(string? key, ChapterDiagnostic? diagnostic, params (string Name, object? Value)[] arguments)
    {
        if (diagnostic is not null)
        {
            currentStatusMessage = null;
            StatusText = LocalizeDiagnostic(diagnostic);
            return;
        }

        SetStatus(key, arguments);
    }

    private string LocalizeDiagnostic(ChapterDiagnostic diagnostic)
    {
        var diagnosticKey = $"Diagnostic.{diagnostic.Code}";
        return localizer.TryGetString(diagnosticKey, out _)
            ? localizer.Format(diagnosticKey)
            : diagnostic.Message;
    }

    private void LogStatus() => Log("Log.Status", ("status", StatusText));

    private void Log(string message) => logService.Add(message);

    private void Log(string key, params (string Name, object? Value)[] arguments) =>
        logService.Add(
            key,
            arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));

    private string FormatLogEntry(ApplicationLogEntry entry)
    {
        if (entry.MessageKey is null)
        {
            return entry.Message;
        }

        var message = localizer.Format(entry.MessageKey, entry.Arguments);
        return string.IsNullOrWhiteSpace(entry.TechnicalDetail)
            ? message
            : $"{message} {entry.TechnicalDetail}";
    }

    private void RefreshLocalizedState()
    {
        if (string.IsNullOrEmpty(chapterNameTemplateText))
        {
            ChapterNameTemplateStatus = localizer.GetString("Status.TemplateNotSelected");
        }

        if (currentStatusMessage is not null)
        {
            StatusText = localizer.Format(currentStatusMessage);
        }
    }

    private void LogImportSummary(string operation, ChapterImportResult result)
    {
        var optionCount = result.Groups.Sum(static group => group.Options.Count);
        var chapterCount = result.Groups
            .SelectMany(static group => group.Options)
            .Sum(static option => option.ChapterInfo.Chapters.Count);
        Log("Log.ImportSummary",
            ("operation", operation),
            ("success", result.Success),
            ("partial", result.IsPartial),
            ("groups", result.Groups.Count),
            ("options", optionCount),
            ("chapters", chapterCount),
            ("diagnostics", result.Diagnostics.Count));
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            Log("Log.ImportGroup",
                ("operation", operation),
                ("groupIndex", groupIndex + 1),
                ("sourcePath", group.SourcePath),
                ("defaultOptionIndex", group.DefaultOptionIndex),
                ("options", group.Options.Count));
            for (var optionIndex = 0; optionIndex < group.Options.Count; optionIndex++)
            {
                var option = group.Options[optionIndex];
                var info = option.ChapterInfo;
                Log("Log.ImportOption",
                    ("operation", operation),
                    ("optionIndex", optionIndex + 1),
                    ("id", option.Id),
                    ("label", option.DisplayName),
                    ("source", info.SourceName ?? string.Empty),
                    ("sourceType", info.SourceType),
                    ("chapters", info.Chapters.Count),
                    ("duration", formatter.Format(info.Duration)),
                    ("fps", $"{info.FramesPerSecond:0.###}"));
            }
        }
    }

    private void LogDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var location = string.IsNullOrWhiteSpace(diagnostic.Location) ? string.Empty : $" location='{diagnostic.Location}'";
            var details = string.IsNullOrWhiteSpace(diagnostic.Details) ? string.Empty : $" details='{diagnostic.Details}'";
            Log("Log.Diagnostic",
                ("operation", operation),
                ("severity", diagnostic.Severity),
                ("code", diagnostic.Code),
                ("location", location),
                ("message", diagnostic.Message),
                ("details", details));
        }
    }

    private enum EditKind
    {
        Time,
        Name,
        Frame
    }
}

public sealed record ChapterCellEdit(int Index, string Value);
