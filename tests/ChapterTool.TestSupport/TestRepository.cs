namespace ChapterTool.TestSupport;

/// <summary>
/// Locates the repository root and shared test fixtures from any testhost output directory.
/// </summary>
public static class TestRepository
{
    /// <summary>
    /// Walks up from the testhost output directory until <c>ChapterTool.slnx</c> is found.
    /// </summary>
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
        }
    }

    /// <summary>
    /// Combines path segments under the repository root.
    /// </summary>
    public static string Combine(params string[] relativeSegments) =>
        Path.Combine([Root, .. relativeSegments]);

    /// <summary>
    /// Returns a file path under the repository root. Throws when the file is missing.
    /// </summary>
    public static string RequireFile(params string[] relativeSegments)
    {
        var path = Combine(relativeSegments);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Expected fixture to exist: {path}", path);
        }

        return path;
    }

    /// <summary>
    /// Returns a directory path under the repository root. Throws when the directory is missing.
    /// </summary>
    public static string RequireDirectory(params string[] relativeSegments)
    {
        var path = Combine(relativeSegments);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Expected fixture directory to exist: {path}");
        }

        return path;
    }

    /// <summary>
    /// Resolves a file under <c>tests/ChapterTool.Core.Tests/Fixtures</c>.
    /// </summary>
    public static string CoreFixture(params string[] relativeSegments) =>
        RequireFile(["tests", "ChapterTool.Core.Tests", "Fixtures", .. relativeSegments]);

    /// <summary>
    /// Resolves a file under <c>tests/ChapterTool.Infrastructure.Tests/Fixtures</c>.
    /// </summary>
    public static string InfrastructureFixture(params string[] relativeSegments) =>
        RequireFile(["tests", "ChapterTool.Infrastructure.Tests", "Fixtures", .. relativeSegments]);

    /// <summary>
    /// Resolves a directory under <c>tests/ChapterTool.Infrastructure.Tests/Fixtures</c>.
    /// </summary>
    public static string InfrastructureFixtureDirectory(params string[] relativeSegments) =>
        RequireDirectory(["tests", "ChapterTool.Infrastructure.Tests", "Fixtures", .. relativeSegments]);
}
