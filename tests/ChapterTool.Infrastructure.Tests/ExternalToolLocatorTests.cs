using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ExternalToolLocatorTests
{
    [Fact]
    public async Task LocateAsync_uses_configured_mkvtoolnix_path_before_search_paths()
    {
        var root = CreateTempDirectory();
        var configuredDirectory = Path.Combine(root, "configured");
        Directory.CreateDirectory(configuredDirectory);
        var expectedToolPath = Path.Combine(configuredDirectory, ToolExecutable("mkvextract"));
        await CreateToolFileAsync(expectedToolPath);

        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        await CreateToolFileAsync(Path.Combine(searchDirectory, ToolExecutable("mkvextract")));

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(MkvToolnixPath: configuredDirectory),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [searchDirectory], new FakeMkvToolNixInstallProbe(Path.Combine(root, "probe")));

        var location = await locator.LocateAsync("mkvextract", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_configured_mkvextract_executable_before_search_paths()
    {
        var root = CreateTempDirectory();
        var configuredExecutable = Path.Combine(root, ToolExecutable("custom-mkvextract"));
        await CreateToolFileAsync(configuredExecutable);

        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        await CreateToolFileAsync(Path.Combine(searchDirectory, ToolExecutable("mkvextract")));

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(MkvToolnixPath: configuredExecutable),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [searchDirectory], new FakeMkvToolNixInstallProbe(Path.Combine(root, "probe")));

        var location = await locator.LocateAsync("mkvextract", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(configuredExecutable, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_search_path_before_platform_install_discovery()
    {
        var root = CreateTempDirectory();
        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        var expectedToolPath = Path.Combine(searchDirectory, ToolExecutable("mkvextract"));
        await CreateToolFileAsync(expectedToolPath);
        var platformToolPath = Path.Combine(root, "platform", ToolExecutable("mkvextract"));
        Directory.CreateDirectory(Path.GetDirectoryName(platformToolPath)!);
        await CreateToolFileAsync(platformToolPath);

        var locator = CreateLocatorWithoutDefaultCandidates(
            new ChapterToolSettingsStore(root),
            [searchDirectory],
            new FakeMkvToolNixInstallProbe(platformToolPath));

        var location = await locator.LocateAsync("mkvextract", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_platform_mkvtoolnix_discovery_for_mkvextract()
    {
        var root = CreateTempDirectory();
        var platformToolPath = Path.Combine(root, "platform", ToolExecutable("mkvextract"));
        Directory.CreateDirectory(Path.GetDirectoryName(platformToolPath)!);
        await CreateToolFileAsync(platformToolPath);

        var locator = CreateLocatorWithoutDefaultCandidates(
            new ChapterToolSettingsStore(root),
            [],
            new FakeMkvToolNixInstallProbe(platformToolPath));

        var location = await locator.LocateAsync("mkvextract", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(platformToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_does_not_use_platform_discovery_for_eac3to()
    {
        var root = CreateTempDirectory();
        var platformToolPath = Path.Combine(root, "platform", ToolExecutable("mkvextract"));
        Directory.CreateDirectory(Path.GetDirectoryName(platformToolPath)!);
        await CreateToolFileAsync(platformToolPath);

        var locator = CreateLocatorWithoutDefaultCandidates(
            new ChapterToolSettingsStore(root),
            [],
            new FakeMkvToolNixInstallProbe(platformToolPath));

        var location = await locator.LocateAsync("eac3to", TestContext.Current.CancellationToken);

        Assert.False(location.Found);
        Assert.Null(location.Path);
        Assert.Equal(ChapterDiagnosticCode.MissingDependency, location.DiagnosticCode);
    }

    [Fact]
    public async Task LocateAsync_returns_missing_dependency_when_tool_is_absent()
    {
        var root = CreateTempDirectory();
        var settingsStore = new ChapterToolSettingsStore(root);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [root], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("eac3to", TestContext.Current.CancellationToken);

        Assert.False(location.Found);
        Assert.Null(location.Path);
        Assert.Equal(ChapterDiagnosticCode.MissingDependency, location.DiagnosticCode);
    }

    [Fact]
    public async Task LocateAsync_ignores_configured_file_that_is_not_executable()
    {
        var root = CreateTempDirectory();
        var configuredPath = Path.Combine(root, OperatingSystem.IsWindows() ? "ffprobe.txt" : "ffprobe");
        await File.WriteAllTextAsync(configuredPath, "");
        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(new AppSettings(FfprobePath: configuredPath), TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.False(location.Found);
        Assert.Null(location.Path);
        Assert.Equal(ChapterDiagnosticCode.MissingDependency, location.DiagnosticCode);
    }

    [Fact]
    public async Task LocateAsync_uses_configured_ffprobe_executable_before_ffmpeg_directory_and_search_paths()
    {
        var root = CreateTempDirectory();
        var configuredExecutable = Path.Combine(root, ToolExecutable("custom-ffprobe"));
        await CreateToolFileAsync(configuredExecutable);
        var ffmpegDirectory = Path.Combine(root, "ffmpeg");
        Directory.CreateDirectory(ffmpegDirectory);
        await CreateToolFileAsync(Path.Combine(ffmpegDirectory, ToolExecutable("ffprobe")));
        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        await CreateToolFileAsync(Path.Combine(searchDirectory, ToolExecutable("ffprobe")));

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(FfprobePath: configuredExecutable, FfmpegPath: ffmpegDirectory),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [searchDirectory], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(configuredExecutable, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_search_path_after_configured_path_is_cleared()
    {
        var root = CreateTempDirectory();
        var configuredDirectory = Path.Combine(root, "configured");
        Directory.CreateDirectory(configuredDirectory);
        await CreateToolFileAsync(Path.Combine(configuredDirectory, ToolExecutable("ffprobe")));
        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        var expectedToolPath = Path.Combine(searchDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(expectedToolPath);
        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(new AppSettings(FfprobePath: configuredDirectory), TestContext.Current.CancellationToken);
        await settingsStore.SaveAsync(new AppSettings(FfprobePath: null), TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [searchDirectory], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_updated_settings_after_configured_path_is_cleared_on_existing_locator()
    {
        var root = CreateTempDirectory();
        var configuredDirectory = Path.Combine(root, "configured");
        Directory.CreateDirectory(configuredDirectory);
        var configuredToolPath = Path.Combine(configuredDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(configuredToolPath);
        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        var searchToolPath = Path.Combine(searchDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(searchToolPath);
        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(new AppSettings(FfprobePath: configuredDirectory), TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [searchDirectory], new FakeMkvToolNixInstallProbe());

        var configuredLocation = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);
        await settingsStore.SaveAsync(new AppSettings(FfprobePath: null), TestContext.Current.CancellationToken);
        var searchLocation = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.Equal(configuredToolPath, configuredLocation.Path);
        Assert.Equal(searchToolPath, searchLocation.Path);
    }

    [Fact]
    public async Task LocateAsync_reuses_cached_found_location()
    {
        var root = CreateTempDirectory();
        var toolPath = Path.Combine(root, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(toolPath);
        var provider = new CountingDefaultCandidateProvider(toolPath);
        var locator = new ExternalToolLocator(
            new ChapterToolSettingsStore(root),
            [],
            new FakeMkvToolNixInstallProbe(),
            provider);

        var first = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);
        var second = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.Equal(toolPath, first.Path);
        Assert.Equal(toolPath, second.Path);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task LocateAsync_rescans_when_cached_found_location_disappears()
    {
        var root = CreateTempDirectory();
        var firstDirectory = Path.Combine(root, "first");
        var secondDirectory = Path.Combine(root, "second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        var firstToolPath = Path.Combine(firstDirectory, ToolExecutable("ffprobe"));
        var secondToolPath = Path.Combine(secondDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(firstToolPath);
        await CreateToolFileAsync(secondToolPath);
        var locator = CreateLocatorWithoutDefaultCandidates(
            new ChapterToolSettingsStore(root),
            [firstDirectory, secondDirectory],
            new FakeMkvToolNixInstallProbe());

        var first = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);
        File.Delete(firstToolPath);
        var second = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.Equal(firstToolPath, first.Path);
        Assert.Equal(secondToolPath, second.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_configured_ffprobe_directory()
    {
        var root = CreateTempDirectory();
        var configuredDirectory = Path.Combine(root, "configured");
        Directory.CreateDirectory(configuredDirectory);
        var expectedToolPath = Path.Combine(configuredDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(expectedToolPath);

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(FfprobePath: configuredDirectory),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_uses_configured_ffmpeg_directory_for_ffprobe()
    {
        var root = CreateTempDirectory();
        var ffmpegDirectory = Path.Combine(root, "ffmpeg");
        Directory.CreateDirectory(ffmpegDirectory);
        var expectedToolPath = Path.Combine(ffmpegDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(expectedToolPath);

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(FfmpegPath: ffmpegDirectory),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public async Task LocateAsync_does_not_treat_ffmpeg_path_as_ffprobe_executable()
    {
        var root = CreateTempDirectory();
        var ffprobePath = Path.Combine(root, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(ffprobePath);

        var settingsStore = new ChapterToolSettingsStore(root);
        await settingsStore.SaveAsync(
            new AppSettings(FfmpegPath: ffprobePath),
            TestContext.Current.CancellationToken);
        var locator = CreateLocatorWithoutDefaultCandidates(settingsStore, [], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.False(location.Found);
        Assert.Null(location.Path);
        Assert.Equal(ChapterDiagnosticCode.MissingDependency, location.DiagnosticCode);
    }

    [Fact]
    public async Task LocateAsync_uses_search_directory_for_ffprobe_when_unconfigured()
    {
        var root = CreateTempDirectory();
        var searchDirectory = Path.Combine(root, "search");
        Directory.CreateDirectory(searchDirectory);
        var expectedToolPath = Path.Combine(searchDirectory, ToolExecutable("ffprobe"));
        await CreateToolFileAsync(expectedToolPath);

        var locator = CreateLocatorWithoutDefaultCandidates(new ChapterToolSettingsStore(root), [searchDirectory], new FakeMkvToolNixInstallProbe());

        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);

        Assert.True(location.Found);
        Assert.Equal(expectedToolPath, location.Path);
    }

    [Fact]
    public void WindowsProbe_expands_registry_install_directories_and_display_icons()
    {
        var root = CreateTempDirectory();
        var installDirectory = Path.Combine(root, "MKVToolNix");
        Directory.CreateDirectory(installDirectory);
        var expectedToolPath = Path.Combine(installDirectory, ToolExecutable("mkvextract"));
        CreateToolFile(expectedToolPath);
        var displayIcon = Path.Combine(installDirectory, OperatingSystem.IsWindows() ? "mkvtoolnix-gui.exe,0" : "mkvtoolnix-gui,0");
        var quotedDisplayIcon = $"\"{Path.Combine(installDirectory, "mkvtoolnix-gui.exe")}\",0";
        var probe = new WindowsMkvToolNixInstallProbe(
            new FakeWindowsRegistryInstallProbe([installDirectory, displayIcon, quotedDisplayIcon]),
            enabled: true);

        var candidates = probe.FindMkvExtractCandidates(ToolExecutable("mkvextract")).ToArray();

        Assert.Contains(expectedToolPath, candidates);
        Assert.Equal(3, candidates.Count(candidate => string.Equals(candidate, expectedToolPath, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MacProbe_finds_mkvextract_inside_versioned_app_bundle()
    {
        var root = CreateTempDirectory();
        var expectedToolPath = Path.Combine(root, "MKVToolNix-96.0.app", "Contents", "MacOS", "mkvextract");
        Directory.CreateDirectory(Path.GetDirectoryName(expectedToolPath)!);
        CreateToolFile(expectedToolPath);
        var probe = new MacMkvToolNixInstallProbe([root], enabled: true);

        var candidates = probe.FindMkvExtractCandidates("mkvextract").ToArray();

        Assert.Contains(expectedToolPath, candidates);
    }

    [Fact]
    public void UnixProbe_returns_no_platform_install_candidates()
    {
        var probe = new UnixMkvToolNixInstallProbe();

        Assert.Empty(probe.FindMkvExtractCandidates("mkvextract"));
    }

    private static string ToolExecutable(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    private static async Task CreateToolFileAsync(string path)
    {
        await File.WriteAllTextAsync(path, "");
        MarkExecutable(path);
    }

    private static void CreateToolFile(string path)
    {
        File.WriteAllText(path, "");
        MarkExecutable(path);
    }

    private static void MarkExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static ExternalToolLocator CreateLocatorWithoutDefaultCandidates(
        ChapterToolSettingsStore settingsStore,
        IReadOnlyList<string> searchDirectories,
        IMkvToolNixInstallProbe mkvToolNixInstallProbe) =>
        new(settingsStore, searchDirectories, mkvToolNixInstallProbe, new NoDefaultCandidateProvider());

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeMkvToolNixInstallProbe(params string[] candidates) : IMkvToolNixInstallProbe
    {
        public IEnumerable<string> FindMkvExtractCandidates(string executableName) => candidates;
    }

    private sealed class NoDefaultCandidateProvider : IExternalToolDefaultCandidateProvider
    {
        public IEnumerable<string> FindCandidates(string toolId, string executableName) => [];
    }

    private sealed class CountingDefaultCandidateProvider(params string[] candidates) : IExternalToolDefaultCandidateProvider
    {
        public int CallCount { get; private set; }

        public IEnumerable<string> FindCandidates(string toolId, string executableName)
        {
            CallCount++;
            return candidates;
        }
    }

    private sealed class FakeWindowsRegistryInstallProbe(IReadOnlyList<string> values) : IWindowsRegistryInstallProbe
    {
        public IEnumerable<string> ReadMkvToolNixInstallValues() => values;
    }
}

internal static class ChapterToolSettingsStoreTestExtensions
{
    public static ValueTask SaveAsync(
        this ChapterToolSettingsStore store,
        AppSettings application,
        CancellationToken cancellationToken) =>
        store.UpdateAsync(
            current => current with { Application = application },
            cancellationToken);
}
