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
    public void RuntimeImporterRegistry_resolves_a_dvd_disc_root_directory()
    {
        var discRoot = Path.Combine(Path.GetTempPath(), "ChapterTool_DvdRegistry_" + Guid.NewGuid().ToString("N"));
        var videoTs = Path.Combine(discRoot, "VIDEO_TS");
        Directory.CreateDirectory(videoTs);
        File.WriteAllBytes(Path.Combine(videoTs, "VIDEO_TS.BUP"), [0]);

        try
        {
            var registry = new RuntimeChapterImporterRegistry(
                new Core.Transform.ChapterTimeFormatter(),
                null!,
                null!,
                null!,
                null!);

            Assert.Equal("dvd-ifo", registry.Resolve(discRoot)?.Id);
            Assert.Equal("dvd-ifo", registry.Resolve(videoTs)?.Id);
            Assert.Equal("dvd-ifo", registry.Resolve(Path.Combine(videoTs, "VIDEO_TS.BUP"))?.Id);
        }
        finally
        {
            Directory.Delete(discRoot, recursive: true);
        }
    }
}
