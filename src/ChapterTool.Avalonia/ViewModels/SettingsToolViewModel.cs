using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
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
    private static IReadOnlyList<OutputTextEncoding> OutputEncodings => OutputTextEncodings.All;

    private readonly Session.Ports.IPreferenceSink preferenceSink;
    private readonly ISettingsStore<ChapterToolSettings>? settingsStore;
    private readonly IAppLocalizer localizer;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private readonly ISettingsPickerService? picker;
    private readonly IExternalToolLocator? externalToolLocator;
    private readonly IShellService? shellService;
    private readonly string? settingsDirectory;
    private readonly EventHandler cultureChangedHandler;
    private readonly EventHandler appearanceChangedHandler;
    private ChapterToolSettings savedSettings = ChapterToolSettings.Default;
    private string selectedLanguage;
    private int defaultSaveFormatIndex;
    private int defaultXmlLanguageIndex;
    private int outputTextEncodingIndex;
    private decimal frameAccuracyTolerance;
    private double frameAccuracyToleranceSliderValue;
    private bool liveApplyEnabled;
    private bool isApplyingSnapshot;
    private bool isRefreshingLanguages;
    private readonly ObservableCollection<SelectorDisplayOption> xmlLanguageDisplayOptions = [];

    public SettingsToolViewModel(
        Session.Ports.IPreferenceSink preferenceSink,
        ISettingsStore<ChapterToolSettings>? settingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null,
        IExternalToolLocator? externalToolLocator = null,
        IThemeApplicationService? themeApplicationService = null,
        IShellService? shellService = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null,
        string? settingsDirectory = null,
        bool autoLoad = true)
    {
        this.preferenceSink = preferenceSink;
        this.settingsStore = settingsStore;
        this.localizer = localizer ?? preferenceSink.Localizer;
        this.picker = picker;
        this.externalToolLocator = externalToolLocator;
        this.shellService = shellService;
        this.settingsDirectory = settingsDirectory;
        Appearance = new SettingsAppearanceViewModel(
            this.localizer,
            themeApplicationService,
            fontFamilyCatalog,
            fontApplicationService);
        appearanceChangedHandler = (_, _) => NotifyUnsavedChanges();
        Appearance.Changed += appearanceChangedHandler;
        selectedLanguage = AppLanguage.Normalize(preferenceSink.UiLanguage);
        defaultSaveFormatIndex = Math.Clamp(preferenceSink.SaveFormatIndex, 0, SaveFormats.Count - 1);
        defaultXmlLanguageIndex = XmlLanguageIndex(preferenceSink.XmlLanguage);
        outputTextEncodingIndex = Math.Max(0, IndexOf(OutputEncodings, preferenceSink.OutputTextEncoding));
        frameAccuracyTolerance = MainWindowViewModel.NormalizeFrameAccuracyTolerance(preferenceSink.FrameAccuracyTolerance);
        frameAccuracyToleranceSliderValue = (double)frameAccuracyTolerance;
        ReplaceLanguages(BuildLanguageOptions());
        RefreshXmlLanguageDisplayOptions(notify: false);

        SaveCommand = new UiCommand(
            async (_, token) => await SaveAsync(token),
            _ => settingsStore is not null);
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
            Appearance.RefreshLocalizedOptions();
            RefreshXmlLanguageDisplayOptions(notify: true);
            RefreshToolStatuses();
            if (!string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = StatusTextForCurrentLoadState();
            }
        };
        this.localizer.CultureChanged += cultureChangedHandler;
        InitializationTask = autoLoad ? InitializeAsync() : Task.CompletedTask;
    }

    internal Task InitializationTask { get; }

    public SettingsAppearanceViewModel Appearance { get; }

    public void Dispose()
    {
        localizer.CultureChanged -= cultureChangedHandler;
        Appearance.Changed -= appearanceChangedHandler;
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages => languages;

    public IReadOnlyList<string> SaveFormatOptions { get; } = SaveFormats.Select(ChapterExportFormats.DisplayName).ToArray();

    public IReadOnlyList<string> OutputTextEncodingOptions { get; } = OutputEncodings.Select(OutputTextEncodings.DisplayName).ToArray();

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
                : IndexOf(XmlLanguageDisplayOptions, entry =>
                    string.Equals(entry.MainText, value.MainText, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                DefaultXmlLanguageIndex = index;
            }
        }
    }

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
        get => IndexOf(Languages, entry => string.Equals(entry.CultureName, SelectedLanguage, StringComparison.OrdinalIgnoreCase));
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
            if (SetProperty(ref field, CleanDirectory(value)))
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
            if (SetProperty(ref field, CleanOptionalPath(value)))
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
            if (SetProperty(ref field, CleanOptionalPath(value)))
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
            if (SetProperty(ref field, CleanOptionalPath(value)))
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
            if (SetProperty(ref field, CleanOptionalPath(value)))
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

    public int OutputTextEncodingIndex
    {
        get => outputTextEncodingIndex;
        set
        {
            if (SetProperty(ref outputTextEncodingIndex, Math.Clamp(value, 0, OutputEncodings.Count - 1)))
            {
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
        settingsStore is not null && CurrentSettings() != savedSettings;

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
        Appearance.SetLiveApplyEnabled(false);
        try
        {
            if (settingsStore is not null)
            {
                var settings = await LoadSettingsOrDefaultAsync(cancellationToken);
                savedSettings = ChapterToolSettings.Normalize(settings);
                ApplyAppSettingsToFields(savedSettings.Application);
                Appearance.ApplyThemeSettings(savedSettings.Theme);
                Appearance.ApplyFontSettings(savedSettings.Font);
                Appearance.ApplyToServices(savedSettings.Theme, savedSettings.Font);
                // Capture the post-apply UI snapshot so resolved fonts/paths are not marked dirty.
                savedSettings = CurrentSettings();
            }
        }
        finally
        {
            liveApplyEnabled = true;
            Appearance.SetLiveApplyEnabled(true);
        }

        ApplyCurrentAppSettingsToOwner();
        RefreshToolStatuses();
        NotifyUnsavedChanges();
        StatusText = StatusTextForCurrentLoadState();
    }

    private async ValueTask<ChapterToolSettings> LoadSettingsOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await settingsStore!.LoadAsync(cancellationToken);
        }
        catch (IOException)
        {
            SettingsLoadFailed = true;
            return ChapterToolSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            SettingsLoadFailed = true;
            return ChapterToolSettings.Default;
        }
        catch (CorruptSettingsFileException)
        {
            SettingsLoadFailed = true;
            return ChapterToolSettings.Default;
        }
    }

    private async Task InitializeAsync() => await LoadAsync(CancellationToken.None);

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        if (SettingsLoadFailed && !HasUnsavedChanges)
        {
            StatusText = StatusTextForCurrentLoadState();
            return;
        }

        if (settingsStore is not null)
        {
            var settings = CurrentSettings();
            await settingsStore.SaveAsync(settings, cancellationToken);
            savedSettings = settings;
            Appearance.ApplyThemeSettings(settings.Theme);
            Appearance.ApplyFontSettings(settings.Font);
            Appearance.ApplyToServices(settings.Theme, settings.Font);
        }

        RefreshToolStatuses();
        NotifyUnsavedChanges();
        SettingsLoadFailed = false;
        StatusText = localizer.GetString("Settings.Status.Saved");
    }

    public void DiscardUnsavedAppearanceChanges()
    {
        Appearance.ApplyThemeSettings(savedSettings.Theme);
        Appearance.ApplyFontSettings(savedSettings.Font);
        Appearance.ApplyToServices(savedSettings.Theme, savedSettings.Font);
        NotifyUnsavedChanges();
    }

    public void DiscardUnsavedChanges()
    {
        isApplyingSnapshot = true;
        Appearance.BeginSnapshot();
        try
        {
            ApplyAppSettingsToFields(savedSettings.Application);
            Appearance.ApplyThemeSettings(savedSettings.Theme);
            Appearance.ApplyFontSettings(savedSettings.Font);
        }
        finally
        {
            isApplyingSnapshot = false;
            Appearance.EndSnapshot();
        }

        ApplyCurrentAppSettingsToOwner();
        Appearance.ApplyToServices(savedSettings.Theme, savedSettings.Font);
        RefreshToolStatuses();
        NotifyUnsavedChanges();
    }

    private void ApplyDefaults()
    {
        var defaults = ChapterToolSettings.Default;
        ApplyAppSettingsToFields(defaults.Application);
        Appearance.ApplyThemeSettings(defaults.Theme);
        Appearance.ApplyFontSettings(defaults.Font);
        Appearance.ApplyToServices(defaults.Theme, defaults.Font);
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

    private ChapterToolSettings CurrentSettings() =>
        ChapterToolSettings.Normalize(savedSettings with
        {
            Application = CurrentAppSettings(),
            Theme = Appearance.CurrentThemeSettings(),
            Font = Appearance.CurrentFontSettings(),
        });

    private AppSettings CurrentAppSettings() =>
        savedSettings.Application with
        {
            Language = SelectedLanguage,
            SavingPath = SaveDirectory,
            MkvToolnixPath = MkvToolnixPath,
            Eac3toPath = Eac3toPath,
            FfprobePath = FfprobePath,
            FfmpegPath = FfmpegPath,
            DefaultSaveFormat = SaveFormats[DefaultSaveFormatIndex].ToString(),
            DefaultXmlLanguage = XmlLanguageOptions[DefaultXmlLanguageIndex],
            OutputTextEncoding = OutputTextEncodings.Id(OutputEncodings[OutputTextEncodingIndex]),
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
        OutputTextEncodingIndex = TextEncodingIndex(settings.OutputTextEncoding);
        EmitBom = settings.EmitBom;
        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
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

    private void ApplyCurrentAppSettingsToOwner() => preferenceSink.ApplyLivePreferences(CurrentAppSettings());

    private void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasUnsavedChanges));

    private static string? CleanDirectory(string? value) => ChapterSavePath.CleanOptionalPath(value);

    private static string? CleanOptionalPath(string? value) => ChapterSavePath.CleanOptionalPath(value);

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
        var index = IndexOf(XmlLanguageOptions, entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
        return Math.Max(0, index);
    }

    private static int TextEncodingIndex(string? value) =>
        Math.Max(0, IndexOf(OutputEncodings, OutputTextEncodings.ParseOrDefault(value)));

    private static int IndexOf<T>(IReadOnlyList<T> items, T value)
        where T : notnull
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(items[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (predicate(items[index]))
            {
                return index;
            }
        }

        return -1;
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
