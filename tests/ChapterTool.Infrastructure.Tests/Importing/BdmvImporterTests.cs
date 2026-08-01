using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Infrastructure.Importing.Bdmv;
using System.Text.Json;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class BdmvImporterTests
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
        var importer = new BdmvImporter();
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
        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Groups);
    }

    [Fact]
    public async Task BdmvEntriesUseDisplayNames()
    {
        var discRoot = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);

        Assert.True(result.Success);
        var entry = Assert.Single(result.Groups.SelectMany(static group => group.Entries), candidate => candidate.Id == "00001.mpls");
        Assert.Matches(@"^00001\.mpls \(\d{1,2}:\d{2}:\d{2}\) 00002\.m2ts$", entry.DisplayName);
    }

    [Fact]
    public async Task MultiClipBdmvEntryDisplayNameMergesIntoBracketForm()
    {
        var discRoot = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);

        Assert.True(result.Success);
        var entry = Assert.Single(result.Groups.SelectMany(static group => group.Entries), candidate => candidate.Id == "00000.mpls");
        Assert.Matches(@"^00000\.mpls \(\d{1,2}:\d{2}:\d{2}\) \[00000\+00001\]\.m2ts$", entry.DisplayName);
    }

    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "00002" }, "00002.m2ts")]
    [InlineData(new[] { "00000", "00001" }, "[00000+00001].m2ts")]
    [InlineData(new[] { "00112", "00127", "00115" }, "[00112+00127+00115].m2ts")]
    public void ClipListDisplayMergesMultipleClipsIntoBracketGroup(string[] clips, string expected)
    {
        Assert.Equal(expected, BdmvImporter.ClipListDisplay(clips));
    }

    [Fact]
    public async Task ShortChapterBearingPlaylistsAreRetainedAsEntries()
    {
        var discRoot = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);

        Assert.True(result.Success);
        var actual = result.Groups.SelectMany(static group => group.Entries).ToArray();
        Assert.Contains(actual, static entry => entry.Id == "00000.mpls");
        Assert.Contains(actual, static entry => entry.Id == "00002.mpls");
        Assert.Contains(actual, static entry => entry.Id == "00099.mpls");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Message.Contains("Skipped short", StringComparison.Ordinal));

        var scan = Assert.Single(result.Diagnostics, static diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.BdmvScanCandidate);
        var scanArguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(scan.Arguments, exactMatch: false);
        Assert.True((int)scanArguments["retainedCount"]! > 0);
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
        var importer = new BdmvImporter();
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
        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Groups.SelectMany(static group => group.Entries), entry => entry.Id == "00020.mpls");
        var scan = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.BdmvScanCandidate);
        var arguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(scan.Arguments, exactMatch: false);
        var candidates = Assert.IsType<IReadOnlyList<object?>>(arguments["candidates"], exactMatch: false);
        var skipped = Assert.IsType<IReadOnlyList<object?>>(arguments["skipped"], exactMatch: false);
        Assert.Contains(candidates.Concat(skipped), candidate =>
            candidate is IReadOnlyDictionary<string, object?> values && Equals(values["name"], "00020.mpls"));
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
        var importer = new BdmvImporter();
        foreach (var fixture in fixtures)
        {
            var root = CoreFixtureDir(fixture);
            await using var manifestStream = File.OpenRead(Path.Combine(root, "bdmv-manifest.json"));
            var manifest = await JsonSerializer.DeserializeAsync<Manifest>(manifestStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(manifest);
            var expected = manifest.Titles.Where(static title => title.ChapterCount > 0).ToArray();
            var result = await importer.ImportAsync(new ChapterImportRequest(root), CancellationToken.None);
            Assert.True(result.Success, fixture);
            var actual = result.Groups.SelectMany(static group => group.Entries).ToArray();

            // The importer retains every structurally valid chapter-bearing playlist. The manifest
            // set is a subset of the imported entries; every manifest title must be present in order.
            var expectedIds = expected.Select(static title => title.Playlist).ToArray();
            var actualIds = actual.Select(static entry => entry.Id).ToArray();
            var positions = expectedIds.Select(id => Array.IndexOf(actualIds, id)).ToArray();
            Assert.All(positions, position => Assert.True(position >= 0, $"{fixture}: manifest title missing from imports; actual={string.Join(',', actualIds)}"));
            Assert.Equal(positions, positions.OrderBy(static position => position));
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
        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(discRoot), CancellationToken.None);
        Assert.True(result.Success);
        var entries = result.Groups.SelectMany(static group => group.Entries).ToArray();
        Assert.Equal(["00000.mpls", "00001.mpls", "00002.mpls"], entries.Select(static entry => entry.Id));
        Assert.Equal([14, 6, 2], entries.Select(static entry => entry.ChapterSet.Chapters.Count));
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
    public async Task ImportBdmvWithoutIndexFallsBackToScan()
    {
        // Fixtures now carry navigation files, so build a temp disc with no index.bdmv at all
        // (neither primary nor backup) to exercise the pure playlist-scan fallback.
        using var tempDir = new TempDirectory();
        var bdmvDir = Path.Combine(tempDir.Path, "BDMV");
        var playlistDir = Path.Combine(bdmvDir, "PLAYLIST");
        Directory.CreateDirectory(playlistDir);
        File.Copy(
            Path.Combine(CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1"), "BDMV", "PLAYLIST", "00001.mpls"),
            Path.Combine(playlistDir, "00001.mpls"));

        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Groups);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("index.bdmv not found"));
    }

    [Fact]
    public async Task ImportWithIndexLogsParsedMetadata()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
    }

    [Fact]
    public async Task ImportWithIndexUsesHdmvNavigationToDiscoverPlaylist()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan The Bride of Halloween/DISC1");
        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        var navigation = Assert.Single(result.Diagnostics, static diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.NavigationSource
            && diagnostic.Arguments?.ContainsKey("objects") == true);
        Assert.Contains("playlist references", navigation.Message, StringComparison.Ordinal);
        var navigationArguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(navigation.Arguments, exactMatch: false);
        var objects = Assert.IsType<IReadOnlyList<object?>>(navigationArguments["objects"], exactMatch: false);
        Assert.Contains(objects, item =>
            item is IReadOnlyDictionary<string, object?> values
            && values["playlists"] is IReadOnlyList<object?> playlists
            && playlists.Any(playlist => playlist is IReadOnlyDictionary<string, object?> details
                && Equals(details["playlist"], "00001.mpls")));
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("No playlists could be resolved from index.bdmv titles"));

        var loaded = Assert.Single(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
        var structure = Assert.IsType<IReadOnlyDictionary<string, object?>>(loaded.Arguments, exactMatch: false);
        var indexes = Assert.IsType<IReadOnlyDictionary<string, object?>>(structure["indexes"], exactMatch: false);
        Assert.Equal(38U, indexes["length"]);
        Assert.Equal(1, indexes["titleCount"]);
    }

    [Fact]
    public async Task ImportClpiDiagnosticsAreProduced()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        var clpi = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.ClpiFileLoaded);
        var arguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(clpi.Arguments, exactMatch: false);
        Assert.True((int)arguments["loadedCount"]! > 0);
        Assert.IsType<IReadOnlyList<object?>>(arguments["clips"], exactMatch: false);
    }

    [Fact]
    public async Task ImportWithFailedIndexFallsBackToScan()
    {
        using var tempDir = new TempDirectory();
        var bdmvDir = Path.Combine(tempDir.Path, "BDMV");
        var playlistDir = Path.Combine(bdmvDir, "PLAYLIST");
        Directory.CreateDirectory(playlistDir);

        // Create a deliberately invalid index.bdmv
        await File.WriteAllTextAsync(Path.Combine(bdmvDir, "index.bdmv"), "not a valid index");

        // Create a minimal valid mpls file
        File.Copy(
            Path.Combine(CoreFixtureDir("Detective Conan Zero the Enforcer"), "BDMV", "PLAYLIST", "00000.mpls"),
            Path.Combine(playlistDir, "00000.mpls"));

        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Failed to parse index.bdmv"));
        Assert.Contains(result.Groups.SelectMany(static group => group.Entries), static entry => entry.Id == "00000.mpls");
    }

    [Fact]
    public async Task ImportWithoutIndexDoesNotContainIndexLoadedLog()
    {
        var bdmvDir = CoreFixtureDir("KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1");
        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(bdmvDir);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Loaded index.bdmv"));
    }

    [Fact]
    public async Task ImportReadsDiscTitleFromMetadata()
    {
        var bdmvDir = CoreFixtureDir("Detective Conan Zero the Enforcer");
        var importer = new BdmvImporter();
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
        var importer = new BdmvImporter();
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
        var importer = new BdmvImporter();
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

        var importer = new BdmvImporter();
        var request = new ChapterImportRequest(tempDir.Path);
        var result = await importer.ImportAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("No BDMV playlists"));
    }

    [Fact]
    public void BdmvImporterHasCorrectId()
    {
        var importer = new BdmvImporter();
        Assert.Equal("bdmv", importer.Id);
    }

    [Fact]
    public void BdmvImporterSupportsBdmvExtension()
    {
        var importer = new BdmvImporter();
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
