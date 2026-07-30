using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Importing.Bdmv;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class NativeBdmvImporterTests
{
    private static string CoreFixtureDir(params string[] segments)
    {
        var baseDir = Path.Combine(FixtureResolver.RepositoryRoot, "tests", "ChapterTool.Core.Tests", "Fixtures");
        var path = Path.Combine(new[] { baseDir, "Importing", "Disc", "Bdmv" }.Concat(segments).ToArray());
        Assert.True(Directory.Exists(path), $"Expected fixture directory: {path}");
        return path;
    }

    [Fact]
    public async Task ImportBdmvDirectoryWithIndexSucceeds()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Groups);

        var entries = result.Groups.SelectMany(static g => g.Entries).ToList();
        Assert.NotEmpty(entries);
    }

    [Fact]
    public async Task ImportBdmvDirectoryDisc2Succeeds()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC2");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Groups);
    }

    [Fact]
    public async Task ImportBdmvWithoutIndexFallsBackToScan()
    {
        var bdmvDir = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Groups);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("index.bdmv not found"));
    }

    [Fact]
    public async Task ImportWithIndexLogsParsedMetadata()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
    }

    [Fact]
    public async Task ImportWithIndexUsesHdmvTitleReferenceAsPlaylistFile()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Index title references playlist: 00002.mpls"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("No playlists could be resolved from index.bdmv titles"));

        var loaded = Assert.Single(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
        var structure = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(loaded.Arguments);
        var indexes = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(structure["indexes"]);
        Assert.Equal(38U, indexes["length"]);
        Assert.Equal(1, indexes["titleCount"]);
    }

    [Fact]
    public async Task ImportClpiDiagnosticsAreProduced()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("CLPI for"));
    }

    [Fact]
    public async Task ImportWithFailedIndexLogsError()
    {
        using var tempDir = new TempDirectory();
        var bdmvDir = Path.Combine(tempDir.Path, "BDMV");
        var playlistDir = Path.Combine(bdmvDir, "PLAYLIST");
        Directory.CreateDirectory(playlistDir);

        // Create a deliberately invalid index.bdmv
        File.WriteAllText(Path.Combine(bdmvDir, "index.bdmv"), "not a valid index");

        // Create a minimal valid mpls file
        File.Copy(
            Path.Combine(CoreFixtureDir("Detective Conan Zero the Enforcer"), "BDMV", "PLAYLIST", "00000.mpls"),
            Path.Combine(playlistDir, "00000.mpls"));

        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Failed to parse index.bdmv"));
    }

    [Fact]
    public async Task ImportWithoutIndexDoesNotContainIndexLoadedLog()
    {
        var bdmvDir = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
    }

    [Fact]
    public async Task ImportReadsDiscTitleFromMetadata()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        var allEntries = result.Groups.SelectMany(static g => g.Entries).ToList();
        var japaneseTitles = allEntries.Where(e => e.ChapterSet.Title.Contains("名探偵"));
        Assert.NotEmpty(japaneseTitles);
    }

    [Fact]
    public async Task ImportWithDiscTitleFromMetadata()
    {
        var bdmvDir = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        var allEntries = result.Groups.SelectMany(static g => g.Entries).ToList();
        Assert.NotEmpty(allEntries);

        // Disc title should be non-empty (either Japanese or English metadata)
        Assert.All(allEntries, e => Assert.NotEmpty(e.ChapterSet.Title));
    }

    [Fact]
    public async Task ImportRejectsMissingPlaylistDirectory()
    {
        using var tempDir = new TempDirectory();
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("PLAYLIST"));
    }

    [Fact]
    public async Task ImportDirectoryWithoutMplsFilesReturnsNoChapters()
    {
        using var tempDir = new TempDirectory();
        var bdmvDir = Path.Combine(tempDir.Path, "BDMV");
        var playlistDir = Path.Combine(bdmvDir, "PLAYLIST");
        Directory.CreateDirectory(playlistDir);

        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("No BDMV playlists"));
    }

    [Fact]
    public void NativeBdmvImporterHasCorrectId()
    {
        var importer = new NativeBdmvImporter();
        Assert.Equal("bdmv-native", importer.Id);
    }

    [Fact]
    public void NativeBdmvImporterSupportsBdmvExtension()
    {
        var importer = new NativeBdmvImporter();
        Assert.Contains("BDMV", importer.SupportedExtensions);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ChapterTool_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
