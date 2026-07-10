using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class SettingsToolViewModelTests
{
    [Fact]
    public async Task LoadsAndSavesDurablePreferences()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(
            SavingPath: "out",
            Language: "en-US",
            MkvToolnixPath: "mkv",
            Eac3toPath: "eac3to",
            FfprobePath: "ffprobe",
            FfmpegPath: "ffmpeg",
            DefaultSaveFormat: "Xml",
            DefaultXmlLanguage: "ja",
            EmitBom: true,
            FrameAccuracyTolerance: 0.02m));
        var themeStore = new FakeThemeSettingsStore(ThemeSettings.Default);
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, themeStore, new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedLanguage = "ja-JP";
        viewModel.SaveDirectory = "new-out";
        viewModel.MkvToolnixPath = null;
        viewModel.Eac3toPath = "new-eac3to";
        viewModel.FfprobePath = "new-ffprobe";
        viewModel.FfmpegPath = "new-ffmpeg";
        viewModel.DefaultSaveFormatIndex = viewModel.SaveFormatOptions.ToList().IndexOf("JSON");
        viewModel.DefaultXmlLanguageIndex = viewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        viewModel.EmitBom = false;
        viewModel.FrameAccuracyTolerance = 0.2m;
        SelectPreset(viewModel, "solarized-dark");

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Equal("ja-JP", appStore.Current.Language);
        Assert.Equal("new-out", appStore.Current.SavingPath);
        Assert.Null(appStore.Current.MkvToolnixPath);
        Assert.Equal("new-eac3to", appStore.Current.Eac3toPath);
        Assert.Equal("new-ffprobe", appStore.Current.FfprobePath);
        Assert.Equal("new-ffmpeg", appStore.Current.FfmpegPath);
        Assert.Equal("Json", appStore.Current.DefaultSaveFormat);
        Assert.Equal("jpn", appStore.Current.DefaultXmlLanguage);
        Assert.False(appStore.Current.EmitBom);
        Assert.Equal(0.2m, appStore.Current.FrameAccuracyTolerance);
        Assert.Equal(ChapterExportFormat.Json, owner.SaveFormat);
        Assert.Equal("jpn", owner.XmlLanguage);
        Assert.False(owner.EmitBom);
        Assert.Equal(0.2m, owner.FrameAccuracyTolerance);
        Assert.Equal("new-out", owner.SaveDirectory);
        Assert.Equal("ja-JP", owner.UiLanguage);
        Assert.Equal("solarized-dark", themeStore.Current.PresetId);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadFallsBackToDefaultsWhenSettingsStoresFail()
    {
        var owner = CreateOwner();
        var themeApplication = new FakeThemeApplicationService();
        var localizer = new AppLocalizationManager("en-US");
        var viewModel = CreateViewModel(
            owner,
            new ThrowingAppSettingsStore(),
            new ThrowingThemeSettingsStore(),
            localizer,
            themeApplicationService: themeApplication);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ChapterExportFormat.Txt, owner.SaveFormat);
        Assert.Equal(ThemePresetCatalog.DefaultPresetId, viewModel.SelectedThemePreset.Id);
        Assert.Equal(ThemePresetCatalog.DefaultPresetId, themeApplication.LastApplied?.PresetId);
        Assert.True(viewModel.SettingsLoadFailed);
        Assert.Contains("defaults", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task RuntimeSafeSettingsApplyImmediatelyWithoutSavingStore()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(
            SavingPath: "saved",
            Language: "en-US",
            DefaultSaveFormat: "Txt",
            DefaultXmlLanguage: "und",
            EmitBom: true,
            FrameAccuracyTolerance: 0.10m));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedLanguage = "ja-JP";
        viewModel.SaveDirectory = "live";
        viewModel.DefaultSaveFormatIndex = viewModel.SaveFormatOptions.ToList().IndexOf("JSON");
        viewModel.DefaultXmlLanguageIndex = viewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        viewModel.EmitBom = false;
        viewModel.FrameAccuracyTolerance = 0.20m;

        Assert.Equal("ja-JP", owner.UiLanguage);
        Assert.Equal("live", owner.SaveDirectory);
        Assert.Equal(ChapterExportFormat.Json, owner.SaveFormat);
        Assert.Equal("jpn", owner.XmlLanguage);
        Assert.False(owner.EmitBom);
        Assert.Equal(0.20m, owner.FrameAccuracyTolerance);
        Assert.Equal("saved", appStore.Current.SavingPath);
        Assert.Equal("en-US", appStore.Current.Language);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task SavePersistsLiveSettingsAndClearsUnsavedState()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(Language: "en-US", SavingPath: "saved"));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedLanguage = "ja-JP";
        viewModel.SaveDirectory = "live";
        Assert.True(viewModel.HasUnsavedChanges);

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Equal("ja-JP", appStore.Current.Language);
        Assert.Equal("live", appStore.Current.SavingPath);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task DiscardUnsavedChangesRestoresSavedRuntimeState()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(
            SavingPath: "saved",
            Language: "en-US",
            DefaultSaveFormat: "Txt",
            DefaultXmlLanguage: "und",
            FrameAccuracyTolerance: 0.10m));
        var themeStore = new FakeThemeSettingsStore(new ThemeSettings("solarized-light"));
        var themeApplication = new FakeThemeApplicationService();
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(
            owner,
            appStore,
            themeStore,
            new AppLocalizationManager("en-US"),
            themeApplicationService: themeApplication);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedLanguage = "ja-JP";
        viewModel.SaveDirectory = "live";
        viewModel.DefaultSaveFormatIndex = viewModel.SaveFormatOptions.ToList().IndexOf("JSON");
        viewModel.FrameAccuracyTolerance = 0.20m;
        SelectPreset(viewModel, "ayu-dark");

        viewModel.DiscardUnsavedChanges();

        Assert.Equal("en-US", owner.UiLanguage);
        Assert.Equal("saved", owner.SaveDirectory);
        Assert.Equal(ChapterExportFormat.Txt, owner.SaveFormat);
        Assert.Equal("und", owner.XmlLanguage);
        Assert.Equal(0.10m, owner.FrameAccuracyTolerance);
        Assert.Equal("solarized-light", themeApplication.LastApplied?.PresetId);
        Assert.Equal("en-US", appStore.Current.Language);
        Assert.Equal("solarized-light", themeStore.Current.PresetId);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task FrameAccuracyToleranceIsNormalizedBeforeSave()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FrameAccuracyTolerance: -1m));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0.15m, viewModel.FrameAccuracyTolerance);

        viewModel.FrameAccuracyTolerance = 2m;
        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Equal(0.30m, appStore.Current.FrameAccuracyTolerance);
        Assert.Equal(0.30m, owner.FrameAccuracyTolerance);
    }

    [Fact]
    public async Task FrameAccuracyToleranceSliderUsesContinuousBoundedValueAndDisplayText()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FrameAccuracyTolerance: 0.10m));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.FrameAccuracyToleranceSliderValue = 0.173;

        Assert.Equal(0.173m, viewModel.FrameAccuracyTolerance);
        Assert.Equal("0.173", viewModel.FrameAccuracyToleranceDisplayText);

        viewModel.FrameAccuracyToleranceSliderValue = 0.001;

        Assert.Equal(0.01m, viewModel.FrameAccuracyTolerance);
        Assert.Equal("0.01", viewModel.FrameAccuracyToleranceDisplayText);
    }

    [Fact]
    public async Task FrameAccuracyToleranceSliderSnapsNearRecommendedValues()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FrameAccuracyTolerance: 0.15m));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.FrameAccuracyToleranceSliderValue = 0.141;

        Assert.Equal(0.15m, viewModel.FrameAccuracyTolerance);
        Assert.Equal("0.15", viewModel.FrameAccuracyToleranceDisplayText);

        viewModel.FrameAccuracyToleranceSliderValue = 0.173;

        Assert.Equal(0.173m, viewModel.FrameAccuracyTolerance);
        Assert.Equal("0.173", viewModel.FrameAccuracyToleranceDisplayText);
    }

    [Fact]
    public async Task FrameAccuracyToleranceSliderDoesNotRewriteThumbValueWhenSnapping()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FrameAccuracyTolerance: 0.10m));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var sliderNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsToolViewModel.FrameAccuracyToleranceSliderValue))
            {
                sliderNotifications++;
            }
        };

        viewModel.FrameAccuracyToleranceSliderValue = 0.141;

        Assert.Equal(0.141, viewModel.FrameAccuracyToleranceSliderValue, precision: 3);
        Assert.Equal(0.15m, viewModel.FrameAccuracyTolerance);
        Assert.Equal("0.15", viewModel.FrameAccuracyToleranceDisplayText);
        Assert.Equal(1, sliderNotifications);
    }

    [Fact]
    public void SaveFormatOptionsExposeSingleQpfileEntry()
    {
        var viewModel = CreateViewModel(
            CreateOwner(),
            null,
            null,
            new AppLocalizationManager("en-US"));

        Assert.Contains("TXT", viewModel.SaveFormatOptions);
        Assert.Contains("XML", viewModel.SaveFormatOptions);
        Assert.Contains("QPFile", viewModel.SaveFormatOptions);
        Assert.Contains("TsmuxerMeta", viewModel.SaveFormatOptions);
        Assert.Contains("CUE", viewModel.SaveFormatOptions);
        Assert.Contains("JSON", viewModel.SaveFormatOptions);
        Assert.DoesNotContain("Qpf", viewModel.SaveFormatOptions);
        Assert.DoesNotContain("Chapter2Qpfile", viewModel.SaveFormatOptions);
        Assert.Equal(9, viewModel.SaveFormatOptions.Count);
    }

    [Fact]
    public void XmlLanguageDisplayOptionsMatchMainWindowFormat()
    {
        var viewModel = CreateViewModel(
            CreateOwner(),
            null,
            null,
            new AppLocalizationManager("en-US"));

        var index = viewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        var entry = viewModel.XmlLanguageDisplayOptions[index];

        Assert.Equal("jpn", entry.MainText);
        Assert.Equal("Japanese", entry.RemarkText);
        Assert.Equal("jpn（Japanese）", entry.DisplayText);
    }

    [Fact]
    public void XmlLanguageDisplayOptionsRefreshAfterUiLanguageSwitch()
    {
        var localizer = new AppLocalizationManager("en-US");
        var owner = CreateOwner(localizer: localizer);
        var viewModel = CreateViewModel(owner, null, null, localizer);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        localizer.SetCulture("zh-CN");
        var entry = viewModel.XmlLanguageDisplayOptions[viewModel.XmlLanguageOptions.ToList().IndexOf("und")];

        Assert.Contains(nameof(SettingsToolViewModel.XmlLanguageDisplayOptions), notifications);
        Assert.Equal("未确定", entry.RemarkText);
        Assert.Equal("und（未确定）", entry.DisplayText);
    }

    [Fact]
    public void DisposedViewModelStopsRefreshingLocalizedOptions()
    {
        var localizer = new AppLocalizationManager("en-US");
        var owner = CreateOwner(localizer: localizer);
        var viewModel = CreateViewModel(owner, null, null, localizer);
        var notifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsToolViewModel.XmlLanguageDisplayOptions))
            {
                notifications++;
            }
        };

        (viewModel as IDisposable)?.Dispose();
        localizer.SetCulture("zh-CN");

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task AppearanceChangesApplyThemeImmediately()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(Language: "en-US"));
        var themeStore = new FakeThemeSettingsStore(ThemeSettings.Default);
        var themeApplication = new FakeThemeApplicationService();
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(
            owner,
            appStore,
            themeStore,
            new AppLocalizationManager("en-US"),
            themeApplicationService: themeApplication);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        SelectPreset(viewModel, "gruvbox-dark");

        Assert.Equal("gruvbox-dark", themeApplication.LastApplied?.PresetId);
        Assert.Equal(ThemeSettings.Default, themeStore.Current);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task DiscardUnsavedAppearanceChangesRestoresLoadedTheme()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(Language: "en-US"));
        var themeStore = new FakeThemeSettingsStore(new ThemeSettings("solarized-light"));
        var themeApplication = new FakeThemeApplicationService();
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(
            owner,
            appStore,
            themeStore,
            new AppLocalizationManager("en-US"),
            themeApplicationService: themeApplication);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        SelectPreset(viewModel, "ayu-dark");
        viewModel.DiscardUnsavedAppearanceChanges();

        Assert.Equal("solarized-light", viewModel.SelectedThemePreset.Id);
        Assert.Equal("solarized-light", themeApplication.LastApplied?.PresetId);
        Assert.Equal("solarized-light", themeStore.Current.PresetId);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task DiscardAfterSaveKeepsSavedAppearanceChanges()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings());
        var themeStore = new FakeThemeSettingsStore(new ThemeSettings("solarized-light"));
        var themeApplication = new FakeThemeApplicationService();
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(
            owner,
            appStore,
            themeStore,
            new AppLocalizationManager("en-US"),
            themeApplicationService: themeApplication);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        SelectPreset(viewModel, "ayu-dark");
        await viewModel.SaveCommand.ExecuteAsync();
        viewModel.DiscardUnsavedAppearanceChanges();

        Assert.Equal("ayu-dark", viewModel.SelectedThemePreset.Id);
        Assert.Equal("ayu-dark", themeApplication.LastApplied?.PresetId);
        Assert.Equal("ayu-dark", themeStore.Current.PresetId);
    }

    [Fact]
    public async Task ResetSelectsDefaultPresetWithoutPersistingIt()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings());
        var themeStore = new FakeThemeSettingsStore(new ThemeSettings("ayu-dark"));
        var themeApplication = new FakeThemeApplicationService();
        var viewModel = CreateViewModel(
            CreateOwner(appStore),
            appStore,
            themeStore,
            new AppLocalizationManager("en-US"),
            themeApplicationService: themeApplication);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        await viewModel.ResetCommand.ExecuteAsync();

        Assert.Equal(ThemePresetCatalog.DefaultPresetId, viewModel.SelectedThemePreset.Id);
        Assert.Equal(ThemePresetCatalog.DefaultPresetId, themeApplication.LastApplied?.PresetId);
        Assert.Equal("ayu-dark", themeStore.Current.PresetId);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task PresetDisplayNamesRefreshWithoutChangingStableSelection()
    {
        var localizer = new AppLocalizationManager("en-US");
        var appStore = new FakeAppSettingsStore(new AppSettings(Language: "en-US"));
        var viewModel = CreateViewModel(
            CreateOwner(appStore, localizer),
            appStore,
            new FakeThemeSettingsStore(ThemeSettings.Default),
            localizer);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        SelectPreset(viewModel, "ayu-mirage");

        Assert.Equal("Ayu Mirage", viewModel.SelectedThemePreset.DisplayName);
        localizer.SetCulture("zh-CN");
        Assert.Equal("ayu-mirage", viewModel.SelectedThemePreset.Id);
        Assert.Equal("Ayu 幻景", viewModel.SelectedThemePreset.DisplayName);
        Assert.Contains("Ayu 幻景", viewModel.ThemePreviewAutomationName, StringComparison.Ordinal);

        localizer.SetCulture("ja-JP");
        Assert.Equal("ayu-mirage", viewModel.SelectedThemePreset.Id);
        Assert.Equal("Ayu ミラージュ", viewModel.SelectedThemePreset.DisplayName);
    }

    [Fact]
    public async Task ClearPathRestoresDiscoveryStatus()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FfprobePath: "missing"));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        await viewModel.ClearFfprobeCommand.ExecuteAsync();

        Assert.Null(viewModel.FfprobePath);
        Assert.Contains("PATH", viewModel.FfprobeStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolValidationUsesDirectoryExecutableExpansion()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executableName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        var executable = Path.Combine(root, executableName);
        await File.WriteAllTextAsync(executable, "");
        var appStore = new FakeAppSettingsStore(new AppSettings(FfprobePath: root));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));

        try
        {
            await viewModel.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Contains(executable, viewModel.FfprobeStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FfmpegPathRequiresDirectoryContainingFfprobe()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var ffprobe = Path.Combine(root, ToolExecutable("ffprobe"));
        await File.WriteAllTextAsync(ffprobe, "");
        var appStore = new FakeAppSettingsStore(new AppSettings(FfmpegPath: ffprobe));
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"));

        try
        {
            await viewModel.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Path must be a directory", viewModel.FfmpegStatus);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateToolsDiscoversAndFillsExternalToolPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mkvextract = Path.Combine(root, ToolExecutable("mkvextract"));
        var eac3to = Path.Combine(root, ToolExecutable("eac3to"));
        var ffprobe = Path.Combine(root, ToolExecutable("ffprobe"));
        await File.WriteAllTextAsync(mkvextract, "");
        await File.WriteAllTextAsync(eac3to, "");
        await File.WriteAllTextAsync(ffprobe, "");
        var appStore = new FakeAppSettingsStore(new AppSettings());
        var owner = CreateOwner(appStore);
        var locator = new FakeExternalToolLocator(new Dictionary<string, ExternalToolLocation>(StringComparer.OrdinalIgnoreCase)
        {
            ["mkvextract"] = new(true, mkvextract),
            ["eac3to"] = new(true, eac3to),
            ["ffprobe"] = new(true, ffprobe)
        });
        var viewModel = CreateViewModel(
            owner,
            appStore,
            new FakeThemeSettingsStore(ThemeSettings.Default),
            new AppLocalizationManager("en-US"),
            externalToolLocator: locator);

        try
        {
            await viewModel.LoadAsync(TestContext.Current.CancellationToken);
            await viewModel.ValidateToolsCommand.ExecuteAsync();

            Assert.Equal(mkvextract, viewModel.MkvToolnixPath);
            Assert.Equal(eac3to, viewModel.Eac3toPath);
            Assert.Equal(ffprobe, viewModel.FfprobePath);
            Assert.Equal(root, viewModel.FfmpegPath);
            Assert.Contains(mkvextract, viewModel.MkvToolnixStatus, StringComparison.Ordinal);
            Assert.Contains(eac3to, viewModel.Eac3toStatus, StringComparison.Ordinal);
            Assert.Contains(ffprobe, viewModel.FfprobeStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PickerCommandsUseInjectedPicker()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings());
        var picker = new FakeSettingsPicker("picked-directory", "picked-executable");
        var owner = CreateOwner(appStore);
        var viewModel = CreateViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeSettings.Default), new AppLocalizationManager("en-US"), picker);

        await viewModel.BrowseSaveDirectoryCommand.ExecuteAsync();
        await viewModel.BrowseFfprobeCommand.ExecuteAsync();

        Assert.Equal("picked-directory", viewModel.SaveDirectory);
        Assert.Equal("picked-executable", viewModel.FfprobePath);
    }

    private static void SelectPreset(SettingsToolViewModel viewModel, string presetId)
    {
        var index = viewModel.ThemePresets.ToList().FindIndex(option => option.Id == presetId);
        Assert.True(index >= 0, $"Preset not found: {presetId}");
        viewModel.SelectedThemePresetIndex = index;
    }

    private static SettingsToolViewModel CreateViewModel(
        MainWindowViewModel owner,
        ISettingsStore<AppSettings>? appSettingsStore,
        ISettingsStore<ThemeSettings>? themeSettingsStore,
        IAppLocalizer? localizer = null,
        ISettingsPickerService? picker = null,
        IExternalToolLocator? externalToolLocator = null,
        IThemeApplicationService? themeApplicationService = null) =>
        new(
            owner,
            appSettingsStore,
            themeSettingsStore,
            localizer,
            picker,
            externalToolLocator,
            themeApplicationService,
            autoLoad: false);

    private static MainWindowViewModel CreateOwner(
        ISettingsStore<AppSettings>? appSettingsStore = null,
        IAppLocalizer? localizer = null)
    {
        var logService = new ApplicationLogPanelProvider();

        return new MainWindowViewModel(
            new FakeLoadService(),
            new FakeSaveService(),
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            new FakeWindowService(),
            new ChapterTimeFormatter(),
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            appSettingsStore: appSettingsStore,
            localizer: localizer ?? new AppLocalizationManager("en-US"));
    }

    private sealed class FakeAppSettingsStore(AppSettings initial) : ISettingsStore<AppSettings>
    {
        public AppSettings Current { get; private set; } = initial;

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeThemeSettingsStore(ThemeSettings initial) : ISettingsStore<ThemeSettings>
    {
        public ThemeSettings Current { get; private set; } = initial;

        public ValueTask<ThemeSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(ThemeSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingAppSettingsStore : ISettingsStore<AppSettings>
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromException<AppSettings>(new CorruptSettingsFileException("appsettings.json", "appsettings.json.bad", new InvalidDataException()));

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class ThrowingThemeSettingsStore : ISettingsStore<ThemeSettings>
    {
        public ValueTask<ThemeSettings> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromException<ThemeSettings>(new CorruptSettingsFileException("theme-settings.json", "theme-settings.json.bad", new InvalidDataException()));

        public ValueTask SaveAsync(ThemeSettings settings, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FakeThemeApplicationService : IThemeApplicationService
    {
        public ThemeSettings? LastApplied { get; private set; }

        public void Apply(ThemeSettings settings) => LastApplied = settings;
    }

    private sealed class FakeSettingsPicker(string directory, string executable) : ISettingsPickerService
    {
        public ValueTask<string?> PickDirectoryAsync(string title, CancellationToken cancellationToken) => ValueTask.FromResult<string?>(directory);

        public ValueTask<string?> PickExecutableAsync(string title, CancellationToken cancellationToken) => ValueTask.FromResult<string?>(executable);
    }

    private sealed class FakeExternalToolLocator(IReadOnlyDictionary<string, ExternalToolLocation> locations) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                locations.TryGetValue(toolId, out var location)
                    ? location
                    : new ExternalToolLocation(false, null, ChapterDiagnosticCode.MissingDependency, toolId));
    }

    private sealed class FakeLoadService : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterImportResult(
                true,
                [new ChapterImportSource(path, [new ChapterImportEntry("default", "default", new ChapterSet(path, path, ChapterImportFormat.Ogm, 24, TimeSpan.Zero, []))])],
                []));
    }

    private static string ToolExecutable(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", []));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
