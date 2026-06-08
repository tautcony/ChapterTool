using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ChapterTool.Avalonia.Services;
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
    private readonly IApplicationLogService logService;
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
    private int orderShift;
    private bool applyExpression;
    private string expression = "t";
    private string? saveDirectory;
    private string statusText = "Ready";
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
        IFrameRateService? frameRateService = null)
    {
        this.loadService = loadService;
        this.saveService = saveService;
        this.editingService = editingService;
        this.segmentService = segmentService;
        this.windowService = windowService;
        this.formatter = formatter;
        this.frameRateService = frameRateService ?? new FrameRateService();
        this.logService = logService ?? new InMemoryApplicationLogService();
        this.shellService = shellService;
        this.appSettingsStore = appSettingsStore;
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
                ApplyEdit(editingService.Delete(currentInfo, indexes));
            }

            return ValueTask.CompletedTask;
        }, _ => currentInfo is not null);
        InsertCommand = new UiCommand(parameter =>
        {
            if (currentInfo is not null)
            {
                ApplyEdit(editingService.InsertBefore(currentInfo, parameter is int index ? index : Rows.Count));
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
        private set
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

    public string UiLanguage
    {
        get => uiLanguage;
        private set => SetProperty(ref uiLanguage, value);
    }

    public bool AutoGenerateNames
    {
        get => autoGenerateNames;
        set => SetProperty(ref autoGenerateNames, value);
    }

    public bool UseTemplateNames
    {
        get => useTemplateNames;
        set => SetProperty(ref useTemplateNames, value);
    }

    public int OrderShift
    {
        get => orderShift;
        set => SetProperty(ref orderShift, value);
    }

    public bool ApplyExpression
    {
        get => applyExpression;
        set => SetProperty(ref applyExpression, value);
    }

    public string Expression
    {
        get => expression;
        set => SetProperty(ref expression, value);
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
        UiLanguage = settings.Language;
        logService.Add("Settings loaded");
        NotifyStateChanged();
    }

    public async ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
    {
        UiLanguage = language;
        if (appSettingsStore is null)
        {
            return;
        }

        var current = await appSettingsStore.LoadAsync(cancellationToken);
        await appSettingsStore.SaveAsync(current with { Language = language }, cancellationToken);
        logService.Add($"Language set to {(string.IsNullOrWhiteSpace(language) ? "default" : language)}");
        NotifyStateChanged();
    }

    public string BuildPreview()
    {
        if (currentInfo is null)
        {
            return string.Empty;
        }

        var options = CurrentExportOptions();
        var result = new ChapterExportService(formatter, new ExpressionService()).Export(currentInfo, options);
        if (!result.Success)
        {
            return string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.Message));
        }

        return result.Content;
    }

    public string LogText() => logService.Format();

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
        StatusText = result.Diagnostics.Count == 0 ? "Zones generated" : result.Diagnostics[0].Message;
        logService.Add(StatusText);
        NotifyStateChanged();
        return result.Zones;
    }

    public void ShiftFramesForward(int frames)
    {
        if (currentInfo is null)
        {
            return;
        }

        ApplyEdit(editingService.ShiftFramesForward(currentInfo, frames, (decimal)currentInfo.FramesPerSecond));
    }

    public ChapterExportOptions CurrentExportOptions() =>
        new(
            SaveFormat,
            XmlLanguage,
            currentInfo?.SourceName,
            AutoGenerateNames,
            UseTemplateNames,
            OrderShift,
            ApplyExpression,
            Expression);

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "No source selected";
            logService.Add(StatusText);
            NotifyStateChanged();
            return;
        }

        logService.Add($"Loading {path}");
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (!result.Success || result.Groups.Count == 0)
        {
            StatusText = result.Diagnostics.FirstOrDefault()?.Message ?? "Load failed";
            Progress = 0;
            logService.Add(StatusText);
            NotifyStateChanged();
            return;
        }

        CurrentPath = path;
        DisplayPath = Path.GetFileName(path);
        currentGroup = result.Groups[0];
        currentInfoBelongsToSelectedClip = false;
        ClipOptions.Clear();
        foreach (var option in currentGroup.Options)
        {
            ClipOptions.Add(option);
        }

        SelectClip(Math.Clamp(currentGroup.DefaultOptionIndex, 0, ClipOptions.Count - 1));
        StatusText = $"Loaded {Rows.Count} chapters";
        Progress = 1;
        logService.Add($"{StatusText} from {path}");
        NotifyStateChanged();
    }

    private async ValueTask SaveAsync(string? directory, CancellationToken cancellationToken)
    {
        if (currentInfo is null)
        {
            return;
        }

        var options = CurrentExportOptions();
        var result = await saveService.SaveAsync(currentInfo, options, directory, cancellationToken);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            SaveDirectory = directory;
            if (appSettingsStore is not null)
            {
                var current = await appSettingsStore.LoadAsync(cancellationToken);
                await appSettingsStore.SaveAsync(current with { SavingPath = directory }, cancellationToken);
            }
        }

        StatusText = result.Success ? "Saved" : result.Diagnostics.FirstOrDefault()?.Message ?? "Save failed";
        var detail = result.Diagnostics.LastOrDefault()?.Message;
        logService.Add(string.IsNullOrWhiteSpace(detail) ? StatusText : $"{StatusText}: {detail}");
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
        ApplyEdit(result);
        return ValueTask.CompletedTask;
    }

    private void CombineSegments()
    {
        if (currentGroup is null)
        {
            return;
        }

        var result = segmentService.Combine(currentGroup);
        ApplyEdit(result);
    }

    private async ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken)
    {
        if (currentGroup is null)
        {
            StatusText = "No current MPLS group is loaded";
            logService.Add(StatusText);
            NotifyStateChanged();
            return;
        }

        logService.Add($"Appending MPLS {path}");
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (!result.Success || result.Groups.Count == 0)
        {
            StatusText = result.Diagnostics.FirstOrDefault()?.Message ?? "Append failed";
            logService.Add(StatusText);
            NotifyStateChanged();
            return;
        }

        var edit = segmentService.Append(currentGroup, result.Groups[0]);
        if (edit.Diagnostics.Count > 0)
        {
            StatusText = edit.Diagnostics[0].Message;
            logService.Add(StatusText);
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
        StatusText = $"Appended {result.Groups[0].Options.Count} MPLS segment(s)";
        logService.Add(StatusText);
        NotifyStateChanged();
    }

    private void ApplyEdit(ChapterEditResult result)
    {
        currentInfo = result.ChapterInfo;
        ApplyFrameInfo();
        StatusText = result.Diagnostics.Count == 0 ? "Updated" : result.Diagnostics[0].Message;
        logService.Add(StatusText);
        NotifyStateChanged();
    }

    private void ApplyFrameInfo()
    {
        if (currentInfo is null)
        {
            RefreshRows();
            return;
        }

        var option = selectedFrameRateOption.LegacyMplsCode == 0
            ? frameRateService.FindByValue((decimal)currentInfo.FramesPerSecond)
            : selectedFrameRateOption;
        var result = frameRateService.UpdateFrames(currentInfo, option, RoundFrames, tolerance: 0.01m);
        currentInfo = result.Info;
        selectedFrameRateOption = result.SelectedOption;
        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        if (currentInfoBelongsToSelectedClip)
        {
            UpdateCurrentClipOption(currentInfo);
        }
        RefreshRows();
        NotifyStateChanged();
    }

    private void UpdateCurrentClipOption(ChapterInfo info)
    {
        if (currentGroup is null || SelectedClipIndex < 0 || SelectedClipIndex >= ClipOptions.Count)
        {
            return;
        }

        var updatedOption = ClipOptions[SelectedClipIndex] with { ChapterInfo = info };
        ClipOptions[SelectedClipIndex] = updatedOption;

        var options = currentGroup.Options.ToArray();
        if (SelectedClipIndex < options.Length)
        {
            options[SelectedClipIndex] = updatedOption;
            currentGroup = currentGroup with { Options = options };
        }

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void RefreshRows()
    {
        Rows.Clear();
        if (currentInfo is null)
        {
            return;
        }

        foreach (var chapter in currentInfo.Chapters)
        {
            Rows.Add(new ChapterRowViewModel(chapter, formatter));
        }
    }

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

    private static int ComboIndexFor(FrameRateOption option) =>
        option.LegacyMplsCode > 0 ? option.LegacyMplsCode - 1 : -1;

    private FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
    {
        var legacyCode = frameRateIndex >= 0 ? frameRateIndex + 1 : 0;
        if (legacyCode == 5)
        {
            legacyCode = 4;
        }

        return frameRateService.Options.FirstOrDefault(option => option.LegacyMplsCode == legacyCode && option.IsValid);
    }

    private UiCommand WindowCommand(string id) =>
        new(async (_, token) => await windowService.ShowAsync(id, this, token));

    private async ValueTask OpenRelatedMediaAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (shellService is null)
        {
            StatusText = "Shell service is unavailable";
            logService.Add(StatusText);
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
            StatusText = "Related media file was not found";
            logService.Add(StatusText);
            NotifyStateChanged();
            return;
        }

        await shellService.OpenAsync(target, cancellationToken);
        StatusText = $"Opened {Path.GetFileName(target)}";
        logService.Add(StatusText);
        NotifyStateChanged();
    }

    private enum EditKind
    {
        Time,
        Name,
        Frame
    }
}

public sealed record ChapterCellEdit(int Index, string Value);
