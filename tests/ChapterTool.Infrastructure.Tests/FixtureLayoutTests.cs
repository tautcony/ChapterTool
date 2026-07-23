namespace ChapterTool.Infrastructure.Tests;

public sealed class FixtureLayoutTests
{
    [Fact]
    public void FixturesAreStoredOnlyUnderProjectFixtureRoot()
    {
        var projectRoot = Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Infrastructure.Tests");
        var misplacedFixtureDirectories = Directory
            .EnumerateDirectories(projectRoot, "Fixtures", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(projectRoot, path).Equals("Fixtures", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(misplacedFixtureDirectories);
    }
}
