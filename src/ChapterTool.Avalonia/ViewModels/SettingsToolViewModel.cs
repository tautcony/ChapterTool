using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class SettingsToolViewModel : ObservableViewModel, IDisposable
{
    private static IReadOnlyList<ChapterExportFormat> SaveFormats => ChapterExportFormats.All;

    private readonly MainWindowViewModel owner;
    private readonly ISettingsStore<AppSettings>? appSettingsStore;
    private readonly ISettingsStore<ThemeSettings>? themeSettingsStore;
    private readonly ISettingsStore<FontSettings>? fontSettingsStore;
    private readonly IAppLocalizer localizer;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private readonly ObservableCollection<ThemePresetOptionViewModel> themePresets = [];
    private readonly ObservableCollection<FontFamilyOptionViewModel> uiFontFamilies = [];
    private readonly ObservableCollection<FontFamilyOptionViewModel> monospaceFontFamilies = [];
    private readonly ISettingsPickerService? picker;
    private readonly IExternalToolLocator? externalToolLocator;
    private readonly IThemeApplicationService? themeApplicationService;
    private readonly IFontFamilyCatalog? fontFamilyCatalog;
    private readonly IFontApplicationService? fontApplicationService;
    private readonly IShellService? shellService;
    private readonly string? settingsDirectory;
    private readonly EventHandler cultureChangedHandler;
    private AppSettings savedAppSettings = new();
    private ThemeSettings savedThemeSettings = ThemeSettings.Default;
    private FontSettings savedFontSettings = FontSettings.Default;
    private string selectedThemePresetId = ThemePresetCatalog.DefaultPresetId;
    private string selectedUiFontFamily = string.Empty;
    private string selectedMonospaceFontFamily = string.Empty;
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
        ISettingsStore<ThemeSettings>? themeSettingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null,
        IExternalToolLocator? externalToolLocator = null,
        IThemeApplicationService? themeApplicationService = null,
        IShellService? shellService = null,
        ISettingsStore<FontSettings>? fontSettingsStore = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null,
        string? settingsDirectory = null,
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
        this.fontSettingsStore = fontSettingsStore;
        this.fontFamilyCatalog = fontFamilyCatalog;
        this.fontApplicationService = fontApplicationService;
        this.settingsDirectory = settingsDirectory;
        selectedLanguage = AppLanguage.Normalize(owner.UiLanguage);
        defaultSaveFormatIndex = Math.Clamp(owner.SaveFormatIndex, 0, SaveFormats.Count - 1);
        defaultXmlLanguageIndex = XmlLanguageIndex(owner.XmlLanguage);
        frameAccuracyTolerance = MainWindowViewModel.NormalizeFrameAccuracyTolerance(owner.FrameAccuracyTolerance);
        frameAccuracyToleranceSliderValue = (double)frameAccuracyTolerance;
        ReplaceLanguages(BuildLanguageOptions());
        RefreshXmlLanguageDisplayOptions(notify: false);
        ReplaceThemePresets();
        ReplaceFontFamilies();

        SaveCommand = new UiCommand(
            async (_, token) => await SaveAsync(token),
            _ => appSettingsStore is not null || themeSettingsStore is not null || fontSettingsStore is not null);
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
        OpenSettingsFolderCommand = new UiCommand(
            async (_, token) => await OpenSettingsFolderAsync(token),
            _ => shellService is not null && !string.IsNullOrWhiteSpace(settingsDirectory));
        cultureChangedHandler = (_, _) =>
        {
            RefreshLanguages();
            ReplaceThemePresets();
            ReplaceFontFamilies();
            RefreshXmlLanguageDisplayOptions(notify: true);
            RefreshToolStatuses();
            if (!string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = StatusTextForCurrentLoadState();
            }
        };
        this.localizer.CultureChanged += cultureChangedHandler;
        if (autoLoad)
        {
            InitializationTask = InitializeAsync();
        }
        else
        {
            InitializationTask = Task.CompletedTask;
        }
    }

    internal Task InitializationTask { get; }

    public void Dispose()
    {
        localizer.CultureChanged -= cultureChangedHandler;
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages => languages;

    public IReadOnlyList<string> SaveFormatOptions { get; } = SaveFormats.Select(ChapterExportFormats.DisplayName).ToArray();

    public IReadOnlyList<string> XmlLanguageOptions { get; } =
        XmlChapterLanguageCatalog.Languages.Select(static language => language.Code).ToList();

    public IReadOnlyList<SelectorDisplayOption> XmlLanguageDisplayOptions => xmlLanguageDisplayOptions;

    public string AvaloniaRuntimeDisplay { get; } = $"Avalonia v{InformationalVersion(typeof(Application))}";

    public string DotNetRuntimeDisplay { get; } = $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}";

    public SelectorDisplayOption? SelectedDefaultXmlLanguageDisplayOption
    {
        get
        {
            var entries = XmlLanguageDisplayOptions;
            return DefaultXmlLanguageIndex < 0 || DefaultXmlLanguageIndex >= entries.Count
                ? null
                : entries[DefaultXmlLanguageIndex];
        }
        set
        {
            var index = value is null
                ? -1
                : XmlLanguageDisplayOptions.ToList().FindIndex(entry =>
                    string.Equals(entry.MainText, value.MainText, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                DefaultXmlLanguageIndex = index;
            }
        }
    }

    public IReadOnlyList<ThemePresetOptionViewModel> ThemePresets => themePresets;

    public int SelectedThemePresetIndex
    {
        get => themePresets.ToList().FindIndex(option => string.Equals(option.Id, selectedThemePresetId, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < themePresets.Count)
            {
                SetSelectedThemePresetId(themePresets[value].Id);
            }
        }
    }

    public ThemePresetOptionViewModel SelectedThemePreset =>
        themePresets.First(option => string.Equals(option.Id, selectedThemePresetId, StringComparison.Ordinal));

    public string ThemePreviewAutomationName =>
        localizer.Format(
            "Settings.Appearance.PreviewFor",
            new Dictionary<string, object?> { ["name"] = SelectedThemePreset.DisplayName });

    public IReadOnlyList<FontFamilyOptionViewModel> UiFontFamilies => uiFontFamilies;

    public IReadOnlyList<FontFamilyOptionViewModel> MonospaceFontFamilies => monospaceFontFamilies;

    public int SelectedUiFontFamilyIndex
    {
        get => uiFontFamilies.ToList().FindIndex(option => string.Equals(option.FamilyName, selectedUiFontFamily, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < uiFontFamilies.Count)
            {
                SetSelectedUiFontFamily(uiFontFamilies[value].FamilyName);
            }
        }
    }

    public int SelectedMonospaceFontFamilyIndex
    {
        get => monospaceFontFamilies.ToList().FindIndex(option => string.Equals(option.FamilyName, selectedMonospaceFontFamily, StringComparison.Ordinal));
        set
        {
            if (value >= 0 && value < monospaceFontFamilies.Count)
            {
                SetSelectedMonospaceFontFamily(monospaceFontFamilies[value].FamilyName);
            }
        }
    }

    public FontFamilyOptionViewModel SelectedUiFontFamily =>
        uiFontFamilies.First(option => string.Equals(option.FamilyName, selectedUiFontFamily, StringComparison.Ordinal));

    public FontFamilyOptionViewModel SelectedMonospaceFontFamily =>
        monospaceFontFamilies.First(option => string.Equals(option.FamilyName, selectedMonospaceFontFamily, StringComparison.Ordinal));

    public string FontPreviewText => localizer.GetString("Settings.Appearance.FontPreviewSample");

    public string UiFontPreviewAutomationName => FontPreviewAutomationName("Settings.Appearance.UiFontPreviewFor", SelectedUiFontFamily.DisplayName);

    public string MonospaceFontPreviewAutomationName => FontPreviewAutomationName(
        "Settings.Appearance.MonospaceFontPreviewFor",
        SelectedMonospaceFontFamily.DisplayName);

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
            var index = Languages.ToList().FindIndex(entry => string.Equals(entry.CultureName, SelectedLanguage, StringComparison.OrdinalIgnoreCase));
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
            if (SetProperty(ref defaultSaveFormatIndex, Math.Clamp(value, 0, SaveFormats.Count - 1)))
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

    public bool EmitBom
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ApplyLiveSettings();
            }
        }
    } = true;

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
        || themeSettingsStore is not null && CurrentThemeSettings() != savedThemeSettings
        || fontSettingsStore is not null && CurrentFontSettings() != savedFontSettings;

    public bool SettingsLoadFailed
    {
        get;
        private set => SetProperty(ref field, value);
    }

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

    public UiCommand OpenSettingsFolderCommand { get; }

    public async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        SettingsLoadFailed = false;
        liveApplyEnabled = false;
        try
        {
            if (appSettingsStore is not null)
            {
                var settings = await LoadAppSettingsOrDefaultAsync(cancellationToken);
                ApplyAppSettingsToFields(settings);
                savedAppSettings = CurrentAppSettings();
            }

            if (themeSettingsStore is not null)
            {
                var theme = await LoadThemeSettingsOrDefaultAsync(cancellationToken);
                savedThemeSettings = ThemePresetCatalog.Normalize(theme);
                ApplyThemeSettings(theme);
                themeApplicationService?.Apply(theme);
            }

            if (fontSettingsStore is not null)
            {
                var fonts = await LoadFontSettingsOrDefaultAsync(cancellationToken);
                savedFontSettings = ResolveFontSettings(fonts);
                ApplyFontSettings(savedFontSettings);
                fontApplicationService?.Apply(savedFontSettings);
            }
        }
        finally
        {
            liveApplyEnabled = true;
        }

        ApplyCurrentAppSettingsToOwner();
        RefreshToolStatuses();
        NotifyUnsavedChanges();
        StatusText = StatusTextForCurrentLoadState();
    }

    private async ValueTask<AppSettings> LoadAppSettingsOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await appSettingsStore!.LoadAsync(cancellationToken);
        }
        catch (IOException)
        {
            SettingsLoadFailed = true;
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            SettingsLoadFailed = true;
            return new AppSettings();
        }
        catch (CorruptSettingsFileException)
        {
            SettingsLoadFailed = true;
            return new AppSettings();
        }
    }

    private async ValueTask<ThemeSettings> LoadThemeSettingsOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await themeSettingsStore!.LoadAsync(cancellationToken);
        }
        catch (IOException)
        {
            SettingsLoadFailed = true;
            return ThemeSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            SettingsLoadFailed = true;
            return ThemeSettings.Default;
        }
        catch (CorruptSettingsFileException)
        {
            SettingsLoadFailed = true;
            return ThemeSettings.Default;
        }
    }

    private async ValueTask<FontSettings> LoadFontSettingsOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await fontSettingsStore!.LoadAsync(cancellationToken);
        }
        catch (IOException)
        {
            SettingsLoadFailed = true;
            return FontSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            SettingsLoadFailed = true;
            return FontSettings.Default;
        }
        catch (CorruptSettingsFileException)
        {
            SettingsLoadFailed = true;
            return FontSettings.Default;
        }
    }

    private async Task InitializeAsync()
    {
        await LoadAsync(CancellationToken.None);
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (SettingsLoadFailed && !HasUnsavedChanges)
        {
            StatusText = StatusTextForCurrentLoadState();
            return;
        }

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
            ApplyThemeSettings(settings);
            themeApplicationService?.Apply(settings);
        }

        if (fontSettingsStore is not null)
        {
            var settings = CurrentFontSettings();
            await fontSettingsStore.SaveAsync(settings, cancellationToken);
            savedFontSettings = settings;
            fontApplicationService?.Apply(settings);
        }

        RefreshToolStatuses();
        NotifyUnsavedChanges();
        SettingsLoadFailed = false;
        StatusText = localizer.GetString("Settings.Status.Saved");
    }

    public void DiscardUnsavedAppearanceChanges()
    {
        ApplyThemeSettings(savedThemeSettings);
        ApplyFontSettings(savedFontSettings);
        themeApplicationService?.Apply(savedThemeSettings);
        fontApplicationService?.Apply(savedFontSettings);
        NotifyUnsavedChanges();
    }

    public void DiscardUnsavedChanges()
    {
        isApplyingSnapshot = true;
        try
        {
            ApplyAppSettingsToFields(savedAppSettings);
            ApplyThemeSettings(savedThemeSettings);
            ApplyFontSettings(savedFontSettings);
        }
        finally
        {
            isApplyingSnapshot = false;
        }

        ApplyCurrentAppSettingsToOwner();
        themeApplicationService?.Apply(savedThemeSettings);
        fontApplicationService?.Apply(savedFontSettings);
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
        EmitBom = defaults.EmitBom;
        FrameAccuracyTolerance = defaults.FrameAccuracyTolerance;
        ApplyThemeSettings(ThemeSettings.Default);
        ApplyFontSettings(FontSettings.Default);
        themeApplicationService?.Apply(ThemeSettings.Default);
        fontApplicationService?.Apply(FontSettings.Default);
        ApplyLiveSettings();
        RefreshToolStatuses();
        SettingsLoadFailed = false;
        StatusText = localizer.GetString("Settings.Status.Reset");
    }

    private string StatusTextForCurrentLoadState() =>
        localizer.GetString(SettingsLoadFailed ? "Settings.Status.LoadedDefaults" : "Settings.Status.Ready");

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

    private async ValueTask OpenSettingsFolderAsync(CancellationToken cancellationToken)
    {
        if (shellService is not null && !string.IsNullOrWhiteSpace(settingsDirectory))
        {
            await shellService.OpenAsync(settingsDirectory, cancellationToken);
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
        var entries = XmlLanguageDisplay.Options(localizer);
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
            OnPropertyChanged(nameof(SelectedDefaultXmlLanguageDisplayOption));
        }
    }

    private List<LanguageOptionViewModel> BuildLanguageOptions() =>
        localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                localizer.GetString(language.DisplayNameKey)))
            .ToList();

    private void ReplaceLanguages(IReadOnlyList<LanguageOptionViewModel> entries)
    {
        languages.Clear();
        foreach (var entry in entries)
        {
            languages.Add(entry);
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

    private ThemeSettings CurrentThemeSettings() => new(selectedThemePresetId);

    private FontSettings CurrentFontSettings() => new(selectedUiFontFamily, selectedMonospaceFontFamily);

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
            EmitBom = EmitBom,
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
        EmitBom = settings.EmitBom;
        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
    }

    private void ApplyThemeSettings(ThemeSettings settings)
    {
        SetSelectedThemePresetId(ThemePresetCatalog.Resolve(settings.PresetId).Id, apply: false);
    }

    private void ApplyFontSettings(FontSettings settings)
    {
        var resolved = ResolveFontSettings(settings);
        SetSelectedUiFontFamily(resolved.UiFontFamily, apply: false);
        SetSelectedMonospaceFontFamily(resolved.MonospaceFontFamily, apply: false);
    }

    private FontSettings ResolveFontSettings(FontSettings settings) =>
        fontApplicationService?.Resolve(settings)
        ?? (fontFamilyCatalog is null ? FontSettings.Normalize(settings) : FontSettingsResolver.Resolve(settings, fontFamilyCatalog));

    private void SetSelectedUiFontFamily(string familyName, bool apply = true)
    {
        var resolved = ResolveFontSettings(new FontSettings(familyName, selectedMonospaceFontFamily));
        if (!SetProperty(ref selectedUiFontFamily, resolved.UiFontFamily, nameof(SelectedUiFontFamily)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedUiFontFamilyIndex));
        OnPropertyChanged(nameof(UiFontPreviewAutomationName));
        ApplyLiveFontSettings(apply);
    }

    private void SetSelectedMonospaceFontFamily(string familyName, bool apply = true)
    {
        var resolved = ResolveFontSettings(new FontSettings(selectedUiFontFamily, familyName));
        if (!SetProperty(ref selectedMonospaceFontFamily, resolved.MonospaceFontFamily, nameof(SelectedMonospaceFontFamily)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedMonospaceFontFamilyIndex));
        OnPropertyChanged(nameof(MonospaceFontPreviewAutomationName));
        ApplyLiveFontSettings(apply);
    }

    private void ApplyLiveFontSettings(bool apply)
    {
        if (apply && liveApplyEnabled && !isApplyingSnapshot)
        {
            fontApplicationService?.Apply(CurrentFontSettings());
            NotifyUnsavedChanges();
        }
    }

    private void SetSelectedThemePresetId(string presetId, bool apply = true)
    {
        var normalized = ThemePresetCatalog.Resolve(presetId).Id;
        if (!SetProperty(ref selectedThemePresetId, normalized, nameof(SelectedThemePreset)))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedThemePresetIndex));
        OnPropertyChanged(nameof(ThemePreviewAutomationName));
        if (apply && liveApplyEnabled && !isApplyingSnapshot)
        {
            themeApplicationService?.Apply(CurrentThemeSettings());
            NotifyUnsavedChanges();
        }
    }

    private void ReplaceThemePresets()
    {
        var selectedId = selectedThemePresetId;
        themePresets.Clear();
        foreach (var preset in ThemePresetCatalog.All)
        {
            themePresets.Add(new ThemePresetOptionViewModel(
                preset.Id,
                localizer.GetString(preset.DisplayNameKey),
                preset.Palette.PreviewSwatches.Select(static color => new ThemeSwatchViewModel(color)).ToArray()));
        }

        selectedThemePresetId = ThemePresetCatalog.Resolve(selectedId).Id;
        OnPropertyChanged(nameof(ThemePresets));
        OnPropertyChanged(nameof(SelectedThemePreset));
        OnPropertyChanged(nameof(SelectedThemePresetIndex));
        OnPropertyChanged(nameof(ThemePreviewAutomationName));
    }

    private void ReplaceFontFamilies()
    {
        var uiSelection = selectedUiFontFamily;
        var monospaceSelection = selectedMonospaceFontFamily;
        var families = fontFamilyCatalog?.Families ?? [];

        uiFontFamilies.Clear();
        uiFontFamilies.Add(new FontFamilyOptionViewModel(
            string.Empty,
            () => localizer.GetString("Settings.Appearance.SystemUiFont"),
            true,
            FontFamily.Default));
        monospaceFontFamilies.Clear();
        monospaceFontFamilies.Add(new FontFamilyOptionViewModel(
            string.Empty,
            () => localizer.GetString("Settings.Appearance.SystemMonospaceFont"),
            true,
            FontFamily.Parse(AvaloniaFontApplicationService.DefaultMonospaceFontFamily)));
        var cultureName = localizer.CurrentCultureName;
        foreach (var family in families)
        {
            var option = new FontFamilyOptionViewModel(
                family.FamilyName,
                () => family.GetDisplayName(cultureName),
                false,
                FontFamily.Parse(family.FamilyName));
            uiFontFamilies.Add(option);
            monospaceFontFamilies.Add(option);
        }

        selectedUiFontFamily = ResolveFontSettings(new FontSettings(uiSelection, monospaceSelection)).UiFontFamily;
        selectedMonospaceFontFamily = ResolveFontSettings(new FontSettings(uiSelection, monospaceSelection)).MonospaceFontFamily;
        OnPropertyChanged(nameof(UiFontFamilies));
        OnPropertyChanged(nameof(MonospaceFontFamilies));
        OnPropertyChanged(nameof(SelectedUiFontFamily));
        OnPropertyChanged(nameof(SelectedMonospaceFontFamily));
        OnPropertyChanged(nameof(SelectedUiFontFamilyIndex));
        OnPropertyChanged(nameof(SelectedMonospaceFontFamilyIndex));
        OnPropertyChanged(nameof(FontPreviewText));
        OnPropertyChanged(nameof(UiFontPreviewAutomationName));
        OnPropertyChanged(nameof(MonospaceFontPreviewAutomationName));
    }

    private string FontPreviewAutomationName(string key, string displayName) =>
        localizer.Format(key, new Dictionary<string, object?> { ["name"] = displayName });

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
            var index = ChapterExportFormats.IndexOf(format);
            return Math.Max(0, index);
        }

        return 0;
    }

    private int XmlLanguageIndex(string? value)
    {
        var index = XmlLanguageOptions.ToList().FindIndex(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
        return Math.Max(0, index);
    }
}

public sealed record ThemePresetOptionViewModel(
    string Id,
    string DisplayName,
    IReadOnlyList<ThemeSwatchViewModel> PreviewSwatches);

public sealed record ThemeSwatchViewModel(string Color);

public sealed class FontFamilyOptionViewModel(
    string familyName,
    Func<string> displayNameFactory,
    bool isDefault,
    FontFamily previewFontFamily)
{
    private readonly Lazy<string> displayName = new(displayNameFactory, LazyThreadSafetyMode.ExecutionAndPublication);

    public string FamilyName { get; } = familyName;

    public string DisplayName => displayName.Value;

    public bool IsDefault { get; } = isDefault;

    public FontFamily PreviewFontFamily { get; } = previewFontFamily;
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
