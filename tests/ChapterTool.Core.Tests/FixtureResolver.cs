using ChapterTool.TestSupport;

namespace ChapterTool.Core.Tests;

public static class FixtureResolver
{
    public static string RepositoryRoot => TestRepository.Root;

    public static string ExistingSample(params string[] relativeSegments)
    {
        var path = TestRepository.Combine(relativeSegments);
        Assert.True(File.Exists(path), $"Expected fixture to exist: {path}");
        return path;
    }

    public static string Fixture(params string[] relativeSegments)
    {
        var path = TestRepository.Combine(["tests", "ChapterTool.Core.Tests", "Fixtures", .. relativeSegments]);
        Assert.True(File.Exists(path), $"Expected fixture to exist: {path}");
        return path;
    }
}
