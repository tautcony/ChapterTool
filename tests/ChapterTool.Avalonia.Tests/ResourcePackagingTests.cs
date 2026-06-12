namespace ChapterTool.Avalonia.Tests;

public sealed class ResourcePackagingTests
{
    [Fact]
    public void RequiredAssetsExist()
    {
        var root = RepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root, "src", "ChapterTool.Avalonia", "Assets", "Icons", "app-icon.svg")));
        Assert.True(File.Exists(Path.Combine(root, "src", "ChapterTool.Avalonia", "Assets", "Images", "chapter-empty.svg")));
    }

    private static string RepositoryRoot()
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
