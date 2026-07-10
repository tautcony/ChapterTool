using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class SettingsMigrationTests
{
    [Fact]
    public async Task Theme_settings_persist_only_normalized_preset_id()
    {
        var root = CreateTempDirectory();
        var store = new ThemeSettingsStore(root);

        await store.SaveAsync(new ThemeSettings("solarized-dark"), TestContext.Current.CancellationToken);

        Assert.Equal(new ThemeSettings("solarized-dark"), await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("{\n  \"presetId\": \"solarized-dark\"\n}", await File.ReadAllTextAsync(Path.Combine(root, "theme-settings.json")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-preset")]
    public async Task Theme_settings_unknown_id_falls_back_without_rewriting(string presetId)
    {
        var root = CreateTempDirectory();
        var settingsPath = Path.Combine(root, "theme-settings.json");
        var original = $"{{\"presetId\":\"{presetId}\"}}";
        await File.WriteAllTextAsync(settingsPath, original);
        var store = new ThemeSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeSettings.Default, settings);
        Assert.Equal(original, await File.ReadAllTextAsync(settingsPath));
    }

    [Fact]
    public async Task Theme_settings_missing_file_ignores_legacy_file()
    {
        var root = CreateTempDirectory();
        var legacyPath = Path.Combine(root, "theme-colors.json");
        await File.WriteAllTextAsync(legacyPath, "{\"backChange\":\"#010203\"}");
        var store = new ThemeSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeSettings.Default, settings);
        Assert.True(File.Exists(legacyPath));
        Assert.False(File.Exists(Path.Combine(root, "theme-settings.json")));
    }

    [Fact]
    public async Task App_settings_cache_returns_saved_values_without_stale_reads()
    {
        var root = CreateTempDirectory();
        var store = new AppSettingsStore(root);

        var initial = await store.LoadAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(new AppSettings(Language: "zh-CN", FfprobePath: @"C:\Tools\ffprobe.exe"), TestContext.Current.CancellationToken);
        var saved = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("", initial.Language);
        Assert.Equal("zh-CN", saved.Language);
        Assert.Equal(@"C:\Tools\ffprobe.exe", saved.FfprobePath);
    }

    [Fact]
    public async Task App_settings_preserves_corrupt_current_file_and_surfaces_error()
    {
        var root = CreateTempDirectory();
        var settingsPath = Path.Combine(root, "appsettings.json");
        await File.WriteAllTextAsync(settingsPath, "{");
        var store = new AppSettingsStore(root);

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(settingsPath, exception.SettingsPath);
        Assert.Equal(settingsPath + ".corrupt", exception.BackupPath);
        Assert.False(File.Exists(settingsPath));
        Assert.Equal("{", await File.ReadAllTextAsync(exception.BackupPath));
    }

    [Fact]
    public async Task Theme_settings_preserves_corrupt_current_file_and_surfaces_error()
    {
        var root = CreateTempDirectory();
        var settingsPath = Path.Combine(root, "theme-settings.json");
        await File.WriteAllTextAsync(settingsPath, "{");
        var store = new ThemeSettingsStore(root);

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(settingsPath, exception.SettingsPath);
        Assert.Equal(settingsPath + ".corrupt", exception.BackupPath);
        Assert.False(File.Exists(settingsPath));
        Assert.Equal("{", await File.ReadAllTextAsync(exception.BackupPath));
    }

    [Fact]
    public async Task Theme_settings_concurrent_corrupt_loads_surface_structured_errors()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "theme-settings.json"), "{");
        var first = new ThemeSettingsStore(root);
        var second = new ThemeSettingsStore(root);

        var results = await Task.WhenAll(
            CaptureCorruptLoadAsync(first),
            CaptureCorruptLoadAsync(second));

        Assert.All(results, exception =>
        {
            Assert.NotNull(exception);
            Assert.True(File.Exists(exception!.BackupPath));
        });
    }

    [Fact]
    public async Task App_settings_concurrent_saves_do_not_race_on_temp_file()
    {
        var root = CreateTempDirectory();
        var store = new AppSettingsStore(root);
        var payload = new string('x', 100_000);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
            store.SaveAsync(new AppSettings(Language: "en-US", FfprobePath: $"{payload}-{index}"), TestContext.Current.CancellationToken).AsTask()));

        var saved = await store.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("en-US", saved.Language);
        Assert.Empty(Directory.EnumerateFiles(root, "appsettings.json.*.tmp"));
    }

    [Fact]
    public async Task Theme_settings_concurrent_saves_do_not_race_on_temp_file()
    {
        var root = CreateTempDirectory();
        var store = new ThemeSettingsStore(root);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
            store.SaveAsync(new ThemeSettings(index % 2 == 0 ? "solarized-dark" : "ayu-light"), TestContext.Current.CancellationToken).AsTask()));

        _ = await store.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(Directory.EnumerateFiles(root, "theme-settings.json.*.tmp"));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<CorruptSettingsFileException?> CaptureCorruptLoadAsync(AppSettingsStore store)
    {
        try
        {
            _ = await store.LoadAsync(TestContext.Current.CancellationToken);
            return null;
        }
        catch (CorruptSettingsFileException exception)
        {
            return exception;
        }
    }

    private static async Task<CorruptSettingsFileException?> CaptureCorruptLoadAsync(ThemeSettingsStore store)
    {
        try
        {
            _ = await store.LoadAsync(TestContext.Current.CancellationToken);
            return null;
        }
        catch (CorruptSettingsFileException exception)
        {
            return exception;
        }
    }
}
