using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Session;
using ChapterTool.Avalonia.Session.Ports;
using ChapterTool.Avalonia.Workflows;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels;

/// <summary>Coordinates the main window state, commands, and chapter workflows.</summary>
public sealed partial class MainWindowViewModel : ObservableViewModel
{
    private readonly IChapterLoadService loadService;
    private readonly IChapterSaveService saveService;
    private readonly IChapterEditingService editingService;
    private readonly ChapterSegmentService segmentService;
    private readonly IWindowService windowService;
    private readonly IChapterTimeFormatter formatter;
    private readonly IFrameRateService frameRateService;
    private readonly ChapterExportService exportService;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IShellService? shellService;
    private readonly LoadSaveWorkflow loadSaveWorkflow;
    private readonly ProjectionFacade projectionFacade;
    private readonly StatusDiagnosticsPresenter statusDiagnosticsPresenter;
    private readonly DisplayOptionCoordinator displayOptionCoordinator;
    private readonly ObservableCollection<SelectorDisplayOption> xmlLanguageDisplayOptions = [];

    private FrameRateOption selectedFrameRateOption;
    private decimal? configuredFrameRate;
    private bool isRefreshingChapterNameModeOptions;
    private string chapterNameTemplateStatus;
    private string statusText;
    private string? lastExpressionDiagnosticSignature;

    private ChapterSet? CurrentInfo
    {
        get => Workspace.CurrentChapterSet;
        set => Workspace.SetCurrentChapterSet(value);
    }

    public MainWindowViewModel(
        IChapterLoadService loadService,
        IChapterSaveService saveService,
        IChapterEditingService editingService,
        ChapterSegmentService segmentService,
        IWindowService windowService,
        IChapterTimeFormatter formatter,
        IApplicationLogService logService,
        ILogger<MainWindowViewModel> logger,
        IFrameRateService frameRateService,
        IAppLocalizer localizer,
        IChapterExpressionEngine expressionEngine,
        ChapterExportService exportService,
        IShellService? shellService = null,
        ISettingsStore<ChapterToolSettings>? settingsStore = null,
        IExpressionAuthoringService? expressionAuthoringService = null)
    {
        ArgumentNullException.ThrowIfNull(loadService);
        ArgumentNullException.ThrowIfNull(saveService);
        ArgumentNullException.ThrowIfNull(editingService);
        ArgumentNullException.ThrowIfNull(segmentService);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(logService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(frameRateService);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(expressionEngine);
        ArgumentNullException.ThrowIfNull(exportService);

        this.loadService = loadService;
        this.saveService = saveService;
        this.editingService = editingService;
        this.segmentService = segmentService;
        this.windowService = windowService;
        this.formatter = formatter;
        this.frameRateService = frameRateService;
        this.ExpressionEngine = expressionEngine;
        this.exportService = exportService;
        this.LogService = logService;
        this.logger = logger;
        Localizer = localizer;
        this.shellService = shellService;
        this.SettingsStore = settingsStore;
        ExpressionAuthoringService = expressionAuthoringService ?? new ExpressionAuthoringService(this.ExpressionEngine);
        loadSaveWorkflow = new LoadSaveWorkflow(Workspace, this.loadService, this.saveService);
        ClipEditingCoordinator = new ClipEditingCoordinator(Workspace, this.editingService, this.frameRateService);
        projectionFacade = new ProjectionFacade(Workspace, this.ExpressionEngine, this.formatter);
        statusDiagnosticsPresenter = new StatusDiagnosticsPresenter(Localizer, this.logger, value => StatusText = value);
        displayOptionCoordinator = new DisplayOptionCoordinator(Localizer, this.frameRateService);
        PortAdapters = new MainWindowPortAdapters(this);
        chapterNameTemplateStatus = Localizer.GetString("Status.TemplateNotSelected");
        statusText = Localizer.GetString("Status.Ready");
        RefreshXmlLanguageDisplayOptions(notify: false);
        RefreshChapterNameModeOptions();
        RefreshFrameRateDisplayOptions();
        Localizer.CultureChanged += (_, _) => RefreshLocalizedState();
        selectedFrameRateOption = this.frameRateService.Options[0];
        ClipOptions.CollectionChanged += OnClipOptionsChanged;
        Rows.CollectionChanged += OnRowsChanged;

        InitializeCommands();
    }

    /// <summary>Explicit workspace owning clip session, edit buffer, path, and revision.</summary>
    internal ChapterWorkspace Workspace { get; } = new();

    internal MainWindowPortAdapters PortAdapters { get; }

    internal IChapterExpressionEngine ExpressionEngine { get; }

    internal ChapterSet? CurrentChapterSet => CurrentInfo;

    internal ClipEditingCoordinator ClipEditingCoordinator { get; }

    internal ISettingsStore<ChapterToolSettings>? SettingsStore { get; }

    internal void NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    private void InitializeCommands()
    {
        InitializeFileCommands();
        InitializeEditCommands();
        InitializeWindowCommands();
    }

    private void InitializeFileCommands()
    {
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
        SaveCommand = new UiCommand(async (parameter, token) => await SaveAsync(parameter?.ToString(), token), _ => CurrentInfo is not null);
    }

    private void InitializeEditCommands()
    {
        RefreshCommand = new UiCommand((_, _) =>
        {
            ApplyFrameInfo();
            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
        ChangeFpsCommand = new UiCommand((_, _) =>
        {
            ChangeFpsToSelectedOption();
            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null && selectedFrameRateOption.IsValid);
        SelectClipCommand = new UiCommand((parameter, _) =>
        {
            SelectClip(Convert.ToInt32(parameter));
            return ValueTask.CompletedTask;
        }, parameter => parameter is int index and >= 0 && index < ClipOptions.Count);
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
            if (CurrentInfo is not null && parameter is IReadOnlySet<int> indexes)
            {
                ApplyEdit(ClipEditingCoordinator.Delete(CurrentInfo, indexes), Localizer.Format(LocalizedMessage.Create("Action.DeleteRows", ("indexes", string.Join(",", indexes.Order())))));
            }

            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
        InsertCommand = new UiCommand(parameter =>
        {
            if (CurrentInfo is not null)
            {
                var index = parameter is int value ? value : Rows.Count;
                ApplyEdit(ClipEditingCoordinator.InsertBefore(CurrentInfo, index), Localizer.Format(LocalizedMessage.Create("Action.InsertRow", ("index", index))));
            }

            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
    }

    private void InitializeWindowCommands()
    {
        PreviewCommand = WindowCommand("preview", () => CurrentInfo is not null);
        LogCommand = WindowCommand("log");
        SettingsCommand = WindowCommand("settings");
        LanguageCommand = WindowCommand("language");
        ExpressionCommand = WindowCommand("expression");
        TemplateNamesCommand = WindowCommand("template-names");
        ZonesCommand = WindowCommand("zones");
        ForwardShiftCommand = WindowCommand("forward-shift");
        OpenRelatedMediaCommand = new UiCommand(async (parameter, token) => await OpenRelatedMediaAsync(parameter, token), _ => RelatedMediaReferences.Count > 0);
    }

    public string CurrentPath => Workspace.CurrentPath;

    public string DisplayPath => Workspace.DisplayPath;

    /// <summary>
    /// Authoritative path text for the source path box and reload/load adapters.
    /// Updated by browse/drop and synchronized from successful loads.
    /// </summary>
    public string SourcePath
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<ChapterRowViewModel> Rows { get; } = [];

    public bool IsChapterGridEmpty => Rows.Count == 0;

    public ObservableCollection<ChapterImportEntry> ClipOptions { get; } = [];

    public ObservableCollection<SelectorDisplayOption> ClipDisplayOptions { get; } = [];

    public ObservableCollection<SelectorDisplayOption> ChapterNameModeOptions { get; } = [];

    public ObservableCollection<SelectorDisplayOption> FrameRateDisplayOptions { get; } = [];

    public int SelectedClipIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(RelatedMediaReferences));
                OnPropertyChanged(nameof(SelectedClipDisplayOption));
            }
        }
    }

    public SelectorDisplayOption? SelectedClipDisplayOption
    {
        get => SelectedClipIndex < 0 || SelectedClipIndex >= ClipDisplayOptions.Count
            ? null
            : ClipDisplayOptions[SelectedClipIndex];
        set
        {
            var index = value is null ? -1 : ClipDisplayOptions.IndexOf(value);
            if (index >= 0 && index != SelectedClipIndex)
            {
                SelectClip(index);
                NotifyStateChanged();
            }
        }
    }

    private HashSet<int> SelectedRowIndexes
    {
        get;
        set => SetProperty(ref field, value);
    } = [];

    private bool suppressFrameOptionsRefresh;

    public bool RoundFrames
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnFrameOptionsChangedFromBinding();
            }
        }
    } = true;

    public decimal FrameAccuracyTolerance
    {
        get;
        set
        {
            var normalized = NormalizeFrameAccuracyTolerance(value);
            if (SetProperty(ref field, normalized))
            {
                RefreshRows();
            }
        }
    } = 0.15m;

    public int SelectedFrameRateIndex
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            if (!suppressFrameOptionsRefresh)
            {
                var entry = FrameRateOptionForComboIndex(value);
                if (entry is not null)
                {
                    selectedFrameRateOption = entry;
                }
            }

            OnFrameOptionsChangedFromBinding();
        }
    } = 0;

    public bool IsClipSelectionVisible => ClipOptions.Count > 1 || IsClipCombineChecked;

    /// <summary>Derived from typed clip session mode (combined vs split).</summary>
    public bool IsClipCombineChecked => Workspace.ClipSession?.IsCombined == true;

    public bool IsAdvancedPanelExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ChapterExportFormat SaveFormat
    {
        get => Workspace.ExportPreferences.Format;
        set
        {
            if (Workspace.ExportPreferences.SetFormat(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(SaveFormatIndex));
                OnPropertyChanged(nameof(IsXmlLanguageEnabled));
            }
        }
    }

    public int SaveFormatIndex
    {
        get
        {
            var index = ChapterExportFormats.IndexOf(SaveFormat);
            return Math.Max(0, index);
        }
        set => SaveFormat = ChapterExportFormats.AtIndex(value);
    }

    public IReadOnlyList<string> XmlLanguageOptions { get; } =
        XmlChapterLanguageCatalog.Languages.Select(static language => language.Code).ToList();

    private IReadOnlyDictionary<string, int>? xmlLanguageIndexes;

    public IReadOnlyList<SelectorDisplayOption> XmlLanguageDisplayOptions => xmlLanguageDisplayOptions;

    public SelectorDisplayOption? SelectedXmlLanguageDisplayOption
    {
        get
        {
            var entries = XmlLanguageDisplayOptions;
            return XmlLanguageIndex < 0 || XmlLanguageIndex >= entries.Count
                ? null
                : entries[XmlLanguageIndex];
        }

        set
        {
            var index = value is null
                ? -1
                : XmlLanguageDisplayOptions.ToList().FindIndex(entry =>
                    string.Equals(entry.MainText, value.MainText, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                XmlLanguageIndex = index;
            }
        }
    }

    public string XmlLanguage
    {
        get => Workspace.ExportPreferences.XmlLanguage;
        set
        {
            if (Workspace.ExportPreferences.SetXmlLanguage(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(XmlLanguageIndex));
                OnPropertyChanged(nameof(SelectedXmlLanguageDisplayOption));
            }
        }
    }

    public int XmlLanguageIndex
    {
        get
        {
            xmlLanguageIndexes ??= XmlLanguageOptions
                .Select(static (entry, index) => (entry, index))
                .ToDictionary(static item => item.entry, static item => item.index, StringComparer.OrdinalIgnoreCase);
            return xmlLanguageIndexes.GetValueOrDefault(XmlLanguage, 0);
        }

        set
        {
            if (value >= 0 && value < XmlLanguageOptions.Count)
            {
                XmlLanguage = XmlLanguageOptions[value];
            }
        }
    }

    public bool IsXmlLanguageEnabled => SaveFormat == ChapterExportFormat.Xml;

    public string UiLanguage
    {
        get;
        internal set => SetProperty(ref field, value);
    } = string.Empty;

    public IAppLocalizer Localizer { get; }

    public IExpressionAuthoringService ExpressionAuthoringService { get; }

    public bool AutoGenerateNames
    {
        get => Workspace.Projection.AutoGenerateNames;
        set
        {
            var previousTemplate = Workspace.Projection.UseTemplateNames;
            if (Workspace.Projection.SetAutoGenerateNames(value))
            {
                OnPropertyChanged();
                if (previousTemplate != Workspace.Projection.UseTemplateNames)
                {
                    OnPropertyChanged(nameof(UseTemplateNames));
                }

                OnPropertyChanged(nameof(ChapterNameModeIndex));
                RefreshRows();
            }
        }
    }

    public bool UseTemplateNames
    {
        get => Workspace.Projection.UseTemplateNames;
        set
        {
            var previousAuto = Workspace.Projection.AutoGenerateNames;
            if (Workspace.Projection.SetUseTemplateNames(value))
            {
                OnPropertyChanged();
                if (previousAuto != Workspace.Projection.AutoGenerateNames)
                {
                    OnPropertyChanged(nameof(AutoGenerateNames));
                }

                OnPropertyChanged(nameof(ChapterNameModeIndex));
                RefreshRows();
            }
        }
    }

    public string ChapterNameTemplateText
    {
        get => Workspace.Projection.ChapterNameTemplateText;
        set
        {
            if (Workspace.Projection.SetChapterNameTemplateText(value))
            {
                OnPropertyChanged();
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
            if (isRefreshingChapterNameModeOptions)
            {
                return;
            }

            AutoGenerateNames = false;
            UseTemplateNames = value is 1 or 2;
            if (value != 2)
            {
                ChapterNameTemplateText = string.Empty;
                ChapterNameTemplateStatus = Localizer.GetString("Status.TemplateNotSelected");
            }

            OnPropertyChanged();
        }
    }

    public int OrderShift
    {
        get => Workspace.Projection.OrderShift;
        set
        {
            if (Workspace.Projection.SetOrderShift(value))
            {
                OnPropertyChanged();
                RefreshRows();
            }
        }
    }

    public bool ApplyExpression
    {
        get => Workspace.Projection.ApplyExpression;
        set
        {
            if (Workspace.Projection.SetApplyExpression(value))
            {
                OnPropertyChanged();
                RefreshRows();
            }
        }
    }

    public string Expression
    {
        get => Workspace.Projection.Expression;
        set
        {
            if (Workspace.Projection.SetExpression(value))
            {
                OnPropertyChanged();
                RefreshRows();
            }
        }
    }

    public string ExpressionPresetId
    {
        get => Workspace.Projection.ExpressionPresetId;
        set
        {
            if (Workspace.Projection.SetExpressionPresetId(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string ExpressionSourceName
    {
        get => Workspace.Projection.ExpressionSourceName;
        set
        {
            if (Workspace.Projection.SetExpressionSourceName(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string? SaveDirectory
    {
        get => Workspace.ExportPreferences.SaveDirectory;
        internal set
        {
            if (Workspace.ExportPreferences.SetSaveDirectory(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveSaveDirectoryDisplay));
            }
        }
    }

    public string EffectiveSaveDirectoryDisplay
    {
        get
        {
            var directory = ResolveSaveDirectory(directoryOverride: null);
            return string.IsNullOrWhiteSpace(directory)
                ? Localizer.GetString("Main.OutputDirectoryUnresolved")
                : directory;
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public double Progress
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public IReadOnlyList<ReferencedMediaFile> RelatedMediaReferences =>
        Workspace.ClipSession?.RelatedMedia ?? [];

    public bool CanAppendMpls => Workspace.ClipSession?.CanAppendMpls == true;

    public bool CanCombine => Workspace.ClipSession?.CanCombine == true;

    public bool CanSave => CurrentInfo is not null;

    public bool CanRefreshRows => CurrentInfo is not null;

    public bool CanEditRows => CurrentInfo is not null;

    public bool CanOpenRelatedMedia => RelatedMediaReferences.Count > 0;

    public bool EmitBom
    {
        get => Workspace.ExportPreferences.EmitBom;
        internal set
        {
            if (Workspace.ExportPreferences.SetEmitBom(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public OutputTextEncoding OutputTextEncoding
    {
        get => Workspace.ExportPreferences.TextEncoding;
        internal set
        {
            if (Workspace.ExportPreferences.SetTextEncoding(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public UiCommand LoadCommand { get; private set; } = null!;

    public UiCommand ReloadCommand { get; private set; } = null!;

    public UiCommand AppendMplsCommand { get; private set; } = null!;

    public UiCommand DropPathLoadCommand { get; private set; } = null!;

    public UiCommand SaveCommand { get; private set; } = null!;

    public UiCommand RefreshCommand { get; private set; } = null!;

    public UiCommand ChangeFpsCommand { get; private set; } = null!;

    public UiCommand SelectClipCommand { get; private set; } = null!;

    public UiCommand CombineCommand { get; private set; } = null!;

    public UiCommand EditTimeCommand { get; private set; } = null!;

    public UiCommand EditNameCommand { get; private set; } = null!;

    public UiCommand EditFrameCommand { get; private set; } = null!;

    public UiCommand DeleteCommand { get; private set; } = null!;

    public UiCommand InsertCommand { get; private set; } = null!;

    public UiCommand PreviewCommand { get; private set; } = null!;

    public UiCommand LogCommand { get; private set; } = null!;

    public UiCommand SettingsCommand { get; private set; } = null!;

    public UiCommand LanguageCommand { get; private set; } = null!;

    public UiCommand ExpressionCommand { get; private set; } = null!;

    public UiCommand TemplateNamesCommand { get; private set; } = null!;

    public UiCommand ZonesCommand { get; private set; } = null!;

    public UiCommand ForwardShiftCommand { get; private set; } = null!;

    public UiCommand OpenRelatedMediaCommand { get; private set; } = null!;

    public void SetFrameOptions(int frameRateIndex, bool roundFrames)
    {
        suppressFrameOptionsRefresh = true;
        try
        {
            RoundFrames = roundFrames;
            var entry = FrameRateOptionForComboIndex(frameRateIndex);
            if (entry is not null)
            {
                selectedFrameRateOption = entry;
                SelectedFrameRateIndex = frameRateIndex;
                return;
            }

            selectedFrameRateOption = CurrentInfo is null
                ? frameRateService.Options[0]
                : frameRateService.FindByValue((decimal)CurrentInfo.FramesPerSecond);
            SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        }
        finally
        {
            suppressFrameOptionsRefresh = false;
        }
    }

    private void OnFrameOptionsChangedFromBinding()
    {
        if (suppressFrameOptionsRefresh || CurrentInfo is null)
        {
            return;
        }

        ApplyFrameInfo();
    }

    /// <summary>Assigns frame-rate selection without re-running ApplyFrameInfo (used during internal refresh).</summary>
    private void SetSelectedFrameRateIndexSilent(int index)
    {
        suppressFrameOptionsRefresh = true;
        try
        {
            SelectedFrameRateIndex = index;
        }
        finally
        {
            suppressFrameOptionsRefresh = false;
        }
    }




    public string BuildPreview()
    {
        if (CurrentInfo is null)
        {
            return string.Empty;
        }

        var projection = CurrentOutputProjection();
        var entries = CurrentExportOptionsForProjectedInfo();

        // Use composition-injected export service (same path family as save), not ad-hoc construction.
        var result = exportService.Export(projection.Info, entries);
        if (!result.Success)
        {
            return string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        return result.Content;
    }

    public string LogText() => LogService.Format(FormatLogEntry);

    public IApplicationLogService LogService { get; }

    public void ClearLog() => LogService.Clear();

    public void UpdateSelectedRows(IReadOnlySet<int> indexes)
    {
        SelectedRowIndexes = indexes.Where(index => index >= 0).ToHashSet();
        NotifyCommandStates();
    }

    public string CreateZonesText()
    {
        if (CurrentInfo is null)
        {
            return string.Empty;
        }

        var indexes = SelectedRowIndexes.Count == 0
            ? Enumerable.Range(0, CurrentInfo.Chapters.Count).ToHashSet()
            : SelectedRowIndexes;
        var result = editingService.CreateZones(CurrentInfo, indexes, (decimal)CurrentInfo.FramesPerSecond);
        SetStatus(result.Diagnostics.Count == 0 ? "Status.ZonesGenerated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.CreateZones", ("selectedRows", indexes.Count), ("chapters", CurrentInfo.Chapters.Count));
        LogDiagnostics(Localizer.GetString("Operation.CreateZones"), result.Diagnostics);
        LogStatus();
        NotifyStateChanged();
        return result.Zones;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsChapterGridEmpty));
        NotifyCommandStates();
    }

    internal void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsClipSelectionVisible));
        OnPropertyChanged(nameof(IsClipCombineChecked));
        OnPropertyChanged(nameof(RelatedMediaReferences));
        OnPropertyChanged(nameof(CanAppendMpls));
        OnPropertyChanged(nameof(CanCombine));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRefreshRows));
        OnPropertyChanged(nameof(CanEditRows));
        OnPropertyChanged(nameof(CanOpenRelatedMedia));
        OnPropertyChanged(nameof(EffectiveSaveDirectoryDisplay));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        ReloadCommand.RaiseCanExecuteChanged();
        AppendMplsCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        ChangeFpsCommand.RaiseCanExecuteChanged();
        SelectClipCommand.RaiseCanExecuteChanged();
        CombineCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        InsertCommand.RaiseCanExecuteChanged();
        OpenRelatedMediaCommand.RaiseCanExecuteChanged();
        PreviewCommand.RaiseCanExecuteChanged();
        LogCommand.RaiseCanExecuteChanged();
        SettingsCommand.RaiseCanExecuteChanged();
        LanguageCommand.RaiseCanExecuteChanged();
        ExpressionCommand.RaiseCanExecuteChanged();
        TemplateNamesCommand.RaiseCanExecuteChanged();
        ZonesCommand.RaiseCanExecuteChanged();
        ForwardShiftCommand.RaiseCanExecuteChanged();
    }



    private UiCommand WindowCommand(string id, Func<bool>? canExecute = null) =>
        new(async (_, token) => await windowService.ShowAsync(id, this, token), _ => canExecute?.Invoke() ?? true);

    private async ValueTask OpenRelatedMediaAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (shellService is null)
        {
            SetStatus("Status.ShellUnavailable");
            LogStatus(LogLevel.Warning);
            NotifyStateChanged();
            return;
        }

        var reference = parameter as ReferencedMediaFile ?? RelatedMediaReferences.FirstOrDefault();
        var target = reference?.AbsolutePath;
        if (string.IsNullOrWhiteSpace(target) && reference is not null && !string.IsNullOrWhiteSpace(CurrentPath))
        {
            var baseDirectory = Directory.Exists(CurrentPath) ? CurrentPath : Path.GetDirectoryName(CurrentPath);
            target = baseDirectory is null ? reference.RelativePath : Path.GetFullPath(Path.Combine(baseDirectory, reference.RelativePath));
        }

        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            SetStatus("Status.RelatedMediaNotFound");
            Log(LogLevel.Warning, "Log.RelatedMediaNotFound",
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

    public static decimal NormalizeFrameAccuracyTolerance(decimal value)
    {
        if (value <= 0m)
        {
            return 0.15m;
        }

        var bounded = Math.Clamp(value, 0.01m, 0.30m);
        foreach (var recommended in FrameAccuracyToleranceRecommendedValues)
        {
            if (Math.Abs(bounded - recommended) <= 0.01m)
            {
                return recommended;
            }
        }

        return bounded;
    }

    private static readonly decimal[] FrameAccuracyToleranceRecommendedValues =
    [
        0.05m,
        0.10m,
        0.15m,
        0.20m,
        0.25m,
        0.30m
    ];

    private enum EditKind
    {
        Time,
        Name,
        Frame
    }

}
