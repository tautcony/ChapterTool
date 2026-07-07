using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class SettingsMigrationTests
{
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
        var settingsPath = Path.Combine(root, "theme-colors.json");
        await File.WriteAllTextAsync(settingsPath, "{");
        var store = new ThemeSettingsStore(root);

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(settingsPath, exception.SettingsPath);
        Assert.Equal(settingsPath + ".corrupt", exception.BackupPath);
        Assert.False(File.Exists(settingsPath));
        Assert.Equal("{", await File.ReadAllTextAsync(exception.BackupPath));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
