using System.Text.Json;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class SettingsMigrationTests
{
    [Fact]
    public async Task Saving_aggregate_once_produces_one_versioned_document_with_all_child_values()
    {
        var root = CreateTempDirectory();
        var documentStore = new ChapterToolSettingsStore(root);

        await documentStore.SaveAsync(
            new ChapterToolSettings
            {
                Application = new AppSettings(Language: "zh-CN", FfprobePath: @"C:\Tools\ffprobe.exe"),
                Theme = new ThemeSettings("solarized-dark"),
                Font = new FontSettings("Noto Sans", "JetBrains Mono"),
            },
            TestContext.Current.CancellationToken);

        var settings = await documentStore.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ChapterToolSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal("zh-CN", settings.Application.Language);
        Assert.Equal("utf8", settings.Application.OutputTextEncoding);
        Assert.Equal(@"C:\Tools\ffprobe.exe", settings.Application.FfprobePath);
        Assert.Equal("solarized-dark", settings.Theme.PresetId);
        Assert.Equal(new FontSettings("Noto Sans", "JetBrains Mono"), settings.Font);

        using var json = await ReadDocumentAsync(root);
        Assert.Equal(ChapterToolSettings.CurrentSchemaVersion, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("zh-CN", json.RootElement.GetProperty("application").GetProperty("language").GetString());
        Assert.Equal("solarized-dark", json.RootElement.GetProperty("theme").GetProperty("presetId").GetString());
        Assert.Equal("Noto Sans", json.RootElement.GetProperty("font").GetProperty("uiFontFamily").GetString());
        Assert.False(File.Exists(Path.Combine(root, "appsettings.json")));
        Assert.False(File.Exists(Path.Combine(root, "theme-settings.json")));
        Assert.False(File.Exists(Path.Combine(root, "font-settings.json")));
    }

    [Fact]
    public async Task Missing_configuration_returns_defaults_without_creating_a_file()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ChapterToolSettings.Default, settings);
        Assert.False(File.Exists(SettingsPath(root)));
    }

    [Fact]
    public async Task Non_lowercase_output_text_encoding_falls_back_to_utf8()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);

        await store.SaveAsync(
            new ChapterToolSettings { Application = new AppSettings(OutputTextEncoding: "UTF16LE") },
            TestContext.Current.CancellationToken);

        using var json = await ReadDocumentAsync(root);
        Assert.Equal("utf8", json.RootElement.GetProperty("application").GetProperty("outputTextEncoding").GetString());
    }

    [Fact]
    public async Task Unchanged_document_loads_reuse_the_cached_aggregate_snapshot()
    {
        var root = CreateTempDirectory();
        await new ChapterToolSettingsStore(root).SaveAsync(
            new ChapterToolSettings { Application = new AppSettings(Language: "ja-JP") },
            TestContext.Current.CancellationToken);
        var store = new ChapterToolSettingsStore(root);

        var first = await store.LoadAsync(TestContext.Current.CancellationToken);
        var second = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal("ja-JP", second.Application.Language);
    }

    [Fact]
    public async Task Predecessor_files_are_ignored_when_unified_document_is_absent()
    {
        var root = CreateTempDirectory();
        var appPath = Path.Combine(root, "appsettings.json");
        var themePath = Path.Combine(root, "theme-settings.json");
        var fontPath = Path.Combine(root, "font-settings.json");
        await File.WriteAllTextAsync(appPath, "{\"language\":\"ja-JP\",\"ffmpegPath\":\"/tools\"}");
        await File.WriteAllTextAsync(themePath, "{\"presetId\":\"ayu-mirage\"}");
        await File.WriteAllTextAsync(fontPath, "{\"uiFontFamily\":\" Inter \",\"monospaceFontFamily\":\" Mono \"}");
        var predecessorBytes = new Dictionary<string, byte[]>
        {
            [appPath] = await File.ReadAllBytesAsync(appPath),
            [themePath] = await File.ReadAllBytesAsync(themePath),
            [fontPath] = await File.ReadAllBytesAsync(fontPath),
        };

        var settings = await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ChapterToolSettings.Default, settings);
        Assert.False(File.Exists(SettingsPath(root)));
        foreach (var (path, bytes) in predecessorBytes)
        {
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
    }

    [Fact]
    public async Task Unversioned_section_document_upgrades_to_current_version()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(
            SettingsPath(root),
            "{\"application\":{\"language\":\"en-US\"},\"theme\":{\"presetId\":\"ayu-light\"},\"font\":{}}");

        var settings = await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-US", settings.Application.Language);
        Assert.Equal("ayu-light", settings.Theme.PresetId);
        using var json = await ReadDocumentAsync(root);
        Assert.Equal(ChapterToolSettings.CurrentSchemaVersion, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task Current_version_runtime_normalization_does_not_rewrite_document()
    {
        var root = CreateTempDirectory();
        var path = SettingsPath(root);
        await File.WriteAllTextAsync(
            path,
            "{\"schemaVersion\":1,\"application\":{},\"theme\":{\"presetId\":\"missing\"},\"font\":{\"uiFontFamily\":\" Inter \",\"monospaceFontFamily\":null}}");
        var originalWriteTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, originalWriteTime);

        var settings = await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeSettings.Default, settings.Theme);
        Assert.Equal(new FontSettings("Inter", string.Empty), settings.Font);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(path));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":-1,\"application\":{}}")]
    [InlineData("{\"schemaVersion\":1.5,\"application\":{}}")]
    [InlineData("{\"unrecognized\":true}")]
    public async Task Invalid_version_metadata_preserves_active_file(string content)
    {
        var root = CreateTempDirectory();
        var path = SettingsPath(root);
        await File.WriteAllTextAsync(path, content);

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(path, exception.SettingsPath);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(exception.BackupPath));
    }

    [Fact]
    public async Task Future_version_is_neither_loaded_nor_overwritten()
    {
        var root = CreateTempDirectory();
        var path = SettingsPath(root);
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":99,\"future\":{\"value\":true}}");
        var original = await File.ReadAllBytesAsync(path);
        var store = new ChapterToolSettingsStore(root);

        var loadException = await Assert.ThrowsAsync<UnsupportedSettingsVersionException>(
            async () => await store.LoadAsync(TestContext.Current.CancellationToken));
        var saveException = await Assert.ThrowsAsync<UnsupportedSettingsVersionException>(
            async () => await store.SaveAsync(
                new ChapterToolSettings { Application = new AppSettings(Language: "zh-CN") },
                TestContext.Current.CancellationToken));

        Assert.Equal(99, loadException.FoundVersion);
        Assert.Equal(ChapterToolSettings.CurrentSchemaVersion, saveException.SupportedVersion);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public async Task Malformed_active_document_is_preserved_and_surfaces_structured_error()
    {
        var root = CreateTempDirectory();
        var path = SettingsPath(root);
        await File.WriteAllTextAsync(path, "{");

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(path, exception.SettingsPath);
        Assert.Equal(path + ".corrupt", exception.BackupPath);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(exception.BackupPath));
    }

    [Fact]
    public async Task Independently_constructed_aggregate_stores_serialize_concurrent_updates()
    {
        var root = CreateTempDirectory();
        var appUpdater = new ChapterToolSettingsStore(root);
        var themeUpdater = new ChapterToolSettingsStore(root);
        var fontUpdater = new ChapterToolSettingsStore(root);

        await Task.WhenAll(
            appUpdater.UpdateAsync(
                current => current with { Application = current.Application with { Language = "ja-JP" } },
                TestContext.Current.CancellationToken).AsTask(),
            themeUpdater.UpdateAsync(
                current => current with { Theme = new ThemeSettings("solarized-dark") },
                TestContext.Current.CancellationToken).AsTask(),
            fontUpdater.UpdateAsync(
                current => current with { Font = new FontSettings("UI", "Mono") },
                TestContext.Current.CancellationToken).AsTask());

        var settings = await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ja-JP", settings.Application.Language);
        Assert.Equal("solarized-dark", settings.Theme.PresetId);
        Assert.Equal(new FontSettings("UI", "Mono"), settings.Font);
        Assert.Empty(Directory.EnumerateFiles(root, "settings.json.*.tmp"));
    }

    [Fact]
    public async Task Concurrent_updates_to_one_section_leave_valid_document_and_no_temp_files()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);
        var payload = new string('x', 100_000);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
            store.UpdateAsync(
                current => current with
                {
                    Application = current.Application with
                    {
                        Language = "en-US",
                        FfprobePath = $"{payload}-{index}",
                    },
                },
                TestContext.Current.CancellationToken).AsTask()));

        var saved = (await store.LoadAsync(TestContext.Current.CancellationToken)).Application;
        Assert.Equal("en-US", saved.Language);
        Assert.StartsWith(payload, saved.FfprobePath, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(root, "settings.json.*.tmp"));
    }

    [Fact]
    public async Task Legacy_theme_colors_alone_remain_ignored()
    {
        var root = CreateTempDirectory();
        var legacyPath = Path.Combine(root, "theme-colors.json");
        await File.WriteAllTextAsync(legacyPath, "{\"backChange\":\"#010203\"}");

        var settings = (await new ChapterToolSettingsStore(root).LoadAsync(TestContext.Current.CancellationToken)).Theme;

        Assert.Equal(ThemeSettings.Default, settings);
        Assert.True(File.Exists(legacyPath));
        Assert.False(File.Exists(SettingsPath(root)));
    }

    private static string SettingsPath(string root) => Path.Combine(root, ChapterToolSettingsStore.FileName);

    private static async Task<JsonDocument> ReadDocumentAsync(string root)
    {
        await using var stream = File.OpenRead(SettingsPath(root));
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
