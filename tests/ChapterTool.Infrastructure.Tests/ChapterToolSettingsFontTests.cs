using System.Text.Json;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ChapterToolSettingsFontTests
{
    [Fact]
    public async Task Missing_document_returns_font_defaults_without_creating_file()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(FontSettings.Default, settings.Font);
        Assert.False(File.Exists(Path.Combine(root, ChapterToolSettingsStore.FileName)));
    }

    [Fact]
    public async Task Save_round_trips_trimmed_canonical_family_names_in_font_child_content()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);

        await store.SaveAsync(
            new ChapterToolSettings { Font = new FontSettings("  Noto Sans  ", "  JetBrains Mono ") },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new FontSettings("Noto Sans", "JetBrains Mono"),
            (await store.LoadAsync(TestContext.Current.CancellationToken)).Font);
        await using var stream = File.OpenRead(Path.Combine(root, ChapterToolSettingsStore.FileName));
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var font = json.RootElement.GetProperty("font");
        Assert.Equal("Noto Sans", font.GetProperty("uiFontFamily").GetString());
        Assert.Equal("JetBrains Mono", font.GetProperty("monospaceFontFamily").GetString());
    }

    [Fact]
    public async Task Blank_font_values_round_trip_as_stable_defaults()
    {
        var root = CreateTempDirectory();
        var store = new ChapterToolSettingsStore(root);

        await store.SaveAsync(
            new ChapterToolSettings { Font = new FontSettings(" ", "\t") },
            TestContext.Current.CancellationToken);

        Assert.Equal(FontSettings.Default, (await store.LoadAsync(TestContext.Current.CancellationToken)).Font);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
