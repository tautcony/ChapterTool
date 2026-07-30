using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class BdmvPathHelperTests
{
    private static string BdmvDir(params string[] segments)
    {
        var baseDir = Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures");
        var path = Path.Combine(new[] { baseDir, "Importing", "Disc", "Bdmv" }.Concat(segments).ToArray());
        Assert.True(Directory.Exists(path), $"Expected BDMV fixture directory to exist: {path}");
        return path;
    }

    private static string BdmvFilePath(params string[] segments)
    {
        var baseDir = Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures");
        var path = Path.Combine(new[] { baseDir, "Importing", "Disc", "Bdmv" }.Concat(segments).ToArray());
        Assert.True(File.Exists(path), $"Expected BDMV fixture file to exist: {path}");
        return path;
    }

    [Fact]
    public void FindBdmvRootFindsRootFromMplsPath()
    {
        var mplsPath = BdmvFilePath("Detective Conan The Bride of Halloween/DISC1", "BDMV", "PLAYLIST", "00001.mpls");
        var root = BdmvPathHelper.FindBdmvRoot(mplsPath);
        Assert.NotNull(root);
        Assert.EndsWith("Detective Conan The Bride of Halloween/DISC1", root);
    }

    [Fact]
    public void FindBdmvRootFindsRootFromClpiPath()
    {
        var clpiPath = BdmvFilePath("Detective Conan Zero the Enforcer", "BDMV", "CLIPINF", "00000.clpi");
        var root = BdmvPathHelper.FindBdmvRoot(clpiPath);
        Assert.NotNull(root);
        Assert.EndsWith("Detective Conan Zero the Enforcer", root);
    }

    [Fact]
    public void FindBdmvRootReturnsNullForPathOutsideBdmv()
    {
        var nonBdmvPath = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var root = BdmvPathHelper.FindBdmvRoot(nonBdmvPath);
        Assert.Null(root);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void FindBdmvRootReturnsNullForInvalidPath(string? path)
    {
        Assert.Null(BdmvPathHelper.FindBdmvRoot(path!));
    }

    [Fact]
    public void FindBdmvRootReturnsNullForNonExistentFile()
    {
        Assert.Null(BdmvPathHelper.FindBdmvRoot("/nonexistent/path/file.mpls"));
    }

    [Fact]
    public void GetClpiPathConstructsCorrectPath()
    {
        var clpiPath = BdmvPathHelper.GetClpiPath("/bdmv/root", "00001");
        Assert.Equal("/bdmv/root/BDMV/CLIPINF/00001.clpi", clpiPath);
    }

    [Theory]
    [InlineData(null, "00001")]
    [InlineData("", "00001")]
    [InlineData("/root", null)]
    [InlineData("/root", "")]
    public void GetClpiPathReturnsNullForInvalidInputs(string? root, string? clipName)
    {
        Assert.Null(BdmvPathHelper.GetClpiPath(root!, clipName!));
    }

    [Fact]
    public void GetIndexPathConstructsCorrectPath()
    {
        var indexPath = BdmvPathHelper.GetIndexPath("/bdmv/root");
        Assert.Equal("/bdmv/root/BDMV/index.bdmv", indexPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetIndexPathReturnsNullForInvalidRoot(string? root)
    {
        Assert.Null(BdmvPathHelper.GetIndexPath(root!));
    }

    [Fact]
    public void GetMetaXmlPathFindsXmlFile()
    {
        var bdmvRoot = BdmvDir("Detective Conan The Bride of Halloween/DISC1");
        var xmlPath = BdmvPathHelper.GetMetaXmlPath(bdmvRoot);
        Assert.NotNull(xmlPath);
        Assert.EndsWith("bdmt_jpn.xml", xmlPath);
    }

    [Fact]
    public void GetMetaXmlPathReturnsNullWhenDirectoryMissing()
    {
        Assert.Null(BdmvPathHelper.GetMetaXmlPath("/nonexistent/path"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetMetaXmlPathReturnsNullForInvalidRoot(string? root)
    {
        Assert.Null(BdmvPathHelper.GetMetaXmlPath(root!));
    }

    [Fact]
    public void DiscoverClpiFilesFindsAvailableClpiFiles()
    {
        var bdmvRoot = BdmvDir("Detective Conan The Bride of Halloween/DISC1");
        var clipNames = new[] { "00010", "00003", "99999" };
        var clpiMap = BdmvPathHelper.DiscoverClpiFiles(bdmvRoot, clipNames);

        Assert.Equal(2, clpiMap.Count);
        Assert.True(clpiMap.ContainsKey("00010"));
        Assert.True(clpiMap.ContainsKey("00003"));
        Assert.False(clpiMap.ContainsKey("99999"));
    }

    [Fact]
    public void DiscoverClpiFilesReturnsEmptyForMissingRoot()
    {
        var clpiMap = BdmvPathHelper.DiscoverClpiFiles("/nonexistent", new[] { "00000" });
        Assert.Empty(clpiMap);
    }

    [Fact]
    public void DiscoverClpiFilesReturnsEmptyForEmptyClipNames()
    {
        var bdmvRoot = BdmvDir("Detective Conan The Bride of Halloween/DISC1");
        var clpiMap = BdmvPathHelper.DiscoverClpiFiles(bdmvRoot, []);
        Assert.Empty(clpiMap);
    }

    [Fact]
    public void DiscoverClpiFilesDeduplicatesClipNames()
    {
        var bdmvRoot = BdmvDir("Detective Conan The Bride of Halloween/DISC1");
        var clipNames = new[] { "00010", "00010", "00010" };
        var clpiMap = BdmvPathHelper.DiscoverClpiFiles(bdmvRoot, clipNames);
        Assert.Single(clpiMap);
    }
}
