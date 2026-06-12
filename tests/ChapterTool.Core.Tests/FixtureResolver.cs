namespace ChapterTool.Core.Tests;

public static class FixtureResolver
{
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.Avalonia.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
        }
    }

    public static string ExistingSample(params string[] relativeSegments)
    {
        var path = Path.Combine(new[] { RepositoryRoot }.Concat(relativeSegments).ToArray());
        Assert.True(File.Exists(path), $"Expected fixture to exist: {path}");
        return path;
    }

    public static string Fixture(params string[] relativeSegments)
    {
        var path = Path.Combine(new[] { RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures" }.Concat(relativeSegments).ToArray());
        Assert.True(File.Exists(path), $"Expected fixture to exist: {path}");
        return path;
    }
}
