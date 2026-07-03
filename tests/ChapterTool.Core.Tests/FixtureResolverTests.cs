namespace ChapterTool.Core.Tests;

public sealed class FixtureResolverTests
{
    [Fact]
    public void Fixture_locates_non_ascii_cue_fixture()
    {
        var path = FixtureResolver.Fixture(
            "Importing",
            "Cue",
            "のんのんびより りぴーと オリジナルサウンドトラック.cue");

        Assert.Contains("のんのんびより", Path.GetFileName(path), StringComparison.Ordinal);
    }
}
