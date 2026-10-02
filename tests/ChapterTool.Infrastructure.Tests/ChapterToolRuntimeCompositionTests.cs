using ChapterTool.Infrastructure.Importing.Runtime;

namespace ChapterTool.Infrastructure.Tests;

public sealed class ChapterToolRuntimeCompositionTests
{
    [Fact]
    public void ResolveSettingsDirectory_returns_explicit_directory_when_provided()
    {
        var result = ChapterToolRuntimeComposition.ResolveSettingsDirectory("/explicit/settings");

        Assert.Equal("/explicit/settings", result);
    }

    [Fact]
    public async Task CreateSettingsStore_creates_a_usable_store()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool", Guid.NewGuid().ToString("N"));
        var store = ChapterToolRuntimeComposition.CreateSettingsStore(directory);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(settings);
    }

    [Fact]
    public void CreateMp4ChapterReader_returns_a_reader()
    {
        Assert.NotNull(ChapterToolRuntimeComposition.CreateMp4ChapterReader());
    }

    [Fact]
    public void RuntimeImporterRegistry_resolves_a_dvd_disc_root_directory()
    {
        var discRoot = Path.Combine(Path.GetTempPath(), "ChapterTool_DvdRegistry_" + Guid.NewGuid().ToString("N"));
        var videoTs = Path.Combine(discRoot, "VIDEO_TS");
        Directory.CreateDirectory(videoTs);
        File.WriteAllBytes(Path.Combine(videoTs, "VIDEO_TS.BUP"), [0]);

        try
        {
            var registry = new RuntimeChapterImporterRegistry(
                new ChapterTool.Core.Transform.ChapterTimeFormatter(),
                null!,
                null!,
                null!,
                null!);

            Assert.Equal("dvd-ifo", registry.Resolve(discRoot)?.Id);
            Assert.Equal("dvd-ifo", registry.Resolve(videoTs)?.Id);
        }
        finally
        {
            Directory.Delete(discRoot, recursive: true);
        }
    }
}
