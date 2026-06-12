using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests;

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
            DefaultXmlLanguage: "ja"));
        var themeStore = new FakeThemeSettingsStore(ThemeColorSettings.Default);
        var owner = CreateOwner(appStore);
        var viewModel = new SettingsToolViewModel(owner, appStore, themeStore, new AppLocalizationManager("en-US"));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedLanguage = "ja-JP";
        viewModel.SaveDirectory = "new-out";
        viewModel.MkvToolnixPath = null;
        viewModel.Eac3toPath = "new-eac3to";
        viewModel.FfprobePath = "new-ffprobe";
        viewModel.FfmpegPath = "new-ffmpeg";
        viewModel.DefaultSaveFormatIndex = viewModel.SaveFormatOptions.ToList().IndexOf("Json");
        viewModel.DefaultXmlLanguageIndex = viewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        viewModel.ColorSlots[0].Value = "#010203";

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Equal("ja-JP", appStore.Current.Language);
        Assert.Equal("new-out", appStore.Current.SavingPath);
        Assert.Null(appStore.Current.MkvToolnixPath);
        Assert.Equal("new-eac3to", appStore.Current.Eac3toPath);
        Assert.Equal("new-ffprobe", appStore.Current.FfprobePath);
        Assert.Equal("new-ffmpeg", appStore.Current.FfmpegPath);
        Assert.Equal("Json", appStore.Current.DefaultSaveFormat);
        Assert.Equal("jpn", appStore.Current.DefaultXmlLanguage);
        Assert.Equal(ChapterExportFormat.Json, owner.SaveFormat);
        Assert.Equal("jpn", owner.XmlLanguage);
        Assert.Equal("new-out", owner.SaveDirectory);
        Assert.Equal("ja-JP", owner.UiLanguage);
        Assert.Equal("#010203", themeStore.Current.BackChange);
    }

    [Fact]
    public async Task ClearPathRestoresDiscoveryStatus()
    {
        var appStore = new FakeAppSettingsStore(new AppSettings(FfprobePath: "missing"));
        var owner = CreateOwner(appStore);
        var viewModel = new SettingsToolViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeColorSettings.Default), new AppLocalizationManager("en-US"));
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
        var viewModel = new SettingsToolViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeColorSettings.Default), new AppLocalizationManager("en-US"));

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
        var viewModel = new SettingsToolViewModel(
            owner,
            appStore,
            new FakeThemeSettingsStore(ThemeColorSettings.Default),
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
        var viewModel = new SettingsToolViewModel(owner, appStore, new FakeThemeSettingsStore(ThemeColorSettings.Default), new AppLocalizationManager("en-US"), picker);

        await viewModel.BrowseSaveDirectoryCommand.ExecuteAsync();
        await viewModel.BrowseFfprobeCommand.ExecuteAsync();

        Assert.Equal("picked-directory", viewModel.SaveDirectory);
        Assert.Equal("picked-executable", viewModel.FfprobePath);
    }

    private static MainWindowViewModel CreateOwner(ISettingsStore<AppSettings>? appSettingsStore = null)
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
            localizer: new AppLocalizationManager("en-US"));
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

    private sealed class FakeThemeSettingsStore(ThemeColorSettings initial) : ISettingsStore<ThemeColorSettings>
    {
        public ThemeColorSettings Current { get; private set; } = initial;

        public ValueTask<ThemeColorSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(ThemeColorSettings settings, CancellationToken cancellationToken)
        {
            Current = settings;
            return ValueTask.CompletedTask;
        }
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
                    : new ExternalToolLocation(false, null, "MissingDependency", toolId));
    }

    private sealed class FakeLoadService : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterImportResult(
                true,
                [new ChapterInfoGroup(path, [new ChapterSourceOption("default", "default", new ChapterInfo(path, path, 0, "OGM", 24, TimeSpan.Zero, Array.Empty<Chapter>()))], 0)],
                Array.Empty<ChapterDiagnostic>()));
    }

    private static string ToolExecutable(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", Array.Empty<ChapterDiagnostic>()));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
