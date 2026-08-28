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
}
