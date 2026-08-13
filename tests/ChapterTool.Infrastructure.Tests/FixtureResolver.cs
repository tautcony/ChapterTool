using ChapterTool.TestSupport;

namespace ChapterTool.Infrastructure.Tests;

public static class FixtureResolver
{
    public static string RepositoryRoot => TestRepository.Root;

    public static string Fixture(params string[] relativeSegments)
    {
        var path = TestRepository.Combine(["tests", "ChapterTool.Infrastructure.Tests", "Fixtures", .. relativeSegments]);
        Assert.True(File.Exists(path), $"Expected fixture to exist: {path}");
        return path;
    }

    public static string FixtureDirectory(params string[] relativeSegments)
    {
        var path = TestRepository.Combine(["tests", "ChapterTool.Infrastructure.Tests", "Fixtures", .. relativeSegments]);
        Assert.True(Directory.Exists(path), $"Expected fixture directory to exist: {path}");
        return path;
    }
}
