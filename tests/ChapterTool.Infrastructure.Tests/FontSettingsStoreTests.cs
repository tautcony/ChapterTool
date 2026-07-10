using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class FontSettingsStoreTests
{
    [Fact]
    public async Task Missing_file_returns_defaults_without_creating_file()
    {
        var root = CreateTempDirectory();
        var store = new FontSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(FontSettings.Default, settings);
        Assert.False(File.Exists(Path.Combine(root, "font-settings.json")));
    }

    [Fact]
    public async Task Save_round_trips_trimmed_canonical_family_names()
    {
        var root = CreateTempDirectory();
        var store = new FontSettingsStore(root);

        await store.SaveAsync(new FontSettings("  Noto Sans  ", "  JetBrains Mono "), TestContext.Current.CancellationToken);

        Assert.Equal(
            new FontSettings("Noto Sans", "JetBrains Mono"),
            await store.LoadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "{\n  \"uiFontFamily\": \"Noto Sans\",\n  \"monospaceFontFamily\": \"JetBrains Mono\"\n}",
            await File.ReadAllTextAsync(Path.Combine(root, "font-settings.json")));
    }

    [Fact]
    public async Task Blank_values_round_trip_as_stable_defaults()
    {
        var root = CreateTempDirectory();
        var store = new FontSettingsStore(root);

        await store.SaveAsync(new FontSettings(" ", "\t"), TestContext.Current.CancellationToken);

        Assert.Equal(FontSettings.Default, await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Load_normalizes_existing_values_without_rewriting_file()
    {
        var root = CreateTempDirectory();
        var path = Path.Combine(root, "font-settings.json");
        const string original = "{\"uiFontFamily\":\"  Noto Sans  \",\"monospaceFontFamily\":null}";
        await File.WriteAllTextAsync(path, original);
        var store = new FontSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new FontSettings("Noto Sans", ""), settings);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Malformed_file_is_preserved_and_surfaces_structured_error()
    {
        var root = CreateTempDirectory();
        var path = Path.Combine(root, "font-settings.json");
        await File.WriteAllTextAsync(path, "{");
        var store = new FontSettingsStore(root);

        var exception = await Assert.ThrowsAsync<CorruptSettingsFileException>(
            async () => await store.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(path, exception.SettingsPath);
        Assert.False(File.Exists(path));
        Assert.Equal("{", await File.ReadAllTextAsync(exception.BackupPath));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
