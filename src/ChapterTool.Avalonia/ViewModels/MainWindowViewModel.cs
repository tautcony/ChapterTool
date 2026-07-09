using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ObservableViewModel
{
    private readonly IChapterLoadService loadService;
    private readonly IChapterSaveService saveService;
    private readonly IChapterEditingService editingService;
    private readonly ChapterSegmentService segmentService;
    private readonly IWindowService windowService;
    private readonly IChapterTimeFormatter formatter;
    private readonly IFrameRateService frameRateService;
    private readonly IChapterExpressionEngine expressionEngine;
    private readonly ChapterOutputProjectionService outputProjectionService;
    private readonly IApplicationLogService logService;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IShellService? shellService;
    private readonly ISettingsStore<AppSettings>? appSettingsStore;

    private ChapterImportSource? currentGroup;
    private ChapterSet? currentInfo;
    private FrameRateOption selectedFrameRateOption;
    private decimal? configuredFrameRate;
    private bool currentInfoBelongsToSelectedClip;
    private ChapterImportSource? splitClipGroup;
    private ChapterImportEntry? combinedClipOption;
    private int loadOperationVersion;
    private bool isRefreshingChapterNameModeOptions;
    private bool autoGenerateNames;
    private bool useTemplateNames;
    private string chapterNameTemplateText = string.Empty;
    private string chapterNameTemplateStatus;
    private string statusText;
    private LocalizedMessage? currentStatusMessage;
    private LocalizedMessage? currentProgressMessage;
    private readonly ObservableCollection<SelectorDisplayOption> xmlLanguageDisplayOptions = [];

    public MainWindowViewModel(
        IChapterLoadService loadService,
        IChapterSaveService saveService,
        IChapterEditingService editingService,
        ChapterSegmentService segmentService,
        IWindowService windowService,
        IChapterTimeFormatter formatter,
        IApplicationLogService logService,
        ILogger<MainWindowViewModel> logger,
        IShellService? shellService = null,
        ISettingsStore<AppSettings>? appSettingsStore = null,
        IFrameRateService? frameRateService = null,
        IAppLocalizer? localizer = null,
        IChapterExpressionEngine? expressionEngine = null)
    {
        this.loadService = loadService;
        this.saveService = saveService;
        this.editingService = editingService;
        this.segmentService = segmentService;
        this.windowService = windowService;
        this.formatter = formatter;
        this.frameRateService = frameRateService ?? new FrameRateService();
        this.expressionEngine = expressionEngine ?? new LuaExpressionScriptService();
        outputProjectionService = new ChapterOutputProjectionService(this.expressionEngine);
        this.logService = logService;
        this.logger = logger;

        this.Localizer = localizer ?? new AppLocalizationManager();
        this.shellService = shellService;
        this.appSettingsStore = appSettingsStore;
        chapterNameTemplateStatus = this.Localizer.GetString("Status.TemplateNotSelected");
        statusText = this.Localizer.GetString("Status.Ready");
        RefreshXmlLanguageDisplayOptions(notify: false);
        RefreshChapterNameModeOptions();
        this.Localizer.CultureChanged += (_, _) => RefreshLocalizedState();
        selectedFrameRateOption = this.frameRateService.Options[0];
        ClipOptions.CollectionChanged += OnClipOptionsChanged;
        Rows.CollectionChanged += OnRowsChanged;

        InitializeCommands();
    }

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
        SaveCommand = new UiCommand(async (_, token) => await SaveAsync(null, token), _ => currentInfo is not null);
        SaveDirectoryCommand = new UiCommand(async (parameter, token) => await SaveAsync(parameter?.ToString() ?? SaveDirectory, token), _ => currentInfo is not null);
    }

    private void InitializeEditCommands()
    {
        RefreshCommand = new UiCommand((_, _) =>
        {
            ApplyFrameInfo();
            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
        ChangeFpsCommand = new UiCommand((_, _) =>
        {
            ChangeFpsToSelectedOption();
            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null && selectedFrameRateOption.IsValid);
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
            if (currentInfo is not null && parameter is IReadOnlySet<int> indexes)
            {
                ApplyEdit(editingService.Delete(currentInfo, indexes), Localizer.Format(LocalizedMessage.Create("Action.DeleteRows", ("indexes", string.Join(",", indexes.Order())))));
            }

            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
        InsertCommand = new UiCommand(parameter =>
        {
            if (currentInfo is not null)
            {
                var index = parameter is int value ? value : Rows.Count;
                ApplyEdit(editingService.InsertBefore(currentInfo, index), Localizer.Format(LocalizedMessage.Create("Action.InsertRow", ("index", index))));
            }

            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
    }

    private void InitializeWindowCommands()
    {
        PreviewCommand = WindowCommand("preview", () => currentInfo is not null);
        LogCommand = WindowCommand("log");
        SettingsCommand = WindowCommand("settings");
        ColorSettingsCommand = WindowCommand("color-settings");
        LanguageCommand = WindowCommand("language");
        ExpressionCommand = WindowCommand("expression");
        TemplateNamesCommand = WindowCommand("template-names");
        ZonesCommand = WindowCommand("zones");
        ForwardShiftCommand = WindowCommand("forward-shift");
        OpenRelatedMediaCommand = new UiCommand(async (parameter, token) => await OpenRelatedMediaAsync(parameter, token), _ => RelatedMediaReferences.Count > 0);
    }

    public string CurrentPath
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string DisplayPath
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<ChapterRowViewModel> Rows { get; } = [];

    public bool IsChapterGridEmpty => Rows.Count == 0;

    public ObservableCollection<ChapterImportEntry> ClipOptions { get; } = [];

    public ObservableCollection<SelectorDisplayOption> ClipDisplayOptions { get; } = [];

    public ObservableCollection<SelectorDisplayOption> ChapterNameModeOptions { get; } = [];

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
    } = new();

    public bool RoundFrames
    {
        get;
        set => SetProperty(ref field, value);
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
        private set => SetProperty(ref field, value);
    } = -1;

    public bool IsClipSelectionVisible => ClipOptions.Count > 1 || IsClipCombineChecked;

    public bool IsClipCombineChecked
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsClipSelectionVisible));
                OnPropertyChanged(nameof(CanCombine));
            }
        }
    }

    public bool IsAdvancedPanelExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ChapterExportFormat SaveFormat
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(SaveFormatIndex));
                OnPropertyChanged(nameof(IsXmlLanguageEnabled));
            }
        }
    } = ChapterExportFormat.Txt;

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
        get;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "und" : value.Trim().ToLowerInvariant();
            if (SetProperty(ref field, normalized))
            {
                OnPropertyChanged(nameof(XmlLanguageIndex));
                OnPropertyChanged(nameof(SelectedXmlLanguageDisplayOption));
            }
        }
    } = "und";

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
        private set => SetProperty(ref field, value);
    } = "";

    public IAppLocalizer Localizer { get; }

    public IReadOnlyList<ChapterExpressionPreset> ExpressionPresets => expressionEngine.Presets;

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
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RefreshRows();
            }
        }
    }

    public bool ApplyExpression
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RefreshRows();
            }
        }
    }

    public string Expression
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RefreshRows();
            }
        }
    } = "t";

    public string ExpressionPresetId
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string ExpressionSourceName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string? SaveDirectory
    {
        get;
        set => SetProperty(ref field, value);
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
        currentGroup is null || SelectedClipIndex < 0 || SelectedClipIndex >= currentGroup.Entries.Count
            ? []
            : currentGroup.Entries[SelectedClipIndex].ReferencedMediaFiles ?? [];

    public bool CanAppendMpls => currentGroup?.Entries.Any(static entry => entry.ChapterSet.ImportFormat == ChapterImportFormat.Mpls) == true;

    public bool CanCombine => IsClipCombineChecked
        || currentGroup is not null
            && currentGroup.Entries.Count > 1
            && currentGroup.Entries[0].ChapterSet.ImportFormat is ChapterImportFormat.Mpls or ChapterImportFormat.DvdIfo
            && currentGroup.Entries.All(entry => entry.ChapterSet.ImportFormat == currentGroup.Entries[0].ChapterSet.ImportFormat);

    public bool CanSave => currentInfo is not null;

    public bool CanRefreshRows => currentInfo is not null;

    public bool CanEditRows => currentInfo is not null;

    public bool CanOpenRelatedMedia => RelatedMediaReferences.Count > 0;

    public bool EmitBom
    {
        get;
        private set => SetProperty(ref field, value);
    } = true;

    public UiCommand LoadCommand { get; private set; } = null!;
    public UiCommand ReloadCommand { get; private set; } = null!;
    public UiCommand AppendMplsCommand { get; private set; } = null!;
    public UiCommand DropPathLoadCommand { get; private set; } = null!;
    public UiCommand SaveCommand { get; private set; } = null!;
    public UiCommand SaveDirectoryCommand { get; private set; } = null!;
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
    public UiCommand ColorSettingsCommand { get; private set; } = null!;
    public UiCommand LanguageCommand { get; private set; } = null!;
    public UiCommand ExpressionCommand { get; private set; } = null!;
    public UiCommand TemplateNamesCommand { get; private set; } = null!;
    public UiCommand ZonesCommand { get; private set; } = null!;
    public UiCommand ForwardShiftCommand { get; private set; } = null!;
    public UiCommand OpenRelatedMediaCommand { get; private set; } = null!;

    public void SetFrameOptions(int frameRateIndex, bool roundFrames)
    {
        RoundFrames = roundFrames;
        var entry = FrameRateOptionForComboIndex(frameRateIndex);
        if (entry is not null)
        {
            selectedFrameRateOption = entry;
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
        ApplySettings(settings);
        Log("Log.SettingsLoaded",
            ("savingPath", SaveDirectory ?? string.Empty),
            ("language", UiLanguage));
        NotifyStateChanged();
    }

    public void ApplySettings(AppSettings settings)
    {
        SaveDirectory = settings.SavingPath;
        UiLanguage = AppLanguage.Normalize(settings.Language);
        Localizer.SetCulture(UiLanguage);
        if (Enum.TryParse<ChapterExportFormat>(settings.DefaultSaveFormat, ignoreCase: true, out var format))
        {
            SaveFormat = format;
        }

        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
        XmlLanguage = string.IsNullOrWhiteSpace(settings.DefaultXmlLanguage)
            ? "und"
            : settings.DefaultXmlLanguage;
        EmitBom = settings.EmitBom;
        NotifyStateChanged();
    }

    public async ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
    {
        UiLanguage = AppLanguage.Normalize(language);
        Localizer.SetCulture(UiLanguage);
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
        var entries = CurrentExportOptionsForProjectedInfo();
        var result = new ChapterExportService(formatter).Export(projection.Info, entries);
        if (!result.Success)
        {
            return string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        return result.Content;
    }

    public string LogText() => logService.Format(FormatLogEntry);

    public IApplicationLogService LogService => logService;

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
        LogDiagnostics(Localizer.GetString("Operation.CreateZones"), result.Diagnostics);
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

        ApplyEdit(editingService.ShiftFramesForward(currentInfo, frames, (decimal)currentInfo.FramesPerSecond), Localizer.Format(LocalizedMessage.Create("Action.ShiftFramesForward", ("frames", frames))));
    }

    private ChapterExportOptions CurrentExportOptions() =>
        new(
            Format: SaveFormat,
            XmlLanguage: XmlLanguage,
            SourceFileName: currentInfo?.SourceName,
            AutoGenerateNames: AutoGenerateNames,
            UseTemplateNames: UseTemplateNames,
            ChapterNameTemplateText: ChapterNameTemplateText,
            OrderShift: OrderShift,
            ApplyExpression: ApplyExpression,
            Expression: Expression,
            ExpressionPresetId: ExpressionPresetId,
            ExpressionSourceName: ExpressionSourceName,
            EmitBom: EmitBom);

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        var operationId = Interlocked.Increment(ref loadOperationVersion);
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status.NoSourceSelected");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.LoadingSource", ("path", path));
        Progress = 0.05;
        SetProgressStatus(ChapterImportProgressPhase.LoadingSource);
        var progress = new ChapterImportProgressSink(update =>
        {
            if (operationId != Volatile.Read(ref loadOperationVersion))
            {
                return;
            }

            Progress = Math.Clamp(update.Fraction ?? Progress, 0, 0.98);
            SetProgressStatus(update.Phase);
        });
        var result = await loadService.LoadAsync(path, progress, cancellationToken);
        if (operationId != Volatile.Read(ref loadOperationVersion))
        {
            return;
        }

        LogImportSummary("Load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.LoadFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            currentProgressMessage = null;
            Progress = 0;
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        CurrentPath = path;
        DisplayPath = Path.GetFileName(path);
        currentGroup = result.Groups[0];
        splitClipGroup = null;
        combinedClipOption = null;
        IsClipCombineChecked = false;
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in currentGroup.Entries)
        {
            ClipOptions.Add(entry);
        }

        SelectClip(Math.Clamp(currentGroup.DefaultEntryIndex, 0, ClipOptions.Count - 1));
        SetStatus("Status.LoadedChapters", ("count", Rows.Count));
        currentProgressMessage = null;
        Progress = 1;
        Log("Log.StatusFromPath", ("status", StatusText), ("path", path));
        LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
        NotifyStateChanged();
    }

    private async ValueTask SaveAsync(string? directory, CancellationToken cancellationToken)
    {
        if (currentInfo is null)
        {
            return;
        }

        var projection = CurrentOutputProjection();
        var entries = CurrentExportOptionsForProjectedInfo();
        Log("Log.SavingChapters",
            ("format", entries.Format),
            ("directory", directory ?? string.Empty),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("chapters", projection.Info.Chapters.Count),
            ("applyExpression", ApplyExpression),
            ("expression", Expression));
        LogDiagnostics(Localizer.GetString("Operation.OutputProjection"), projection.Diagnostics);
        var result = await saveService.SaveAsync(projection.Info, entries, directory, cancellationToken);
        if (result.Success && !string.IsNullOrWhiteSpace(directory))
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
        LogDiagnostics(Localizer.GetString("Operation.Save"), result.Diagnostics);
        NotifyStateChanged();
    }

    private void SelectClip(int index)
    {
        if (index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        SelectedClipIndex = index;
        currentInfo = ClipOptions[index].ChapterSet;
        configuredFrameRate = (decimal)currentInfo.FramesPerSecond;
        currentInfoBelongsToSelectedClip = !IsClipCombineChecked;
        Log("Log.SelectedSourceOption",
            ("index", index),
            ("label", ClipOptions[index].DisplayName),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("sourceType", ChapterImportFormats.DisplayName(currentInfo.ImportFormat)),
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
            _ => new ChapterEditResult(currentInfo, [])
        };
        ApplyEdit(result, Localizer.Format(LocalizedMessage.Create("Action.EditCell", ("kind", Localizer.GetString($"EditKind.{kind}")), ("row", edit.Index), ("value", edit.Value))));
        return ValueTask.CompletedTask;
    }

    private void CombineSegments()
    {
        if (currentGroup is null)
        {
            return;
        }

        if (IsClipCombineChecked)
        {
            RestoreSplitClips();
            return;
        }

        var groupToCombine = splitClipGroup ?? currentGroup;
        var result = ChapterSegmentService.Combine(groupToCombine);
        if (result.Diagnostics.Count > 0)
        {
            ApplyEdit(result, Localizer.Format(LocalizedMessage.Create("Action.CombineSegments", ("entries", groupToCombine.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(groupToCombine.Entries[0].ChapterSet.ImportFormat)))));
            return;
        }

        splitClipGroup = groupToCombine;
        combinedClipOption = CreateCombinedClipOption(groupToCombine, result.ChapterSet);
        currentGroup = groupToCombine with { Entries = [combinedClipOption], DefaultEntryIndex = 0 };
        IsClipCombineChecked = true;
        currentInfo = result.ChapterSet;
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        ClipOptions.Add(combinedClipOption);
        SelectClip(0);
        SetStatus("Status.Updated");
        Log("Log.EditChapters",
            ("action", Localizer.Format(LocalizedMessage.Create("Action.CombineSegments", ("entries", groupToCombine.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(groupToCombine.Entries[0].ChapterSet.ImportFormat))))),
            ("before", groupToCombine.Entries.Sum(static entry => entry.ChapterSet.Chapters.Count)),
            ("after", currentInfo?.Chapters.Count ?? 0));
        LogStatus();
        NotifyStateChanged();
    }

    private async ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken)
    {
        var operationId = Volatile.Read(ref loadOperationVersion);
        var expectedGroup = currentGroup;
        var expectedSplitGroup = splitClipGroup;
        if (expectedGroup is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", path));
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (operationId != Volatile.Read(ref loadOperationVersion)
            || !ReferenceEquals(expectedGroup, currentGroup)
            || !ReferenceEquals(expectedSplitGroup, splitClipGroup))
        {
            return;
        }

        LogImportSummary("Append load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.AppendFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var baseGroup = expectedSplitGroup ?? expectedGroup;
        var edit = ChapterSegmentService.Append(baseGroup, result.Groups[0]);
        if (edit.Diagnostics.Count > 0)
        {
            SetStatus(null, diagnostic: edit.Diagnostics[0]);
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendEdit"), edit.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var entries = baseGroup.Entries.ToList();
        entries.AddRange(result.Groups[0].Entries);
        var appendedGroup = baseGroup with { Entries = entries };
        var combinedOption = CreateCombinedClipOption(appendedGroup, edit.ChapterSet);

        splitClipGroup = appendedGroup;
        combinedClipOption = combinedOption;
        currentGroup = appendedGroup with { Entries = [combinedOption], DefaultEntryIndex = 0 };
        IsClipCombineChecked = true;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in currentGroup.Entries)
        {
            ClipOptions.Add(entry);
        }

        currentInfo = edit.ChapterSet;
        currentInfoBelongsToSelectedClip = false;
        SelectClip(0);
        SetStatus("Status.AppendedMplsSegments", ("count", result.Groups[0].Entries.Count));
        LogStatus();
        LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
        NotifyStateChanged();
    }

    private void ApplyEdit(ChapterEditResult result, string? action = null)
    {
        var effectiveAction = action ?? Localizer.GetString("Action.EditChapters");
        var before = currentInfo?.Chapters.Count ?? 0;
        currentInfo = result.ChapterSet;
        ApplyFrameInfo();
        SetStatus(result.Diagnostics.Count == 0 ? "Status.Updated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.EditChapters", ("action", effectiveAction), ("before", before), ("after", currentInfo.Chapters.Count));
        LogDiagnostics(effectiveAction, result.Diagnostics);
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
            detection = frameRateService.DetectDetailed(currentInfo, FrameAccuracyTolerance);
            appliedOption = detection.Option;
        }
        else
        {
            appliedOption = selectedFrameRateOption;
        }

        var result = frameRateService.UpdateFrames(currentInfo, appliedOption, RoundFrames, FrameAccuracyTolerance);
        currentInfo = result.Info;
        var storedInfo = configuredFrameRate is null
            ? currentInfo
            : currentInfo with { FramesPerSecond = (double)configuredFrameRate.Value };

        if (detection is not null)
        {
            selectedFrameRateOption = frameRateService.Options[0];
            SetStatus("Status.DetectedFrameRate", ("displayName", detection.Option.DisplayName), ("confidence", detection.Confidence));
            Log("Log.AutoFrameRateDetection",
                ("entry", detection.Option.DisplayName),
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
            ("entry", appliedOption.DisplayName),
            ("fps", $"{result.FramesPerSecond:0.###}"),
            ("round", RoundFrames),
            ("chapters", currentInfo.Chapters.Count));
        if (currentInfoBelongsToSelectedClip)
        {
            UpdateCurrentClipOption(storedInfo);
        }
        else if (IsClipCombineChecked)
        {
            UpdateCombinedClipOption(storedInfo);
        }
        RefreshRows();
        NotifyStateChanged();
    }

    private void ChangeFpsToSelectedOption()
    {
        if (currentInfo is null || !selectedFrameRateOption.IsValid)
        {
            return;
        }

        var sourceFps = configuredFrameRate ?? (decimal)currentInfo.FramesPerSecond;
        var targetOption = selectedFrameRateOption;
        var targetFps = targetOption.Value;
        var result = ChapterFpsTransformService.ChangeFps(currentInfo, sourceFps, targetFps);
        if (!result.Success)
        {
            SetStatus(null, diagnostic: result.Diagnostics.FirstOrDefault());
            LogDiagnostics(Localizer.GetString("Main.ChangeFps"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var beforeCount = currentInfo.Chapters.Count;
        currentInfo = result.Info;
        configuredFrameRate = targetFps;
        ApplyFrameInfo();
        SetStatus("Status.Updated");
        Log("Log.ChangeFps",
            ("sourceFps", $"{sourceFps:0.###}"),
            ("targetFps", $"{targetFps:0.###}"),
            ("before", beforeCount),
            ("after", result.Info.Chapters.Count));
        LogStatus();
        NotifyStateChanged();
    }

    private void UpdateCurrentClipOption(ChapterSet info)
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

        var entries = currentGroup.Entries.ToList();
        if (index >= entries.Count)
        {
            return;
        }

        var updatedOption = entries[index] with { ChapterSet = info };
        entries[index] = updatedOption;
        ClipOptions[index] = updatedOption;
        currentGroup = currentGroup with { Entries = entries };

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void UpdateCombinedClipOption(ChapterSet info)
    {
        if (currentGroup is null || !IsClipCombineChecked)
        {
            return;
        }

        var entry = combinedClipOption ?? ClipOptions.FirstOrDefault();
        if (entry is null)
        {
            return;
        }

        combinedClipOption = entry with { ChapterSet = info };
        currentGroup = currentGroup with { Entries = [combinedClipOption] };
        if (ClipOptions.Count == 1)
        {
            ClipOptions[0] = combinedClipOption;
        }

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void RestoreSplitClips()
    {
        if (splitClipGroup is null)
        {
            return;
        }

        var combinedChapterCount = combinedClipOption?.ChapterSet.Chapters.Count ?? currentInfo?.Chapters.Count ?? 0;
        currentGroup = splitClipGroup;
        splitClipGroup = null;
        combinedClipOption = null;
        IsClipCombineChecked = false;
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in currentGroup.Entries)
        {
            ClipOptions.Add(entry);
        }

        SelectClip(Math.Clamp(currentGroup.DefaultEntryIndex, 0, ClipOptions.Count - 1));
        SetStatus("Status.Updated");
        Log("Log.EditChapters",
            ("action", Localizer.Format(LocalizedMessage.Create("Action.SplitCombinedSegments", ("entries", currentGroup.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(currentGroup.Entries[0].ChapterSet.ImportFormat))))),
            ("before", combinedChapterCount),
            ("after", currentInfo?.Chapters.Count ?? 0));
        LogStatus();
        NotifyStateChanged();
    }

    private static ChapterImportEntry CreateCombinedClipOption(ChapterImportSource sourceGroup, ChapterSet combinedInfo)
    {
        var mediaReferences = sourceGroup.Entries
            .SelectMany(static entry => entry.ReferencedMediaFiles ?? [])
            .Distinct()
            .ToArray();
        return new ChapterImportEntry(
            "combined",
            $"{combinedInfo.Title}__{combinedInfo.Chapters.Count}",
            combinedInfo,
            CanCombine: true,
            ReferencedMediaFiles: mediaReferences);
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
                new ChapterSet(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []),
                [])
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
        SyncClipDisplayOptions(args);
        OnPropertyChanged(nameof(IsClipSelectionVisible));
        OnPropertyChanged(nameof(RelatedMediaReferences));
        OnPropertyChanged(nameof(SelectedClipIndex));
        OnPropertyChanged(nameof(SelectedClipDisplayOption));
        NotifyCommandStates();
    }

    private void SyncClipDisplayOptions(NotifyCollectionChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.NewItems is not null)
                {
                    var index = args.NewStartingIndex;
                    foreach (ChapterImportEntry entry in args.NewItems)
                    {
                        ClipDisplayOptions.Insert(index++, ToClipDisplayOption(entry));
                    }
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                if (args.OldItems is not null)
                {
                    for (var i = 0; i < args.OldItems.Count; i++)
                    {
                        ClipDisplayOptions.RemoveAt(args.OldStartingIndex);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (args.NewItems is not null)
                {
                    var index = args.NewStartingIndex;
                    foreach (ChapterImportEntry entry in args.NewItems)
                    {
                        ClipDisplayOptions[index++] = ToClipDisplayOption(entry);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Move:
                if (args is { OldStartingIndex: >= 0, NewStartingIndex: >= 0 })
                {
                    ClipDisplayOptions.Move(args.OldStartingIndex, args.NewStartingIndex);
                }

                break;
            default:
                RebuildClipDisplayOptions();
                break;
        }
    }

    private void RebuildClipDisplayOptions()
    {
        ClipDisplayOptions.Clear();
        foreach (var entry in ClipOptions)
        {
            ClipDisplayOptions.Add(ToClipDisplayOption(entry));
        }
    }

    private static SelectorDisplayOption ToClipDisplayOption(ChapterImportEntry entry)
    {
        var mainText = entry.DisplayName;
        var remarkParts = new List<string>();
        var markerIndex = entry.DisplayName.LastIndexOf("__", StringComparison.Ordinal);
        if (markerIndex > 0 && markerIndex + 2 < entry.DisplayName.Length)
        {
            mainText = entry.DisplayName[..markerIndex];
            remarkParts.Add($"{entry.DisplayName[(markerIndex + 2)..]} chapters");
        }
        else if (entry.ChapterSet.Chapters.Count > 0)
        {
            remarkParts.Add($"{entry.ChapterSet.Chapters.Count} chapters");
        }

        var remarkText = string.Join(", ", remarkParts.Where(static part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
        var displayText = string.IsNullOrWhiteSpace(remarkText) ? mainText : $"{mainText}（{remarkText}）";
        return new SelectorDisplayOption(mainText, remarkText, displayText);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsChapterGridEmpty));
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
        ChangeFpsCommand.RaiseCanExecuteChanged();
        SelectClipCommand.RaiseCanExecuteChanged();
        CombineCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        InsertCommand.RaiseCanExecuteChanged();
        OpenRelatedMediaCommand.RaiseCanExecuteChanged();
        PreviewCommand.RaiseCanExecuteChanged();
        LogCommand.RaiseCanExecuteChanged();
        SettingsCommand.RaiseCanExecuteChanged();
        ColorSettingsCommand.RaiseCanExecuteChanged();
        LanguageCommand.RaiseCanExecuteChanged();
        ExpressionCommand.RaiseCanExecuteChanged();
        TemplateNamesCommand.RaiseCanExecuteChanged();
        ZonesCommand.RaiseCanExecuteChanged();
        ForwardShiftCommand.RaiseCanExecuteChanged();
    }

    private static int ComboIndexFor(FrameRateOption entry)
    {
        if (entry.LegacyMplsCode == 0)
        {
            return 0;
        }

        return entry.IsValid ? entry.LegacyMplsCode : -1;
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

        return frameRateService.Options.FirstOrDefault(entry => entry.LegacyMplsCode == legacyCode);
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

    private void SetStatus(string? key, params (string Name, object? Value)[] arguments)
    {
        currentStatusMessage = key is null
            ? null
            : new LocalizedMessage(
                key,
                arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
        StatusText = currentStatusMessage is null ? string.Empty : Localizer.Format(currentStatusMessage);
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

    private void SetProgressStatus(ChapterImportProgressPhase? phase, params (string Name, object? Value)[] arguments)
    {
        currentStatusMessage = null;
        var messageKey = phase is null ? null : ProgressStatusKey(phase.Value);
        currentProgressMessage = messageKey is null
            ? null
            : new LocalizedMessage(
                messageKey,
                arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
        StatusText = currentProgressMessage is null ? string.Empty : Localizer.Format(currentProgressMessage);
    }

    private static string ProgressStatusKey(ChapterImportProgressPhase phase) => phase switch
    {
        ChapterImportProgressPhase.LoadingSource => "Status.LoadingSource",
        ChapterImportProgressPhase.ValidatingSource => "Status.LoadingSource.Validate",
        ChapterImportProgressPhase.DiscoveringTitles => "Status.LoadingSource.Discover",
        ChapterImportProgressPhase.ExportingChapters => "Status.LoadingSource.Export",
        ChapterImportProgressPhase.ParsingChapters => "Status.LoadingSource.Parse",
        _ => "Status.LoadingSource"
    };

    private string LocalizeDiagnostic(ChapterDiagnostic diagnostic)
    {
        var diagnosticKey = $"Diagnostic.{diagnostic.DisplayCode}";
        if (!Localizer.TryGetString(diagnosticKey, out var template))
        {
            return diagnostic.Message;
        }

        var arguments = diagnostic.Arguments;
        if (arguments is null && template.Contains("{message}", StringComparison.Ordinal))
        {
            arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = diagnostic.Message };
        }

        var formatted = Localizer.Format(diagnosticKey, arguments);
        // Strip any unresolved {token} placeholders that may remain when
        // the Arguments dictionary is missing keys expected by the template.
        return UnresolvedPlaceholderPattern().Replace(formatted, "[?]");
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex UnresolvedPlaceholderPattern();

    private void LogStatus(LogLevel level = LogLevel.Information) => Log(level, "Log.Status", ("status", StatusText));

    private void Log(string key, params (string Name, object? Value)[] arguments) =>
        Log(LogLevel.Information, key, technicalDetail: null, arguments);

    private void Log(LogLevel level, string key, params (string Name, object? Value)[] arguments)
    {
        Log(level, key, technicalDetail: null, arguments);
    }

    private void Log(LogLevel level, string key, string? technicalDetail, params (string Name, object? Value)[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var state = arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal);
        state["MessageKey"] = key.Trim();
        if (!string.IsNullOrWhiteSpace(technicalDetail))
        {
            state["TechnicalDetail"] = technicalDetail;
        }

        logger.Log(
            level,
            new EventId(0, key.Trim()),
            state,
            exception: null,
            static (values, _) => values.TryGetValue("MessageKey", out var value) ? value?.ToString() ?? string.Empty : string.Empty);
    }

    private string FormatLogEntry(ApplicationLogEntry entry)
    {
        if (entry.MessageKey is null)
        {
            return entry.Message;
        }

        var message = Localizer.Format(entry.MessageKey, entry.Arguments);
        return string.IsNullOrWhiteSpace(entry.TechnicalDetail)
            ? message
            : $"{message} {entry.TechnicalDetail}";
    }

    private void RefreshLocalizedState()
    {
        RefreshChapterNameModeOptions();
        RefreshXmlLanguageDisplayOptions(notify: true);

        if (string.IsNullOrEmpty(chapterNameTemplateText))
        {
            ChapterNameTemplateStatus = Localizer.GetString("Status.TemplateNotSelected");
        }

        if (currentStatusMessage is not null)
        {
            StatusText = Localizer.Format(currentStatusMessage);
            return;
        }

        if (currentProgressMessage is not null)
        {
            StatusText = Localizer.Format(currentProgressMessage);
        }
    }

    private void RefreshXmlLanguageDisplayOptions(bool notify)
    {
        var entries = XmlLanguageDisplay.Options(Localizer);
        if (xmlLanguageDisplayOptions.Count != entries.Count)
        {
            xmlLanguageDisplayOptions.Clear();
            foreach (var entry in entries)
            {
                xmlLanguageDisplayOptions.Add(entry);
            }
        }
        else
        {
            for (var index = 0; index < entries.Count; index++)
            {
                xmlLanguageDisplayOptions[index].UpdateFrom(entries[index]);
            }
        }

        if (notify)
        {
            OnPropertyChanged(nameof(XmlLanguageDisplayOptions));
            OnPropertyChanged(nameof(SelectedXmlLanguageDisplayOption));
        }
    }

    private void RefreshChapterNameModeOptions()
    {
        var entries = new[]
        {
            new SelectorDisplayOption("keep-original", string.Empty, Localizer.GetString("Main.KeepOriginalName")),
            new SelectorDisplayOption("standard-template", string.Empty, Localizer.GetString("Main.StandardTemplate")),
            new SelectorDisplayOption("template-file", string.Empty, Localizer.GetString("Main.TemplateFile"))
        };

        isRefreshingChapterNameModeOptions = true;
        try
        {
            if (ChapterNameModeOptions.Count != entries.Length)
            {
                ChapterNameModeOptions.Clear();
                foreach (var entry in entries)
                {
                    ChapterNameModeOptions.Add(entry);
                }
            }
            else
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    ChapterNameModeOptions[index].UpdateFrom(entries[index]);
                }
            }
        }
        finally
        {
            isRefreshingChapterNameModeOptions = false;
        }

        OnPropertyChanged(nameof(ChapterNameModeIndex));
    }

    private void LogImportSummary(string operation, ChapterImportResult result)
    {
        var entryCount = result.Groups.Sum(static group => group.Entries.Count);
        var chapterCount = result.Groups
            .SelectMany(static group => group.Entries)
            .Sum(static entry => entry.ChapterSet.Chapters.Count);
        Log(result.Success ? LogLevel.Information : LogLevel.Error, "Log.ImportSummary",
            ("operation", operation),
            ("success", result.Success),
            ("partial", result.IsPartial),
            ("groups", result.Groups.Count),
            ("entries", entryCount),
            ("chapters", chapterCount),
            ("diagnostics", result.Diagnostics.Count));
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            Log("Log.ImportGroup",
                ("operation", operation),
                ("groupIndex", groupIndex + 1),
                ("sourcePath", group.SourcePath),
                ("defaultEntryIndex", group.DefaultEntryIndex),
                ("entries", group.Entries.Count));
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var info = entry.ChapterSet;
                Log("Log.ImportEntry",
                    ("operation", operation),
                    ("entryIndex", entryIndex + 1),
                    ("id", entry.Id),
                    ("label", entry.DisplayName),
                    ("source", info.SourceName ?? string.Empty),
                    ("sourceType", ChapterImportFormats.DisplayName(info.ImportFormat)),
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
            Log(LogLevelFor(diagnostic.Severity), "Log.Diagnostic",
                diagnostic.Details,
                ("operation", operation),
                ("severity", diagnostic.Severity),
                ("code", diagnostic.DisplayCode),
                ("location", location),
                ("message", LocalizeDiagnostic(diagnostic)),
                ("details", details));
        }
    }

    private static LogLevel LogLevelFor(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => LogLevel.Error,
            DiagnosticSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };

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

    private sealed class ChapterImportProgressSink(Action<ChapterImportProgress> handler) : IChapterImportProgressReporter
    {
        public void Report(ChapterImportProgress progress) => handler(progress);
    }
}

public sealed record ChapterCellEdit(int Index, string Value);

public sealed class SelectorDisplayOption(string mainText, string remarkText, string displayText) : ObservableViewModel
{
    private string mainText = mainText;
    private string remarkText = remarkText;
    private string displayText = displayText;

    public string MainText
    {
        get => mainText;
        private set => SetProperty(ref mainText, value);
    }

    public string RemarkText
    {
        get => remarkText;
        private set => SetProperty(ref remarkText, value);
    }

    public string DisplayText
    {
        get => displayText;
        private set => SetProperty(ref displayText, value);
    }

    public void UpdateFrom(SelectorDisplayOption entry)
    {
        MainText = entry.MainText;
        RemarkText = entry.RemarkText;
        DisplayText = entry.DisplayText;
    }

    public override string ToString() => DisplayText;
}
