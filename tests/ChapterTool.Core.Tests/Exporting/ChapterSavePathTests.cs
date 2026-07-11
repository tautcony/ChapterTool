using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class ChapterSavePathTests
{
    [Fact]
    public void BuildBaseFileNameUsesSourcePathThenSourceName()
    {
        var info = new ChapterSet("title", "clip", ChapterImportFormat.Ogm, 24, TimeSpan.FromSeconds(1), []);

        Assert.Equal("movie", ChapterSavePath.BuildBaseFileName(info, Path.Combine("dir", "movie.txt")));
        Assert.Equal("clip", ChapterSavePath.BuildBaseFileName(info, sourcePath: null));
        Assert.Equal("chapters", ChapterSavePath.BuildBaseFileName(info with { SourceName = null }, sourcePath: null));
    }

    [Fact]
    public void BuildBaseFileNameAppendsClipNameForDiscFormats()
    {
        var info = new ChapterSet("title", "00001", ChapterImportFormat.Mpls, 24, TimeSpan.FromSeconds(1), []);

        Assert.Equal("00005__00001", ChapterSavePath.BuildBaseFileName(info, Path.Combine("STREAM", "00005.mpls")));
        Assert.Equal("00001", ChapterSavePath.BuildBaseFileName(info, sourcePath: null));
    }

    [Fact]
    public void AllocateUniqueFilePathSkipsExistingFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = Path.Combine(directory, "movie.txt");
            File.WriteAllText(first, "a");

            var second = ChapterSavePath.AllocateUniqueFilePath(directory, "movie", ".txt");
            Assert.Equal(Path.Combine(directory, "movie_1.txt"), second);
            File.WriteAllText(second, "b");

            var third = ChapterSavePath.AllocateUniqueFilePath(directory, "movie", "txt");
            Assert.Equal(Path.Combine(directory, "movie_2.txt"), third);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryNormalizeDirectoryRejectsEmptyAndInvalidPaths()
    {
        Assert.False(ChapterSavePath.TryNormalizeDirectory(null, out _));
        Assert.False(ChapterSavePath.TryNormalizeDirectory("  ", out _));
        Assert.True(ChapterSavePath.TryNormalizeDirectory("relative-out", out var normalized));
        Assert.Equal(Path.GetFullPath("relative-out"), normalized);
    }

    [Fact]
    public void DirectoryOfSourcePathResolvesFileAndDirectoryInputs()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "movie.txt");
        File.WriteAllText(file, "x");
        try
        {
            Assert.Equal(directory, ChapterSavePath.DirectoryOfSourcePath(file));
            Assert.Equal(directory, ChapterSavePath.DirectoryOfSourcePath(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
