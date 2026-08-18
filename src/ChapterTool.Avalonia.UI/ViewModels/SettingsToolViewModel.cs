using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Provides settings state and commands for the settings tool.</summary>
public sealed partial class SettingsToolViewModel : ObservableViewModel, IDisposable
{
    private static IReadOnlyList<ChapterExportFormat> SaveFormats => ChapterExportFormats.All;

    private static IReadOnlyList<OutputTextEncoding> OutputEncodings => OutputTextEncodings.All;

    private readonly IPreferenceSink preferenceSink;
    private readonly IAppLocalizer localizer;
    private readonly ObservableCollection<LanguageOptionViewModel> languages = [];
    private readonly ISettingsPickerService? picker;
    private readonly IShellService? shellService;
    private readonly Func<Exception, ValueTask>? unexpectedErrorHandler;
    private readonly string? settingsDirectory;
    private readonly SettingsSnapshotCoordinator snapshotCoordinator;
    private readonly EventHandler cultureChangedHandler;
    private readonly EventHandler appearanceChangedHandler;
    private readonly ObservableCollection<SelectorDisplayOption> xmlLanguageDisplayOptions = [];
    private string selectedLanguage;
    private int defaultSaveFormatIndex;
    private int defaultXmlLanguageIndex;
    private int outputTextEncodingIndex;
    private decimal frameAccuracyTolerance;
    private double frameAccuracyToleranceSliderValue;
    private bool isRefreshingLanguages;

    public SettingsToolViewModel(
        IPreferenceSink preferenceSink,
        ISettingsStore<ChapterToolSettings>? settingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null,
        IExternalToolLocator? externalToolLocator = null,
        IThemeApplicationService? themeApplicationService = null,
        IShellService? shellService = null,
        IFontFamilyCatalog? fontFamilyCatalog = null,
        IFontApplicationService? fontApplicationService = null,
        string? settingsDirectory = null,
        IRuntimeCapabilities? capabilities = null,
        bool autoLoad = true,
        Func<Exception, ValueTask>? unexpectedErrorHandler = null)
    {
        this.preferenceSink = preferenceSink;
        snapshotCoordinator = new SettingsSnapshotCoordinator(ChapterToolSettings.Default);
        this.SettingsStoreForTesting = settingsStore;
        this.localizer = localizer ?? preferenceSink.Localizer;
        this.picker = picker;
        this.ExternalToolLocatorForTesting = externalToolLocator;
        this.shellService = shellService;
        this.unexpectedErrorHandler = unexpectedErrorHandler;
        this.settingsDirectory = settingsDirectory;
        Capabilities = capabilities ?? new RuntimeCapabilities(
            RuntimeSourceMode.LocalPath,
            RuntimeOutputMode.Directory,
            RuntimeSecondarySurfaceMode.NativeWindow,
            CanReadClipboard: true,
            CanWriteClipboard: true,
            CanConfigureExternalTools: true,
            CanRunExternalProcesses: true,
            CanOpenLocalPaths: true);
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
        UpdateDraftSnapshot();
        snapshotCoordinator.Commit(snapshotCoordinator.Draft);

        SaveCommand = new UiCommand(
            async (_, token) => await SaveAsync(token),
            _ => settingsStore is not null);
        ResetCommand = new UiCommand((_, _) =>
        {
            ApplyDefaults();
            return ValueTask.CompletedTask;
        });
        ValidateToolsCommand = new UiCommand(async (_, token) => await DiscoverAndFillToolPathsAsync(token), _ => IsExternalToolsVisible);
        BrowseSaveDirectoryCommand = new UiCommand(async (_, token) => await PickDirectoryAsync(value => SaveDirectory = value, token), _ => IsSaveDirectoryVisible);
        BrowseMkvToolnixCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => MkvToolnixPath = value, token), _ => IsExternalToolsVisible);
        BrowseFfprobeCommand = new UiCommand(async (_, token) => await PickExecutableAsync(value => FfprobePath = value, token), _ => IsExternalToolsVisible);
        ClearSaveDirectoryCommand = ClearCommand(() => SaveDirectory = null);
        ClearMkvToolnixCommand = ClearCommand(() => MkvToolnixPath = null);
        ClearFfprobeCommand = ClearCommand(() => FfprobePath = null);
        OpenRepositoryCommand = new UiCommand(async (_, token) => await OpenRepositoryAsync(token), _ => shellService is not null);
        OpenSettingsFolderCommand = new UiCommand(
            async (_, token) => await OpenSettingsFolderAsync(token),
            _ => IsSettingsFolderVisible && shellService is not null && !string.IsNullOrWhiteSpace(settingsDirectory));
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

    internal ISettingsStore<ChapterToolSettings>? SettingsStoreForTesting { get; }

    internal IExternalToolLocator? ExternalToolLocatorForTesting { get; }

    public SettingsAppearanceViewModel Appearance { get; }

    public IRuntimeCapabilities Capabilities { get; }

    public bool IsSaveDirectoryVisible =>
        Capabilities is { OutputMode: RuntimeOutputMode.Directory, CanOpenLocalPaths: true };

    public bool IsExternalToolsVisible => Capabilities.CanConfigureExternalTools;

    public bool IsSettingsFolderVisible => Capabilities.CanOpenLocalPaths;

    public void Dispose()
    {
        localizer.CultureChanged -= cultureChangedHandler;
        Appearance.Changed -= appearanceChangedHandler;
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages => languages;

    public IReadOnlyList<string> SaveFormatOptions { get; } = [.. SaveFormats.Select(ChapterExportFormats.DisplayName)];

    public IReadOnlyList<string> OutputTextEncodingOptions { get; } = [.. OutputEncodings.Select(OutputTextEncodings.DisplayName)];

    public IReadOnlyList<string> XmlLanguageOptions { get; } =
        [.. XmlChapterLanguageCatalog.Languages.Select(static language => language.Code)];

    public IReadOnlyList<SelectorDisplayOption> XmlLanguageDisplayOptions => xmlLanguageDisplayOptions;

    public string AvaloniaRuntimeDisplay { get; } = $"Avalonia v{InformationalVersion(typeof(global::Avalonia.Application))}";

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
            if (SetProperty(ref field, ChapterSavePath.CleanOptionalPath(value)))
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
            if (SetProperty(ref field, ChapterSavePath.CleanOptionalPath(value)))
            {
                MkvToolnixStatus = FormatToolStatus(ValidateTool(value, "mkvextract"));
                NotifyUnsavedChanges();
            }
        }
    }

    public string? FfprobePath
    {
        get;
        set
        {
            if (SetProperty(ref field, ChapterSavePath.CleanOptionalPath(value)))
            {
                FfprobeStatus = FormatToolStatus(ValidateTool(value, "ffprobe"));
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
        SettingsStoreForTesting is not null && HasUnsavedSettingsChanges();

    public bool SettingsLoadFailed
    {
        get => snapshotCoordinator.LoadFailed;
        private set
        {
            if (snapshotCoordinator.LoadFailed == value)
            {
                return;
            }

            snapshotCoordinator.SetLoadFailed(value);
            OnPropertyChanged();
        }
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

    public string FfprobeStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public UiCommand SaveCommand { get; }

    public UiCommand ResetCommand { get; }

    public UiCommand ValidateToolsCommand { get; }

    public UiCommand BrowseSaveDirectoryCommand { get; }

    public UiCommand BrowseMkvToolnixCommand { get; }

    public UiCommand BrowseFfprobeCommand { get; }

    public UiCommand ClearSaveDirectoryCommand { get; }

    public UiCommand ClearMkvToolnixCommand { get; }

    public UiCommand ClearFfprobeCommand { get; }

    public UiCommand OpenRepositoryCommand { get; }

    public UiCommand OpenSettingsFolderCommand { get; }

    public async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        snapshotCoordinator.BeginLoad();
        SettingsLoadFailed = false;
        Appearance.SetLiveApplyEnabled(false);
        try
        {
            try
            {
                if (SettingsStoreForTesting is not null)
                {
                    var settings = await LoadSettingsOrDefaultAsync(cancellationToken);
                    snapshotCoordinator.SetLoaded(settings);
                    ApplyAppSettingsToFields(snapshotCoordinator.Saved.Application);
                    Appearance.ApplyThemeSettings(snapshotCoordinator.Saved.Theme);
                    Appearance.ApplyFontSettings(snapshotCoordinator.Saved.Font);
                    Appearance.ApplyToServices(snapshotCoordinator.Saved.Theme, snapshotCoordinator.Saved.Font);

                    // Capture the post-apply UI snapshot so resolved fonts/paths are not marked dirty.
                    UpdateDraftSnapshot();
                    snapshotCoordinator.CaptureDraftAsSaved();
                }
            }
            finally
            {
                snapshotCoordinator.EnableLiveApply();
                Appearance.SetLiveApplyEnabled(true);
            }

            ApplyCurrentAppSettingsToOwner();
            if (SettingsLoadFailed)
            {
                snapshotCoordinator.CaptureDraftAsSaved();
            }
            RefreshToolStatuses();
            NotifyUnsavedChanges();
            StatusText = StatusTextForCurrentLoadState();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SettingsLoadFailed = true;
            StatusText = localizer.GetString("Status.UnexpectedError");
            if (unexpectedErrorHandler is not null)
            {
                await unexpectedErrorHandler(exception);
            }
        }
    }

    private async ValueTask<ChapterToolSettings> LoadSettingsOrDefaultAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SettingsStoreForTesting!.LoadAsync(cancellationToken);
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

        if (SettingsStoreForTesting is not null)
        {
            var settings = CurrentSettings();
            await SettingsStoreForTesting.SaveAsync(settings, cancellationToken);
            snapshotCoordinator.Commit(settings);
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
        Appearance.ApplyThemeSettings(snapshotCoordinator.Saved.Theme);
        Appearance.ApplyFontSettings(snapshotCoordinator.Saved.Font);
        Appearance.ApplyToServices(snapshotCoordinator.Saved.Theme, snapshotCoordinator.Saved.Font);
        NotifyUnsavedChanges();
    }

    public void DiscardUnsavedChanges()
    {
        snapshotCoordinator.BeginSnapshot();
        Appearance.BeginSnapshot();
        try
        {
            ApplyAppSettingsToFields(snapshotCoordinator.Saved.Application);
            Appearance.ApplyThemeSettings(snapshotCoordinator.Saved.Theme);
            Appearance.ApplyFontSettings(snapshotCoordinator.Saved.Font);
        }
        finally
        {
            snapshotCoordinator.EndSnapshot();
            Appearance.EndSnapshot();
        }

        ApplyCurrentAppSettingsToOwner();
        Appearance.ApplyToServices(snapshotCoordinator.Saved.Theme, snapshotCoordinator.Saved.Font);
        snapshotCoordinator.DiscardDraft();
        RefreshToolStatuses();
        NotifyUnsavedChanges();
    }

    private void ApplyDefaults()
    {
        var defaults = ChapterToolSettings.Default;
        snapshotCoordinator.ResetDraft();
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
    [
        .. localizer.SupportedLanguages
            .Select(language => new LanguageOptionViewModel(
                language.CultureName,
                localizer.GetString(language.DisplayNameKey)))
    ];

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
        FfprobeStatus = FormatToolStatus(ValidateTool(FfprobePath, "ffprobe"));
    }

    private async ValueTask DiscoverAndFillToolPathsAsync(CancellationToken cancellationToken)
    {
        if (ExternalToolLocatorForTesting is null)
        {
            RefreshToolStatuses();
            return;
        }

        MkvToolnixPath = await DiscoverExecutableAsync("mkvextract", MkvToolnixPath, cancellationToken);
        var ffprobe = await DiscoverExecutableAsync("ffprobe", FfprobePath, cancellationToken);
        FfprobePath = ffprobe;

        RefreshToolStatuses();
    }

    private async ValueTask<string?> DiscoverExecutableAsync(string toolId, string? currentPath, CancellationToken cancellationToken)
    {
        var current = ValidateTool(currentPath, toolId);
        if (current.Kind == SettingsToolStatusKind.Found)
        {
            return current.ResolvedPath ?? currentPath;
        }

        var location = await ExternalToolLocatorForTesting!.LocateAsync(toolId, cancellationToken);
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
                ExternalToolExecutableNames.ExecutableName(toolId));
        }

        var text = configuredPath.Trim();
        var executableName = ExternalToolExecutableNames.ExecutableName(toolId);
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
                ExternalToolExecutableNames.ExecutableName(toolId));
        }

        var text = configuredPath.Trim();
        var executableName = ExternalToolExecutableNames.ExecutableName(toolId);
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

    private ChapterToolSettings CurrentSettings()
    {
        var settings = ChapterToolSettings.Normalize(snapshotCoordinator.Saved with
        {
            Application = CurrentAppSettings(),
            Theme = Appearance.CurrentThemeSettings(),
            Font = Appearance.CurrentFontSettings(),
        });

        snapshotCoordinator.UpdateDraft(settings);
        return settings;
    }

    private AppSettings CurrentAppSettings() =>
        snapshotCoordinator.Saved.Application with
        {
            Language = SelectedLanguage,
            SavingPath = SaveDirectory,
            MkvToolnixPath = MkvToolnixPath,
            FfprobePath = FfprobePath,
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
        FfprobePath = settings.FfprobePath;
        DefaultSaveFormatIndex = SaveFormatIndex(settings.DefaultSaveFormat);
        DefaultXmlLanguageIndex = XmlLanguageIndex(settings.DefaultXmlLanguage);
        OutputTextEncodingIndex = TextEncodingIndex(settings.OutputTextEncoding);
        EmitBom = settings.EmitBom;
        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
    }

    private void ApplyLiveSettings()
    {
        if (!snapshotCoordinator.LiveApplyEnabled || snapshotCoordinator.IsApplyingSnapshot)
        {
            return;
        }

        ApplyCurrentAppSettingsToOwner();
        NotifyUnsavedChanges();
    }

    private void ApplyCurrentAppSettingsToOwner() => preferenceSink.ApplyLivePreferences(CurrentAppSettings());

    private void NotifyUnsavedChanges()
    {
        UpdateDraftSnapshot();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void UpdateDraftSnapshot() => _ = CurrentSettings();

    private bool HasUnsavedSettingsChanges()
    {
        UpdateDraftSnapshot();
        return snapshotCoordinator.HasUnsavedChanges;
    }

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
