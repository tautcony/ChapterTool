using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class SettingsMigrationTests
{
    [Fact]
    public async Task App_settings_loads_legacy_chaptertool_json_keys()
    {
        var root = CreateTempDirectory();
        var legacyPath = Path.Combine(root, "chaptertool.json");
        await File.WriteAllTextAsync(
            legacyPath,
            """
            {
              "Software\\ChapterTool.SavingPath": "D:\\\\Output",
              "Software\\ChapterTool.Language": "en-US",
              "Software\\ChapterTool.Location": "{X=12,Y=34}",
              "Software\\ChapterTool.mkvToolnixPath": "C:\\\\Tools\\MKVToolNix",
              "Software\\ChapterTool.eac3toPath": "C:\\\\Tools\\eac3to\\eac3to.exe"
            }
            """);

        var store = new AppSettingsStore(root, [root]);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(@"D:\\Output", settings.SavingPath);
        Assert.Equal("en-US", settings.Language);
        Assert.Equal(new WindowLocation(12, 34), settings.MainWindowLocation);
        Assert.Equal(@"C:\\Tools\MKVToolNix", settings.MkvToolnixPath);
        Assert.Equal(@"C:\\Tools\eac3to\eac3to.exe", settings.Eac3toPath);
        Assert.Equal("Txt", settings.DefaultSaveFormat);
        Assert.Equal("und", settings.DefaultXmlLanguage);
    }

    [Fact]
    public async Task App_settings_prefers_new_typed_file_over_legacy_file()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(root, "chaptertool.json"),
            """{"Software\\ChapterTool.Language":"en-US"}""");

        var store = new AppSettingsStore(root, [root]);
        await store.SaveAsync(
            new AppSettings(Language: "", SavingPath: @"E:\\Saved", DefaultSaveFormat: "Xml", DefaultXmlLanguage: "ja"),
            TestContext.Current.CancellationToken);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("", settings.Language);
        Assert.Equal(@"E:\\Saved", settings.SavingPath);
        Assert.Equal("Xml", settings.DefaultSaveFormat);
        Assert.Equal("ja", settings.DefaultXmlLanguage);
    }

    [Fact]
    public async Task App_settings_cache_returns_saved_values_without_stale_reads()
    {
        var root = CreateTempDirectory();
        var store = new AppSettingsStore(root, [root]);

        var initial = await store.LoadAsync(TestContext.Current.CancellationToken);
        await store.SaveAsync(new AppSettings(Language: "zh-CN", FfprobePath: @"C:\Tools\ffprobe.exe"), TestContext.Current.CancellationToken);
        var saved = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("", initial.Language);
        Assert.Equal("zh-CN", saved.Language);
        Assert.Equal(@"C:\Tools\ffprobe.exe", saved.FfprobePath);
    }

    [Fact]
    public async Task Theme_settings_loads_legacy_color_config_in_six_slot_order()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(root, "color-config.json"),
            """["#010203","#111213","#212223","#313233","#414243","#515253"]""");

        var store = new ThemeSettingsStore(root, [root]);

        var theme = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("#010203", theme.BackChange);
        Assert.Equal("#111213", theme.TextBack);
        Assert.Equal("#212223", theme.MouseOverColor);
        Assert.Equal("#313233", theme.MouseDownColor);
        Assert.Equal("#414243", theme.BorderBackColor);
        Assert.Equal("#515253", theme.TextFrontColor);
        Assert.Equal(
            ["BackChange", "TextBack", "MouseOverColor", "MouseDownColor", "BorderBackColor", "TextFrontColor"],
            theme.OrderedSlots.Select(static slot => slot.Name).ToArray());
    }

    [Fact]
    public async Task Theme_settings_ignores_invalid_legacy_color_values()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(root, "color-config.json"),
            """["#010203","not-a-color","#212223","#313233","#414243","#515253"]""");

        var store = new ThemeSettingsStore(root, [root]);

        var theme = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeColorSettings.Default.TextBack, theme.TextBack);
        Assert.Equal("#212223", theme.MouseOverColor);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
