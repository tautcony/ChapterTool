using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class SettingsToolViewModel : ObservableViewModel
{
    private static readonly ChapterExportFormat[] SaveFormats =
    [
        ChapterExportFormat.Txt,
        ChapterExportFormat.Xml,
        ChapterExportFormat.Qpfile,
        ChapterExportFormat.TimeCodes,
        ChapterExportFormat.TsMuxerMeta,
        ChapterExportFormat.Cue,
        ChapterExportFormat.Json,
        ChapterExportFormat.WebVtt,
        ChapterExportFormat.Celltimes,
        ChapterExportFormat.Chapter2Qpfile
    ];

    private readonly MainWindowViewModel owner;
    private readonly ISettingsStore<AppSettings>? appSettingsStore;
    private readonly ISettingsStore<ThemeColorSettings>? themeSettingsStore;
    private readonly IAppLocalizer localizer;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private readonly ISettingsPickerService? picker;
    private readonly IExternalToolLocator? externalToolLocator;
    private readonly IThemeApplicationService? themeApplicationService;
    private readonly IShellService? shellService;
    private AppSettings savedAppSettings = new();
    private ThemeColorSettings savedThemeSettings = ThemeColorSettings.Default;
    private string selectedLanguage;
    private int defaultSaveFormatIndex;
    private int defaultXmlLanguageIndex;
    private decimal frameAccuracyTolerance;
    private double frameAccuracyToleranceSliderValue;
    private bool liveApplyEnabled;
    private bool isApplyingSnapshot;
    private bool isRefreshingLanguages;
    private readonly ObservableCollection<SelectorDisplayOption> xmlLanguageDisplayOptions = [];

    public SettingsToolViewModel(
        MainWindowViewModel owner,
        ISettingsStore<AppSettings>? appSettingsStore,
        ISettingsStore<ThemeColorSettings>? themeSettingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null,
        IExternalToolLocator? externalToolLocator = null,
        IThemeApplicationService? themeApplicationService = null,
        IShellService? shellService = null,
        bool autoLoad = true)
    {
        this.owner = owner;
        this.appSettingsStore = appSettingsStore;
        this.themeSettingsStore = themeSettingsStore;
        this.localizer = localizer ?? owner.Localizer;
        this.picker = picker;
        this.externalToolLocator = externalToolLocator;
        this.themeApplicationService = themeApplicationService;
        this.shellService = shellService;
        selectedLanguage = AppLanguage.Normalize(owner.UiLanguage);
        defaultSaveFormatIndex = Math.Clamp(owner.SaveFormatIndex, 0, SaveFormats.Length - 1);
        defaultXmlLanguageIndex = XmlLanguageIndex(owner.XmlLanguage);
        frameAccuracyTolerance = MainWindowViewModel.NormalizeFrameAccuracyTolerance(owner.FrameAccuracyTolerance);
        frameAccuracyToleranceSliderValue = (double)frameAccuracyTolerance;
        ReplaceLanguages(BuildLanguageOptions());
        RefreshXmlLanguageDisplayOptions(notify: false);
        ColorSlots = new ObservableCollection<ColorSlotViewModel>(
            ThemeColorSettings.Default.OrderedSlots.Select(static slot => new ColorSlotViewModel(slot.Name, slot.Value)));
        foreach (var slot in ColorSlots)
        {
            slot.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ColorSlotViewModel.Value))
                {
                    ApplyCurrentTheme();
                    NotifyUnsavedChanges();
                }
            };
        }

        SaveCommand = new UiCommand(async (_, token) => await SaveAsync(token), _ => appSettingsStore is not null || themeSettingsStore is not null);
        ResetCommand = new UiCommand((_, _) =>
        {
            ApplyDefaults();
            return ValueTask.CompletedTask;
        });
        ValidateToolsCommand = new UiCommand(async (_, token) => await DiscoverAndFillToolPathsAsync(token));
        BrowseSaveDirectoryCommand = new UiCommand(async (_, token) => await PickDirectoryAsync(value => SaveDirectory = value, token));
        BrowseMkvToolnixCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => MkvToolnixPath = value, token));
        BrowseEac3toCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => Eac3toPath = value, token));
        BrowseFfprobeCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => FfprobePath = value, token));
        BrowseFfmpegCommand = new UiCommand(async (_, token) => await PickDirectoryAsync(value => FfmpegPath = value, token));
        ClearSaveDirectoryCommand = ClearCommand(() => SaveDirectory = null);
        ClearMkvToolnixCommand = ClearCommand(() => MkvToolnixPath = null);
        ClearEac3toCommand = ClearCommand(() => Eac3toPath = null);
        ClearFfprobeCommand = ClearCommand(() => FfprobePath = null);
        ClearFfmpegCommand = ClearCommand(() => FfmpegPath = null);
        OpenRepositoryCommand = new UiCommand(async (_, token) => await OpenRepositoryAsync(token), _ => shellService is not null);
        this.localizer.CultureChanged += (_, _) =>
        {
            RefreshLanguages();
            RefreshXmlLanguageDisplayOptions(notify: true);
            RefreshToolStatuses();
            if (!string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = this.localizer.GetString("Settings.Status.Ready");
            }
        };
        if (autoLoad)
        {
            _ = InitializeAsync();
        }
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages => languages;

    public IReadOnlyList<string> SaveFormatOptions { get; } = SaveFormats.Select(ChapterExportFormatDisplay.LabelFor).ToArray();

    public IReadOnlyList<string> XmlLanguageOptions { get; } =
        XmlChapterLanguageCatalog.Languages.Select(static language => language.Code).ToList();

    public IReadOnlyList<SelectorDisplayOption> XmlLanguageDisplayOptions => xmlLanguageDisplayOptions;

    public string AvaloniaRuntimeDisplay { get; } = $"Avalonia v{InformationalVersion(typeof(Application))}";

    public string DotNetRuntimeDisplay { get; } = $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}";

    public SelectorDisplayOption? SelectedDefaultXmlLanguageDisplayOption
    {
        get
        {
            var options = XmlLanguageDisplayOptions;
            return DefaultXmlLanguageIndex < 0 || DefaultXmlLanguageIndex >= options.Count
                ? null
                : options[DefaultXmlLanguageIndex];
        }
        set
        {
            var index = value is null
                ? -1
                : XmlLanguageDisplayOptions.ToList().FindIndex(option =>
                    string.Equals(option.MainText, value.MainText, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                DefaultXmlLanguageIndex = index;
            }
        }
    }

    public ObservableCollection<ColorSlotViewModel> ColorSlots { get; }

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (SetProperty(ref selectedLanguage, AppLanguage.Normalize(value)))
            {
                OnPropertyChanged(nameof(SelectedLanguageIndex));
                ApplyLiveSettings();
            }
        }
    }

    public int SelectedLanguageIndex
    {
        get
        {
            var index = Languages.ToList().FindIndex(option => string.Equals(option.CultureName, SelectedLanguage, StringComparison.OrdinalIgnoreCase));
            return index;
        }
        set
        {
            if (isRefreshingLanguages)
            {
                return;
            }

            if (value >= 0 && value < Languages.Count)
            {
                SelectedLanguage = Languages[value].CultureName;
            }
        }
    }

    public string? SaveDirectory
    {
        get;
        set
        {
            if (SetProperty(ref field, CleanPath(value)))
            {
                ApplyLiveSettings();
            }
        }
    }

    public string? MkvToolnixPath
    {
        get;
        set
        {
            if (SetProperty(ref field, CleanPath(value)))
            {
                MkvToolnixStatus = FormatToolStatus(ValidateTool(value, "mkvextract"));
                NotifyUnsavedChanges();
            }
        }
    }

    public string? Eac3toPath
    {
        get;
        set
        {
            if (SetProperty(ref field, CleanPath(value)))
            {
                Eac3toStatus = FormatToolStatus(ValidateTool(value, "eac3to"));
                NotifyUnsavedChanges();
            }
        }
    }

    public string? FfprobePath
    {
        get;
        set
        {
            if (SetProperty(ref field, CleanPath(value)))
            {
                FfprobeStatus = FormatToolStatus(ValidateTool(value, "ffprobe"));
                NotifyUnsavedChanges();
            }
        }
    }

    public string? FfmpegPath
    {
        get;
        set
        {
            if (SetProperty(ref field, CleanPath(value)))
            {
                FfmpegStatus = FormatToolStatus(ValidateToolDirectory(value, "ffprobe"));
                NotifyUnsavedChanges();
            }
        }
    }

    public int DefaultSaveFormatIndex
    {
        get => defaultSaveFormatIndex;
        set
        {
            if (SetProperty(ref defaultSaveFormatIndex, Math.Clamp(value, 0, SaveFormats.Length - 1)))
            {
                ApplyLiveSettings();
            }
        }
    }

    public int DefaultXmlLanguageIndex
    {
        get => defaultXmlLanguageIndex;
        set
        {
            if (SetProperty(ref defaultXmlLanguageIndex, Math.Clamp(value, 0, XmlLanguageOptions.Count - 1)))
            {
                OnPropertyChanged(nameof(SelectedDefaultXmlLanguageDisplayOption));
                ApplyLiveSettings();
            }
        }
    }

    public decimal FrameAccuracyTolerance
    {
        get => frameAccuracyTolerance;
        set => SetFrameAccuracyTolerance(value, updateSlider: true);
    }

    public double FrameAccuracyToleranceSliderValue
    {
        get => frameAccuracyToleranceSliderValue;
        set
        {
            var bounded = Math.Clamp(value, 0.01d, 0.30d);
            SetProperty(ref frameAccuracyToleranceSliderValue, bounded);
            SetFrameAccuracyTolerance((decimal)bounded, updateSlider: false);
        }
    }

    public string FrameAccuracyToleranceDisplayText =>
        FrameAccuracyTolerance.ToString("0.###", CultureInfo.InvariantCulture);

    public bool HasUnsavedChanges =>
        appSettingsStore is not null && CurrentAppSettings() != savedAppSettings
        || themeSettingsStore is not null && CurrentThemeSettings() != savedThemeSettings;

    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string MkvToolnixStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string Eac3toStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string FfprobeStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string FfmpegStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public UiCommand SaveCommand { get; }

    public UiCommand ResetCommand { get; }

    public UiCommand ValidateToolsCommand { get; }

    public UiCommand BrowseSaveDirectoryCommand { get; }

    public UiCommand BrowseMkvToolnixCommand { get; }

    public UiCommand BrowseEac3toCommand { get; }

    public UiCommand BrowseFfprobeCommand { get; }

    public UiCommand BrowseFfmpegCommand { get; }

    public UiCommand ClearSaveDirectoryCommand { get; }

    public UiCommand ClearMkvToolnixCommand { get; }

    public UiCommand ClearEac3toCommand { get; }

    public UiCommand ClearFfprobeCommand { get; }

    public UiCommand ClearFfmpegCommand { get; }

    public UiCommand OpenRepositoryCommand { get; }

    public async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        liveApplyEnabled = false;
        if (appSettingsStore is not null)
        {
            var settings = await appSettingsStore.LoadAsync(cancellationToken);
            ApplyAppSettingsToFields(settings);
            savedAppSettings = CurrentAppSettings();
        }

        if (themeSettingsStore is not null)
        {
            var theme = await themeSettingsStore.LoadAsync(cancellationToken);
            savedThemeSettings = NormalizeThemeSettings(theme);
            ApplyColors(theme);
            themeApplicationService?.Apply(theme);
        }

        liveApplyEnabled = true;
        ApplyCurrentAppSettingsToOwner();
        RefreshToolStatuses();
        NotifyUnsavedChanges();
        StatusText = localizer.GetString("Settings.Status.Ready");
    }

    private async Task InitializeAsync()
    {
        await LoadAsync(CancellationToken.None);
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (appSettingsStore is not null)
        {
            var settings = CurrentAppSettings();
            await appSettingsStore.SaveAsync(settings, cancellationToken);
            savedAppSettings = settings;
        }

        if (themeSettingsStore is not null)
        {
            var settings = CurrentThemeSettings();
            await themeSettingsStore.SaveAsync(settings, cancellationToken);
            savedThemeSettings = settings;
            ApplyColors(settings);
            themeApplicationService?.Apply(settings);
        }

        RefreshToolStatuses();
        NotifyUnsavedChanges();
        StatusText = localizer.GetString("Settings.Status.Saved");
    }

    public void DiscardUnsavedAppearanceChanges()
    {
        if (CurrentThemeSettings() == savedThemeSettings)
        {
            return;
        }

        ApplyColors(savedThemeSettings);
        themeApplicationService?.Apply(savedThemeSettings);
        NotifyUnsavedChanges();
    }

    public void DiscardUnsavedChanges()
    {
        isApplyingSnapshot = true;
        try
        {
            ApplyAppSettingsToFields(savedAppSettings);
            ApplyColors(savedThemeSettings);
        }
        finally
        {
            isApplyingSnapshot = false;
        }

        ApplyCurrentAppSettingsToOwner();
        themeApplicationService?.Apply(savedThemeSettings);
        RefreshToolStatuses();
        NotifyUnsavedChanges();
    }

    private void ApplyDefaults()
    {
        var defaults = new AppSettings();
        SelectedLanguage = defaults.Language;
        SaveDirectory = defaults.SavingPath;
        MkvToolnixPath = defaults.MkvToolnixPath;
        Eac3toPath = defaults.Eac3toPath;
        FfprobePath = defaults.FfprobePath;
        FfmpegPath = defaults.FfmpegPath;
        DefaultSaveFormatIndex = SaveFormatIndex(defaults.DefaultSaveFormat);
        DefaultXmlLanguageIndex = XmlLanguageIndex(defaults.DefaultXmlLanguage);
        FrameAccuracyTolerance = defaults.FrameAccuracyTolerance;
        ApplyColors(ThemeColorSettings.Default);
        themeApplicationService?.Apply(ThemeColorSettings.Default);
        ApplyLiveSettings();
        RefreshToolStatuses();
        StatusText = localizer.GetString("Settings.Status.Reset");
    }

    private async ValueTask PickDirectoryAsync(Action<string> apply, CancellationToken cancellationToken)
    {
        if (picker is null)
        {
            return;
        }

        var path = await picker.PickDirectoryAsync(localizer.GetString("Settings.BrowseDirectory"), cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            apply(path);
        }
    }

    private async ValueTask PickExecutableAsync(Action<string> apply, CancellationToken cancellationToken)
    {
        if (picker is null)
        {
            return;
        }

        var path = await picker.PickExecutableAsync(localizer.GetString("Settings.BrowseExecutable"), cancellationToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            apply(path);
        }
    }

    private static UiCommand ClearCommand(Action clear) =>
        new((_, _) =>
        {
            clear();
            return ValueTask.CompletedTask;
        });

    private async ValueTask OpenRepositoryAsync(CancellationToken cancellationToken)
    {
        if (shellService is not null)
        {
            await shellService.OpenAsync("https://github.com/tautcony/ChapterTool", cancellationToken);
        }
    }

    private void RefreshLanguages()
    {
        isRefreshingLanguages = true;
        try
        {
            ReplaceLanguages(BuildLanguageOptions());
            OnPropertyChanged(nameof(Languages));
        }
        finally
        {
            isRefreshingLanguages = false;
        }

        OnPropertyChanged(nameof(SelectedLanguageIndex));
    }

    private void RefreshXmlLanguageDisplayOptions(bool notify)
    {
        var options = XmlLanguageDisplay.Options(localizer);
        if (xmlLanguageDisplayOptions.Count != options.Count)
        {
            xmlLanguageDisplayOptions.Clear();
            foreach (var option in options)
            {
                xmlLanguageDisplayOptions.Add(option);
            }
        }
        else
        {
            for (var index = 0; index < options.Count; index++)
            {
                xmlLanguageDisplayOptions[index].UpdateFrom(options[index]);
            }
        }

        if (notify)
        {
            OnPropertyChanged(nameof(XmlLanguageDisplayOptions));
            OnPropertyChanged(nameof(SelectedDefaultXmlLanguageDisplayOption));
        }
    }

    private List<LanguageOptionViewModel> BuildLanguageOptions() =>
        localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                localizer.GetString(language.DisplayNameKey)))
            .ToList();

    private void ReplaceLanguages(IReadOnlyList<LanguageOptionViewModel> options)
    {
        languages.Clear();
        foreach (var option in options)
        {
            languages.Add(option);
        }
    }

    private void RefreshToolStatuses()
    {
        MkvToolnixStatus = FormatToolStatus(ValidateTool(MkvToolnixPath, "mkvextract"));
        Eac3toStatus = FormatToolStatus(ValidateTool(Eac3toPath, "eac3to"));
        FfprobeStatus = FormatToolStatus(ValidateTool(FfprobePath, "ffprobe"));
        FfmpegStatus = FormatToolStatus(ValidateToolDirectory(FfmpegPath, "ffprobe"));
    }

    private async ValueTask DiscoverAndFillToolPathsAsync(CancellationToken cancellationToken)
    {
        if (externalToolLocator is null)
        {
            RefreshToolStatuses();
            return;
        }

        MkvToolnixPath = await DiscoverExecutableAsync("mkvextract", MkvToolnixPath, cancellationToken);
        Eac3toPath = await DiscoverExecutableAsync("eac3to", Eac3toPath, cancellationToken);
        var ffprobe = await DiscoverExecutableAsync("ffprobe", FfprobePath, cancellationToken);
        FfprobePath = ffprobe;

        if (string.IsNullOrWhiteSpace(FfmpegPath) || ValidateToolDirectory(FfmpegPath, "ffprobe").Kind != SettingsToolStatusKind.Found)
        {
            var ffprobeDirectory = string.IsNullOrWhiteSpace(ffprobe) ? null : Path.GetDirectoryName(ffprobe);
            FfmpegPath = string.IsNullOrWhiteSpace(ffprobeDirectory) ? FfmpegPath : ffprobeDirectory;
        }

        RefreshToolStatuses();
    }

    private async ValueTask<string?> DiscoverExecutableAsync(string toolId, string? currentPath, CancellationToken cancellationToken)
    {
        var current = ValidateTool(currentPath, toolId);
        if (current.Kind == SettingsToolStatusKind.Found)
        {
            return current.ResolvedPath ?? currentPath;
        }

        var location = await externalToolLocator!.LocateAsync(toolId, cancellationToken);
        return location.Found ? location.Path : currentPath;
    }

    private string FormatToolStatus(SettingsToolStatus status) =>
        status.Kind switch
        {
            SettingsToolStatusKind.Discovery => localizer.GetString("Settings.ToolStatus.Discovery"),
            SettingsToolStatusKind.Found => localizer.Format("Settings.ToolStatus.Found", new Dictionary<string, object?> { ["path"] = status.ResolvedPath }),
            SettingsToolStatusKind.Missing => localizer.Format("Settings.ToolStatus.Missing", new Dictionary<string, object?> { ["name"] = status.ExpectedExecutable }),
            SettingsToolStatusKind.InvalidPath => localizer.GetString("Settings.ToolStatus.InvalidPath"),
            SettingsToolStatusKind.NotDirectory => localizer.GetString("Settings.ToolStatus.NotDirectory"),
            _ => localizer.GetString("Settings.ToolStatus.Unsupported")
        };

    private static SettingsToolStatus ValidateTool(string? configuredPath, string toolId)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new SettingsToolStatus(
                SettingsToolStatusKind.Discovery,
                null,
                ExternalToolPathResolver.ExecutableName(toolId));
        }

        var text = configuredPath.Trim();
        var executableName = ExternalToolPathResolver.ExecutableName(toolId);
        if (Directory.Exists(text))
        {
            var candidate = Path.Combine(text, executableName);
            return File.Exists(candidate)
                ? new SettingsToolStatus(SettingsToolStatusKind.Found, candidate, executableName)
                : new SettingsToolStatus(SettingsToolStatusKind.Missing, candidate, executableName);
        }

        return File.Exists(text)
            ? new SettingsToolStatus(SettingsToolStatusKind.Found, text, executableName)
            : new SettingsToolStatus(SettingsToolStatusKind.InvalidPath, text, executableName);
    }

    private static SettingsToolStatus ValidateToolDirectory(string? configuredPath, string toolId)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new SettingsToolStatus(
                SettingsToolStatusKind.Discovery,
                null,
                ExternalToolPathResolver.ExecutableName(toolId));
        }

        var text = configuredPath.Trim();
        var executableName = ExternalToolPathResolver.ExecutableName(toolId);
        if (!Directory.Exists(text))
        {
            return File.Exists(text)
                ? new SettingsToolStatus(SettingsToolStatusKind.NotDirectory, text, executableName)
                : new SettingsToolStatus(SettingsToolStatusKind.InvalidPath, text, executableName);
        }

        var candidate = Path.Combine(text, executableName);
        return File.Exists(candidate)
            ? new SettingsToolStatus(SettingsToolStatusKind.Found, candidate, executableName)
            : new SettingsToolStatus(SettingsToolStatusKind.Missing, candidate, executableName);
    }

    private ThemeColorSettings CurrentThemeSettings()
    {
        var defaults = ThemeColorSettings.Default.OrderedSlots.ToList();
        var values = ColorSlots.Select((slot, index) => NormalizeColor(slot.Value, defaults[index].Value)).ToList();
        return new ThemeColorSettings(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private AppSettings CurrentAppSettings() =>
        savedAppSettings with
        {
            Language = SelectedLanguage,
            SavingPath = SaveDirectory,
            MkvToolnixPath = MkvToolnixPath,
            Eac3toPath = Eac3toPath,
            FfprobePath = FfprobePath,
            FfmpegPath = FfmpegPath,
            DefaultSaveFormat = SaveFormats[DefaultSaveFormatIndex].ToString(),
            DefaultXmlLanguage = XmlLanguageOptions[DefaultXmlLanguageIndex],
            FrameAccuracyTolerance = FrameAccuracyTolerance
        };

    private void ApplyAppSettingsToFields(AppSettings settings)
    {
        SelectedLanguage = settings.Language;
        SaveDirectory = settings.SavingPath;
        MkvToolnixPath = settings.MkvToolnixPath;
        Eac3toPath = settings.Eac3toPath;
        FfprobePath = settings.FfprobePath;
        FfmpegPath = settings.FfmpegPath;
        DefaultSaveFormatIndex = SaveFormatIndex(settings.DefaultSaveFormat);
        DefaultXmlLanguageIndex = XmlLanguageIndex(settings.DefaultXmlLanguage);
        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
    }

    private static ThemeColorSettings NormalizeThemeSettings(ThemeColorSettings settings)
    {
        var defaults = ThemeColorSettings.Default.OrderedSlots.ToList();
        var values = settings.OrderedSlots.Select((slot, index) => NormalizeColor(slot.Value, defaults[index].Value)).ToList();
        return new ThemeColorSettings(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private void ApplyColors(ThemeColorSettings settings)
    {
        var values = settings.OrderedSlots.ToList();
        for (var index = 0; index < ColorSlots.Count && index < values.Count; index++)
        {
            ColorSlots[index].Value = values[index].Value;
        }
    }

    private void ApplyCurrentTheme()
    {
        if (ColorSlots.Count < ThemeColorSettings.Default.OrderedSlots.Count)
        {
            return;
        }

        themeApplicationService?.Apply(CurrentThemeSettings());
    }

    private void ApplyLiveSettings()
    {
        if (!liveApplyEnabled || isApplyingSnapshot)
        {
            return;
        }

        ApplyCurrentAppSettingsToOwner();
        NotifyUnsavedChanges();
    }

    private void ApplyCurrentAppSettingsToOwner() => owner.ApplySettings(CurrentAppSettings());

    private void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasUnsavedChanges));

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        return text is ['#', _, _, _, _, _, _] && text.Skip(1).All(Uri.IsHexDigit)
            ? text.ToUpperInvariant()
            : fallback;
    }

    private static string? CleanPath(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string InformationalVersion(Type type)
    {
        var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version.Split('+', 2)[0];
        }

        return type.Assembly.GetName().Version?.ToString(3) ?? "unknown";
    }

    private void SetFrameAccuracyTolerance(decimal value, bool updateSlider)
    {
        var normalized = MainWindowViewModel.NormalizeFrameAccuracyTolerance(value);
        if (SetProperty(ref frameAccuracyTolerance, normalized, nameof(FrameAccuracyTolerance)))
        {
            OnPropertyChanged(nameof(FrameAccuracyToleranceDisplayText));
            ApplyLiveSettings();
        }

        if (updateSlider)
        {
            SetProperty(ref frameAccuracyToleranceSliderValue, (double)normalized, nameof(FrameAccuracyToleranceSliderValue));
        }
    }

    private static int SaveFormatIndex(string? value)
    {
        if (Enum.TryParse<ChapterExportFormat>(value, ignoreCase: true, out var format))
        {
            var index = Array.IndexOf(SaveFormats, format);
            return Math.Max(0, index);
        }

        return 0;
    }

    private int XmlLanguageIndex(string? value)
    {
        var index = XmlLanguageOptions.ToList().FindIndex(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
        return Math.Max(0, index);
    }

}

public sealed record SettingsToolStatus(
    SettingsToolStatusKind Kind,
    string? ResolvedPath,
    string ExpectedExecutable);

public enum SettingsToolStatusKind
{
    Discovery,
    Found,
    Missing,
    InvalidPath,
    NotDirectory,
    Unsupported
}
