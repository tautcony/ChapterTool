using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Importing.Bdmv;
using System.Diagnostics;
using System.Text.Json;

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
    public async Task DiscRootBdmvDirectoryAndIndexInputsProduceEquivalentEntries()
    {
        var discRoot = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var inputs = new[]
        {
            discRoot,
            Path.Combine(discRoot, "BDMV"),
            Path.Combine(discRoot, "BDMV", "index.bdmv")
        };
        var importer = new NativeBdmvImporter();
        var results = new List<ChapterImportResult>();
        foreach (var input in inputs)
        {
            results.Add(await importer.ImportAsync(new ChapterImportRequest(input), CancellationToken.None));
        }

        Assert.All(results, result => Assert.True(result.Success));
        var signatures = results.Select(result => result.Groups.SelectMany(static group => group.Entries)
            .Select(static entry => (entry.Id, entry.ChapterSet.Duration, Count: entry.ChapterSet.Chapters.Count))
            .ToArray()).ToArray();
        Assert.All(signatures, signature => Assert.Equal(signatures[0], signature));
    }

    [Fact]
    public async Task NoChapterPlaylistIsRetainedAsDiagnosticAndNotImported()
    {
        var discRoot = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var result = await new NativeBdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Groups.SelectMany(static group => group.Entries), entry => entry.Id == "00020.mpls");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("00020.mpls", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FixtureManifestTitlesMatchImportedChapterBearingEntries()
    {
        var fixtures = new[]
        {
            "Detective Conan Zero the Enforcer",
            "Detective Conan The Bride of Halloween/DISC1",
            "Detective Conan The Bride of Halloween/DISC2",
            "KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1",
            "MAYONAKA_PUNCH/MAYONAKA_PUNCH_DISC1",
            "MAYONAKA_PUNCH/MAYONAKA_PUNCH_DISC2"
        };
        var importer = new NativeBdmvImporter();
        foreach (var fixture in fixtures)
        {
            var root = CoreFixtureDir(fixture);
            await using var manifestStream = File.OpenRead(Path.Combine(root, "eac3to-manifest.json"));
            var manifest = await JsonSerializer.DeserializeAsync<Manifest>(manifestStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(manifest);
            var expected = manifest!.Titles.Where(static title => title.ChapterCount > 0).ToArray();
            var result = await importer.ImportAsync(new ChapterImportRequest(root), CancellationToken.None);
            Assert.True(result.Success, fixture);
            var actual = result.Groups.SelectMany(static group => group.Entries).ToArray();
            Assert.True(expected.Length == actual.Length, $"{fixture}: actual={string.Join(',', actual.Select(static entry => $"{entry.Id}/{entry.ChapterSet.Chapters.Count}/{entry.ChapterSet.Duration}"))}");
            var expectedIds = expected.Select(static title => title.Playlist).ToArray();
            var actualIds = actual.Select(static entry => entry.Id).ToArray();
            Assert.True(expectedIds.SequenceEqual(actualIds), $"{fixture}: expected={string.Join(',', expectedIds)}, actual={string.Join(',', actualIds)}, navigation={string.Join(" | ", result.Diagnostics.Where(static diagnostic => diagnostic.Code == ChapterDiagnosticCode.NavigationSource).Select(static diagnostic => diagnostic.Message))}");
            Assert.Equal(expected.Select(static title => title.ChapterCount), actual.Select(static entry => entry.ChapterSet.Chapters.Count));
            foreach (var title in expected)
            {
                var entry = Assert.Single(actual, candidate => candidate.Id == title.Playlist);
                var expectedDuration = TimeSpan.Parse(title.Duration);
                Assert.InRange(entry.ChapterSet.Duration, expectedDuration - TimeSpan.FromSeconds(1), expectedDuration + TimeSpan.FromSeconds(1));
                if (title.Clips is not null)
                {
                    Assert.Equal(title.Clips, entry.ReferencedMediaFiles?.Select(static file => file.DisplayName));
                }
            }
        }
    }

    [Fact]
    public async Task FullDiscPlanValuesCanBeVerifiedOptIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CHAPTERTOOL_RUN_FULL_DISC_PARITY"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var discRoot = @"D:\Downloads\[BDMV][アニメ][131213] 劇場版 STEINS;GATE 負荷領域のデジャヴ\BDISO";
        Assert.True(Directory.Exists(discRoot), $"Expected full disc: {discRoot}");
        var result = await new NativeBdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);
        Assert.True(result.Success);
        var entries = result.Groups.SelectMany(static group => group.Entries).ToArray();
        Assert.Equal(new[] { "00000.mpls", "00001.mpls", "00002.mpls" }, entries.Select(static entry => entry.Id));
        Assert.Equal(new[] { 14, 6, 2 }, entries.Select(static entry => entry.ChapterSet.Chapters.Count));
        Assert.DoesNotContain(entries, static entry => entry.Id == "00005.mpls");

        var expectedTimes = new[]
        {
            new[] { "00:00:00.000", "00:09:43.416", "00:14:10.349", "00:17:26.712", "00:27:30.858", "00:33:28.506", "00:42:00.518", "00:46:29.996", "00:51:35.342", "01:04:31.326", "01:13:59.560", "01:24:27.354", "01:26:36.691", "01:30:01.396" },
            new[] { "00:00:00.000", "00:06:02.362", "00:08:22.001", "00:13:41.554", "00:15:55.488", "00:31:50.108" },
            new[] { "00:00:00.000", "00:11:57.717" }
        };
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.Equal(expectedTimes[index].Select(TimeSpan.Parse), entries[index].ChapterSet.Chapters.Select(static chapter => chapter.StartTime));
        }
    }

    [Fact]
    public async Task Eac3toShowAllCanBeVerifiedOptIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CHAPTERTOOL_RUN_EAC3TO_PARITY"), "1", StringComparison.Ordinal))
        {
            return;
        }

        const string executable = @"C:\Tools\eac3to\eac3to.exe";
        const string source = @"D:\Downloads\[BDMV][アニメ][131213] 劇場版 STEINS;GATE 負荷領域のデジャヴ\BDISO";
        Assert.True(File.Exists(executable), $"Expected eac3to: {executable}");
        Assert.True(Directory.Exists(source), $"Expected full disc: {source}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(source);
        process.StartInfo.ArgumentList.Add("-showall");
        Assert.True(process.Start());
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, error);
        Assert.Contains("00000.mpls", output);
        Assert.Contains("00001.mpls", output);
        Assert.Contains("00005.mpls", output);
        Assert.Contains("00002.mpls", output);
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
    public async Task ImportWithIndexUsesHdmvNavigationToDiscoverPlaylist()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var importer = new NativeBdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Index title references playlist through HDMV navigation: 00001.mpls"));
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

        Assert.False(result.Success);
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

    private sealed record Manifest(IReadOnlyList<ManifestTitle> Titles);

    private sealed record ManifestTitle(string Playlist, string Duration, int ChapterCount, IReadOnlyList<string>? Clips);
}
