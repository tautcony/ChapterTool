using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class IfoImporterTests
{
    private const int CellPlaybackEntrySize = 0x18;

    [Theory]
    [InlineData("VTS_05_0.IFO", 0)]
    [InlineData("VTS_33_0.IFO", 0)]
    [InlineData("VmgRecovery/TheresaDisc4/VIDEO_TS.IFO", 0)]
    public async Task IfoFrameAccuracyReflectsImportedTimesRatherThanRoundedFrameText(string filename, int expectedInexactCount)
    {
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", filename)),
            TestContext.Current.CancellationToken);
        Assert.True(result.Success, Diagnostics(result));
        var service = new FrameRateService();
        var inexactCount = 0;
        foreach (var entry in Assert.Single(result.Groups).Entries)
        {
            var set = entry.ChapterSet;
            var frames = service.UpdateFrames(set, service.FindByValue((decimal)set.FramesPerSecond), round: true, tolerance: 0.01m);

            Assert.Equal(FrameAccuracy.Accurate, frames.Chapters[0].FrameAccuracy);
            inexactCount += frames.Chapters.Count(chapter => chapter.FrameAccuracy == FrameAccuracy.Inexact);
            Assert.Equal(set.Chapters.Select(chapter => chapter.StartTime), frames.Chapters.Select(chapter => chapter.StartTime));
        }

        Assert.Equal(expectedInexactCount, inexactCount);
    }

    [Fact]
    public async Task Vts05SampleUsesPttTitleChapterMapping()
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).First();
        Assert.Equal(ChapterImportFormat.DvdIfo, info.ImportFormat);
        Assert.Equal("VTS_05_1", info.SourceName);
        Assert.Equal(7, info.Chapters.Count);
        AssertNtscFrameStarts(info.Chapters.Select(static chapter => chapter.StartTime),
            [0, 31875, 67043, 101525, 130701, 166388, 196384]);
        Assert.Equal(
            $"VTS_05_0.IFO, VTS_05_1, {info.Duration.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)}",
            result.Groups.Single().Entries[0].ImportDisplayName);
    }

    [Fact]
    public async Task Vts33SampleImportsOnlyPttReferencedTitle()
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_33_0.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var infos = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).ToArray();
        var info = Assert.Single(infos);
        Assert.Equal("VTS_33_1", info.SourceName);
        Assert.Equal(47, info.Chapters.Count);
        Assert.Equal(25, info.FramesPerSecond);
        Assert.Equal(TimeSpan.FromMilliseconds(1411200), info.Duration);
        Assert.Equal(TimeSpan.Zero, info.Chapters[0].StartTime);
        AssertTimesWithin(info.Chapters.Take(46).Select(static chapter => chapter.StartTime),
            Enumerable.Range(0, 46).Select(static index => index * 30d).ToArray());
        Assert.Equal(TimeSpan.FromSeconds(1380), info.Chapters[^1].StartTime);
    }

    [Fact]
    public async Task Srcl8925VmgFixtureImportsThreeChaptersWithoutVideoFiles()
    {
        var vmgPath = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "Srcl8925", "VIDEO_TS.IFO");
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(vmgPath),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entry = Assert.Single(Assert.Single(result.Groups).Entries);
        Assert.Equal("Title 1", entry.ChapterSet.Title);
        Assert.Equal("VTS_01_1", entry.ChapterSet.SourceName);
        Assert.Equal(3, entry.ChapterSet.Chapters.Count);
        AssertNtscFrameStarts(entry.ChapterSet.Chapters.Select(static chapter => chapter.StartTime), [0, 10476, 20210]);
    }

    [Fact]
    public async Task TheresaDisc4VmgFixtureImportsAllTitlesAndChapterStartsWithoutVideoFiles()
    {
        var vmgPath = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "TheresaDisc4", "VIDEO_TS.IFO");
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(vmgPath),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = Assert.Single(result.Groups).Entries;
        Assert.Equal([19, 19, 1], entries.Select(static entry => entry.ChapterSet.Chapters.Count));
        Assert.Equal(entries[0].ChapterSet.Chapters.Select(static chapter => chapter.StartTime),
            entries[1].ChapterSet.Chapters.Select(static chapter => chapter.StartTime));
        Assert.Equal(["Title 1", "Title 2", "Title 3"], entries.Select(static entry => entry.ChapterSet.Title));
        AssertNtscFrameStarts(
            entries[0].ChapterSet.Chapters.Select(static chapter => chapter.StartTime),
            [0, 7560, 15345, 21795, 28230, 32850, 38250, 44655, 52185, 58170, 64980, 70995, 77985, 83970, 90030, 94020, 101085, 106215, 111945]);
    }

    [Fact]
    public async Task DarlingFranxxVmgFixtureMapsEachPttTitleToItsPgc()
    {
        var vmgPath = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "DarlingFranxx", "VIDEO_TS.IFO");
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(vmgPath),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entries = Assert.Single(result.Groups).Entries;
        Assert.Equal(["Title 1", "Title 2", "Title 3"], entries.Select(static entry => entry.ChapterSet.Title));
        Assert.Equal([2, 2, 2], entries.Select(static entry => entry.ChapterSet.Chapters.Count));
        AssertNtscFrameStarts(entries[0].ChapterSet.Chapters.Select(static chapter => chapter.StartTime), [0, 3120]);
        AssertNtscFrameStarts(entries[1].ChapterSet.Chapters.Select(static chapter => chapter.StartTime), [0, 2880]);
        AssertNtscFrameStarts(entries[2].ChapterSet.Chapters.Select(static chapter => chapter.StartTime), [0, 2760]);
    }

    [Fact]
    public async Task ImportAsyncLoadsWholeDvdFolderAndExposesDiscAndStreamMetadata()
    {
        var vmgPath = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "Srcl8925", "VIDEO_TS.IFO");
        var discRoot = Path.GetDirectoryName(vmgPath)!;
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(discRoot),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        Assert.Equal(discRoot, Assert.Single(result.Groups).SourcePath);
        var entry = Assert.Single(result.Groups.Single().Entries);
        Assert.StartsWith("Title 1 (", entry.DisplayName, StringComparison.Ordinal);
        Assert.Contains("[VTS_01_1]", entry.DisplayName, StringComparison.Ordinal);
        Assert.Contains(entry.MediaTracks!, static track => track is { Kind: "video", Codec: "MPEG-2", AspectRatio: "16:9" });
        Assert.Contains(entry.MediaTracks!, static track => track is { Kind: "audio", Codec: "LPCM", SampleRate: "48kHz", Channels: "stereo" }
            && track.Summary.Contains("16-bit", StringComparison.Ordinal));

        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
        var arguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(diagnostic.Arguments, exactMatch: false);
        Assert.Equal(1, arguments["titleCount"]);
        Assert.Equal(3, arguments["chapterCount"]);
        Assert.Equal(["VTS_01_0.IFO"], Assert.IsType<string[]>(arguments["discoveredTitleSets"]));
        Assert.Contains("1 title, 3 chapters, 1 title set;", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 2, true)]
    [InlineData(ushort.MaxValue, 2, true)]
    [InlineData(0, 1, false)]
    public async Task VmgOrdinalRecoveryRequiresMatchingProgramCount(ushort invalidPgc, byte chapterCount, bool success)
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-ordinal-");
        try
        {
            var fixture = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "DarlingFranxx", "VTS_01_0.IFO");
            var bytes = File.ReadAllBytes(fixture);
            WriteUInt16BigEndian(bytes, GetFirstPttEntryOffset(bytes), invalidPgc);
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_01_0.IFO"), bytes);
            var path = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(path, BuildVmgIfo((1, 1, chapterCount)));

            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.Equal(success, result.Success);
            if (success)
            {
                var set = Assert.Single(result.Groups.Single().Entries).ChapterSet;
                AssertNtscFrameStarts(set.Chapters.Select(static chapter => chapter.StartTime), [0, 3120]);
                Assert.Equal(TimeSpan.FromTicks(1_061_060_000), set.Duration);
            }
            else
            {
                Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidIfo);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ShortRealIfoWithValidPttIsAccepted()
    {
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "NULL.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var set = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        Assert.Equal(TimeSpan.Zero, Assert.Single(set.Chapters).StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(720), set.Duration);
    }

    [Fact]
    public async Task VmgRecoveryCannotReadTheFirstPttAcrossTitleBoundaries()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-ptt-boundary-");
        try
        {
            var bytes = ReadVtsSample();
            var table = (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L);
            WriteUInt16BigEndian(bytes, table, 2);
            WriteUInt32BigEndian(bytes, table + 4, 21);
            WriteUInt32BigEndian(bytes, table + 8, 16);
            WriteUInt32BigEndian(bytes, table + 12, 18);
            WriteUInt16BigEndian(bytes, table + 16, 1);
            WriteUInt16BigEndian(bytes, table + 18, 1);
            WriteUInt16BigEndian(bytes, table + 20, 1);
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.IFO"), bytes);
            var path = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(path, BuildVmgIfo((5, 1, 7)));

            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidIfo);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsyncUsesBupFilesWhenPrimaryIfoFilesAreAbsent()
    {
        var fixtureRoot = Path.GetDirectoryName(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VmgRecovery", "Srcl8925", "VIDEO_TS.IFO"))!;
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-bup-");
        try
        {
            File.Copy(Path.Combine(fixtureRoot, "VIDEO_TS.IFO"), Path.Combine(directory.FullName, "VIDEO_TS.BUP"));
            File.Copy(Path.Combine(fixtureRoot, "VTS_01_0.IFO"), Path.Combine(directory.FullName, "VTS_01_0.BUP"));

            var result = await new IfoChapterImporter().ImportAsync(
                new ChapterImportRequest(directory.FullName),
                TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            var entry = Assert.Single(result.Groups.Single().Entries);
            Assert.Equal(3, entry.ChapterSet.Chapters.Count);
            var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
            var arguments = Assert.IsType<IReadOnlyDictionary<string, object?>>(diagnostic.Arguments, exactMatch: false);
            Assert.Equal(["VTS_01_0.BUP"], Assert.IsType<string[]>(arguments["discoveredTitleSets"]));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(0x01, 'e', 'n', "eng")]
    [InlineData(0x21, 'j', 'a', "jpn")]
    [InlineData(0x00, 'e', 'n', null)]
    [InlineData(0x40, 'e', 'n', null)]
    [InlineData(0x01, '\0', '\0', null)]
    public async Task SubtitleAttributesUseLanguageTypeAndReservedByte(byte type, char first, char second, string? language)
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-subtitle-");
        try
        {
            var bytes = ReadVtsSample();
            WriteUInt16BigEndian(bytes, 0x254, 1);
            bytes.AsSpan(0x256, 6).Clear();
            bytes[0x256] = type;
            bytes[0x258] = (byte)first;
            bytes[0x259] = (byte)second;
            var path = Path.Combine(directory.FullName, "VTS_05_0.IFO");
            await File.WriteAllBytesAsync(path, bytes);

            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            var track = Assert.Single(Assert.Single(result.Groups.Single().Entries).MediaTracks!, static item => item.Kind == "subtitle");
            Assert.Equal(language, track.Language);
            if (language is not null)
            {
                Assert.Contains($"[{language}]", track.Summary, StringComparison.Ordinal);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DiscMetadataCountsTitleVobsAndExcludesMenusAndOtherFiles()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-vobs-");
        try
        {
            var path = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(path, BuildVmgIfo((5, 1, 7)));
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.IFO"), ReadVtsSample());
            foreach (var name in new[] { "VTS_05_1.VOB", "VTS_05_2.VOB", "VTS_06_9.VOB", "VTS_05_0.VOB", "VIDEO_TS.VOB", "VTS_05_10.VOB", "VTS_05_x.VOB", "VTS_05_1.BUP" })
            {
                await File.WriteAllBytesAsync(Path.Combine(directory.FullName, name), []);
            }

            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
            Assert.Equal(3, diagnostic.Arguments!["titleVobCount"]);
            Assert.Equal(true, diagnostic.Arguments["menuVobPresent"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DiscMetadataCountsEachTitleSetOnceWhenIfoAndBackupBothExist()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-title-sets-");
        try
        {
            var path = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(path, BuildVmgIfo((5, 1, 7)));
            var bytes = ReadVtsSample();
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.IFO"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.BUP"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_06_0.BUP"), bytes);

            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
            Assert.Equal(["VTS_05_0.IFO", "VTS_06_0.BUP"], Assert.IsType<string[]>(diagnostic.Arguments!["discoveredTitleSets"]));
            Assert.Contains("2 title sets", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void AssertTimesWithin(IEnumerable<TimeSpan> actual, double[] expectedSeconds)
    {
        var actualTimes = actual.ToArray();
        Assert.Equal(expectedSeconds.Length, actualTimes.Length);
        var tolerance = TimeSpan.FromMilliseconds(5);
        for (var index = 0; index < expectedSeconds.Length; index++)
        {
            var expected = TimeSpan.FromSeconds(expectedSeconds[index]);
            Assert.InRange(actualTimes[index], expected - tolerance, expected + tolerance);
        }
    }

    private static void AssertNtscFrameStarts(IEnumerable<TimeSpan> actual, long[] legacyFrames)
    {
        var times = actual.ToArray();
        Assert.Equal(legacyFrames.Length, times.Length);
        for (var index = 0; index < legacyFrames.Length; index++)
        {
            var exactTicks = legacyFrames[index] * (decimal)TimeSpan.TicksPerSecond * 1001 / 30000;
            Assert.InRange(Math.Abs(times[index].Ticks - exactTicks), 0, 0.5m);
        }
    }

    [Fact]
    public async Task ImportAsyncReadsRequestContentStream()
    {
        var importer = new IfoChapterImporter();
        var path = FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO");
        await using var content = File.OpenRead(path);

        var result = await importer.ImportAsync(
            new ChapterImportRequest(path, content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = result.Groups.Single().Entries.Select(static entry => entry.ChapterSet).First();
        Assert.Equal("VTS_05_1", info.SourceName);
        Assert.Equal(7, info.Chapters.Count);
    }

    [Theory]
    [InlineData("OUT_OF_RANGE.IFO", ChapterDiagnosticSource.Ifo, ChapterDiagnosticReason.Invalid)]
    public async Task InvalidIfoSamplesReturnExpectedDiagnostics(string fileName, ChapterDiagnosticSource source, ChapterDiagnosticReason reason)
    {
        var importer = new IfoChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", fileName)),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == new ChapterDiagnosticCode(source, reason));
    }

    [Fact]
    public void IfoOffsetsUseUnsignedSixteenBitValues()
    {
        Assert.Equal(0x8000, IfoChapterImporter.ToUInt16([0x80, 0x00]));
        Assert.Equal(0xFFFF, IfoChapterImporter.ToUInt16([0xFF, 0xFF]));
    }

    [Fact]
    public async Task ShortValidPgcIsNotSilentlyDiscarded()
    {
        var bytes = ReadVtsSample();
        var (pgcOffset, mapOffset, cellTableOffset) = GetFirstPgcOffsets(bytes);
        bytes[pgcOffset + 2] = 1;
        bytes[pgcOffset + 3] = 1;
        bytes[pgcOffset + mapOffset] = 1;
        bytes[pgcOffset + cellTableOffset + 4] = 0;
        bytes[pgcOffset + cellTableOffset + 5] = 0;
        bytes[pgcOffset + cellTableOffset + 6] = 1;
        bytes[pgcOffset + cellTableOffset + 7] = 0x40;
        SetSingleTitlePtt(bytes, (1, 1));
        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_05_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        Assert.NotEmpty(result.Groups.Single().Entries);
        Assert.All(result.Groups.Single().Entries, entry => Assert.InRange(entry.ChapterSet.Duration, TimeSpan.Zero, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task PttChaptersMapToTheirPgcPrograms()
    {
        var bytes = ReadVtsSample();
        SetSingleTitlePtt(bytes, (1, 1), (1, 2));

        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_05_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        AssertNtscFrameStarts(info.Chapters.Select(static chapter => chapter.StartTime), [0, 31875]);
    }

    [Fact]
    public async Task PttCanMapAChapterToAnotherPgc()
    {
        var bytes = File.ReadAllBytes(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_33_0.IFO"));
        SetSingleTitlePtt(bytes, (1, 1), (2, 1));

        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_33_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var info = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        Assert.Equal(2, info.Chapters.Count);
        Assert.Equal(TimeSpan.Zero, info.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(1411200), info.Chapters[1].StartTime);
    }

    [Fact]
    public async Task ImportsEachPttTitleAndNamesItByTitleNumber()
    {
        var bytes = File.ReadAllBytes(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_33_0.IFO"));
        var tableOffset = (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L);
        WriteUInt16BigEndian(bytes, tableOffset, 2);
        WriteUInt32BigEndian(bytes, tableOffset + 8, 16);
        WriteUInt32BigEndian(bytes, tableOffset + 12, 20);
        WriteUInt32BigEndian(bytes, tableOffset + 4, 23);
        WriteUInt16BigEndian(bytes, tableOffset + 16, 1);
        WriteUInt16BigEndian(bytes, tableOffset + 18, 1);
        WriteUInt16BigEndian(bytes, tableOffset + 20, 2);
        WriteUInt16BigEndian(bytes, tableOffset + 22, 1);

        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_33_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        Assert.Equal(["VTS_33_1", "VTS_33_2"], result.Groups.Single().Entries.Select(static entry => entry.ChapterSet.SourceName));
    }

    [Fact]
    public async Task AngleBlockDurationUsesTheFirstAngleCellOnce()
    {
        var original = ReadVtsSample();
        var (_, _, cellTableOffset) = GetFirstPgcOffsets(original);
        var originalDuration = await ImportDurationAsync(original);
        var pgcOriginalOffset = GetFirstPgcOffsets(original).PgcOffset;
        var originalAngleBlockDuration = Enumerable.Range(0, 3)
            .Select(index => ReadCellDuration(original, pgcOriginalOffset, cellTableOffset, index))
            .Aggregate(TimeSpan.Zero, static (sum, duration) => sum + duration);
        var changed = original.ToArray();
        var pgcOffset = GetFirstPgcOffsets(changed).PgcOffset;
        changed[pgcOffset + cellTableOffset] = 0x40;
        changed[pgcOffset + cellTableOffset + 4] = 0;
        changed[pgcOffset + cellTableOffset + 5] = 0;
        changed[pgcOffset + cellTableOffset + 6] = 2;
        var frameRateCode = (byte)(original[pgcOffset + cellTableOffset + 7] & 0xC0);
        changed[pgcOffset + cellTableOffset + 7] = frameRateCode;
        changed[pgcOffset + cellTableOffset + CellPlaybackEntrySize] = 0x80;
        changed[pgcOffset + cellTableOffset + CellPlaybackEntrySize + 4] = 0;
        changed[pgcOffset + cellTableOffset + CellPlaybackEntrySize + 5] = 0;
        changed[pgcOffset + cellTableOffset + CellPlaybackEntrySize + 6] = 30;
        changed[pgcOffset + cellTableOffset + CellPlaybackEntrySize + 7] = (byte)(frameRateCode | 0x00);
        changed[pgcOffset + cellTableOffset + 2 * CellPlaybackEntrySize] = 0xC0;
        changed[pgcOffset + cellTableOffset + 2 * CellPlaybackEntrySize + 4] = 0;
        changed[pgcOffset + cellTableOffset + 2 * CellPlaybackEntrySize + 5] = 0;
        changed[pgcOffset + cellTableOffset + 2 * CellPlaybackEntrySize + 6] = 30;
        changed[pgcOffset + cellTableOffset + 2 * CellPlaybackEntrySize + 7] = (byte)(frameRateCode | 0x00);

        var changedDuration = await ImportDurationAsync(changed);

        var replacement = IfoChapterImporter.ConvertDvdPlaybackTime(0, 0, 2, frameRateCode, out _);
        Assert.InRange((changedDuration - (originalDuration - originalAngleBlockDuration + replacement)).Ticks, -3, 3);
    }

    [Fact]
    public async Task RejectsNonVtsIfoSignature()
    {
        var bytes = ReadVtsSample();
        System.Text.Encoding.ASCII.GetBytes("DVDVIDEO-VMG").CopyTo(bytes, 0);

        await AssertInvalidIfoAsync(bytes);
    }

    [Fact]
    public async Task VmgTitleSearchTableMapsDiscTitleToVtsTitle()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-vmg-");
        try
        {
            var vtsBytes = File.ReadAllBytes(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_33_0.IFO"));
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_33_0.IFO"), vtsBytes);
            var vmgPath = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(vmgPath, BuildVmgIfo((33, 1, 1)));

            var result = await new IfoChapterImporter().ImportAsync(
                new ChapterImportRequest(vmgPath),
                TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            var entry = Assert.Single(result.Groups.Single().Entries);
            Assert.Equal("title-1", entry.Id);
            Assert.StartsWith("Title 1 (", entry.DisplayName, StringComparison.Ordinal);
            Assert.Equal("VTS_33_1", entry.ChapterSet.SourceName);
            Assert.Equal(TimeSpan.FromMilliseconds(1411200), entry.ChapterSet.Duration);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x80)]
    public async Task RandomAndShufflePgcsWithConsistentProgramCountsAreParsed(byte modeBit)
    {
        var bytes = ReadVtsSample();
        var pgcOffset = GetFirstPgcOffsets(bytes).PgcOffset;
        bytes[pgcOffset + 0xA3] = (byte)(modeBit | bytes[pgcOffset + 2]);

        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_05_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        Assert.True(Assert.Single(result.Groups.Single().Entries).ChapterSet.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task VmgTitleSearchTableRejectsMissingVtsFile()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-vmg-");
        try
        {
            var vmgPath = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(vmgPath, BuildVmgIfo((33, 1, 1)));

            var result = await new IfoChapterImporter().ImportAsync(
                new ChapterImportRequest(vmgPath),
                TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidIfo);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task VmgDeclaredChapterCountFallsBackToMatchingPgcPrograms()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-vmg-pgc-");
        try
        {
            var vtsBytes = ReadVtsSample();
            var expectedChapterCount = vtsBytes[GetFirstPgcOffsets(vtsBytes).PgcOffset + 2];
            Assert.True(expectedChapterCount > 1);
            SetSingleTitlePtt(vtsBytes, (1, 1));
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.IFO"), vtsBytes);
            var vmgPath = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(vmgPath, BuildVmgIfo((5, 1, expectedChapterCount)));

            var result = await new IfoChapterImporter().ImportAsync(
                new ChapterImportRequest(vmgPath),
                TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            Assert.Equal(expectedChapterCount, Assert.Single(result.Groups.Single().Entries).ChapterSet.Chapters.Count);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task VmgPgcFallbackRecoversWhenPttRecordHasNoProgramNumber()
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-vmg-bad-ptt-");
        try
        {
            var vtsBytes = ReadVtsSample();
            var expectedChapterCount = vtsBytes[GetFirstPgcOffsets(vtsBytes).PgcOffset + 2];
            WriteUInt16BigEndian(vtsBytes, GetFirstPttEntryOffset(vtsBytes) + 2, 0);

            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "VTS_05_0.IFO"), vtsBytes);
            var vmgPath = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            await File.WriteAllBytesAsync(vmgPath, BuildVmgIfo((5, 1, expectedChapterCount)));

            var result = await new IfoChapterImporter().ImportAsync(
                new ChapterImportRequest(vmgPath),
                TestContext.Current.CancellationToken);

            Assert.True(result.Success, Diagnostics(result));
            Assert.Equal(expectedChapterCount, Assert.Single(result.Groups.Single().Entries).ChapterSet.Chapters.Count);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("empty-pgc-table")]
    [InlineData("oversized-pgc-count")]
    [InlineData("invalid-pgc-offset")]
    [InlineData("zero-program-count")]
    [InlineData("invalid-program-map-offset")]
    [InlineData("invalid-cell-table-offset")]
    [InlineData("program-map-cell-zero")]
    [InlineData("program-map-cell-overrun")]
    [InlineData("zero-cell-count")]
    [InlineData("duplicate-program-map-cell")]
    [InlineData("invalid-cell-minute-bcd")]
    [InlineData("invalid-cell-frame-rate")]
    [InlineData("nonsequential-pgc")]
    [InlineData("invalid-ptt-table-offset")]
    [InlineData("empty-ptt-chapter-list")]
    [InlineData("partial-ptt-chapter-record")]
    [InlineData("ptt-zero-pgc")]
    [InlineData("ptt-unknown-pgc")]
    [InlineData("ptt-unknown-program")]
    public async Task InvalidStructuresFailWithIfoDiagnostic(string mutation)
    {
        var bytes = ReadVtsSample();
        var tableOffset = (int)(ReadUInt32BigEndian(bytes, 0xCC) * 2048L);
        var pgcOffset = tableOffset + (int)ReadUInt32BigEndian(bytes, tableOffset + 12);
        var mapOffset = ReadUInt16BigEndian(bytes, pgcOffset + 0xE6);
        var cellTableOffset = ReadUInt16BigEndian(bytes, pgcOffset + 0xE8);
        switch (mutation)
        {
            case "empty-pgc-table":
                WriteUInt32BigEndian(bytes, tableOffset + 4, 0);
                break;
            case "oversized-pgc-count":
                bytes[tableOffset] = 0x01;
                break;
            case "invalid-pgc-offset":
                WriteUInt32BigEndian(bytes, tableOffset + 12, uint.MaxValue);
                break;
            case "zero-program-count":
                bytes[pgcOffset + 2] = 0;
                break;
            case "invalid-program-map-offset":
                WriteUInt16BigEndian(bytes, pgcOffset + 0xE6, ushort.MaxValue);
                break;
            case "invalid-cell-table-offset":
                WriteUInt16BigEndian(bytes, pgcOffset + 0xE8, ushort.MaxValue);
                break;
            case "program-map-cell-zero":
                bytes[pgcOffset + mapOffset] = 0;
                break;
            case "program-map-cell-overrun":
                bytes[pgcOffset + mapOffset] = byte.MaxValue;
                break;
            case "zero-cell-count":
                bytes[pgcOffset + 3] = 0;
                break;
            case "duplicate-program-map-cell":
                Assert.True(bytes[pgcOffset + 2] > 1);
                bytes[pgcOffset + mapOffset + 1] = bytes[pgcOffset + mapOffset];
                break;
            case "invalid-cell-minute-bcd":
                bytes[pgcOffset + cellTableOffset + 5] = 0x6A;
                break;
            case "invalid-cell-frame-rate":
                bytes[pgcOffset + cellTableOffset + 7] &= 0x3F;
                break;
            case "nonsequential-pgc":
                bytes[pgcOffset + 0xA3] = 0x01;
                break;
            case "invalid-ptt-table-offset":
                WriteUInt32BigEndian(bytes, (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L) + 8, uint.MaxValue);
                break;
            case "empty-ptt-chapter-list":
                SetSingleTitlePtt(bytes);
                break;
            case "partial-ptt-chapter-record":
                var pttTable = (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L);
                WriteUInt32BigEndian(bytes, pttTable + 4, 16);
                break;
            case "ptt-zero-pgc":
                WriteUInt16BigEndian(bytes, GetFirstPttEntryOffset(bytes), 0);
                break;
            case "ptt-unknown-pgc":
                WriteUInt16BigEndian(bytes, GetFirstPttEntryOffset(bytes), ushort.MaxValue);
                break;
            case "ptt-unknown-program":
                WriteUInt16BigEndian(bytes, GetFirstPttEntryOffset(bytes) + 2, ushort.MaxValue);
                break;
        }

        await AssertInvalidIfoAsync(bytes);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00, 0x00)]
    [InlineData(0x00, 0x00, 0x00, 0x80)]
    [InlineData(0x00, 0x00, 0x00, 0xC0 | 0x30)]
    [InlineData(0x00, 0x00, 0x00, 0x40 | 0x25)]
    [InlineData(0x00, 0x6A, 0x00, 0x40)]
    [InlineData(0x00, 0x00, 0x6A, 0x40)]
    [InlineData(0x6A, 0x00, 0x00, 0x40)]
    [InlineData(0x00, 0x00, 0x60, 0x40)]
    public void RejectsInvalidDvdPlaybackTime(byte hour, byte minute, byte second, byte frameByte)
    {
        Assert.Throws<InvalidDataException>(() =>
            IfoChapterImporter.ConvertDvdPlaybackTime(hour, minute, second, frameByte, out _));
    }

    [Theory]
    [InlineData(0xC0 | 0x29, true)]
    [InlineData(0x40 | 0x24, false)]
    public void AcceptsLastFrameForEachSupportedRate(byte frameByte, bool expectedNtsc)
    {
        var time = IfoChapterImporter.ConvertDvdPlaybackTime(0, 0, 0, frameByte, out var isNtsc);

        Assert.Equal(expectedNtsc, isNtsc);
        Assert.True(time > TimeSpan.Zero);
    }

    [Fact]
    public async Task IfoImporterRejectsStreamOverPortableLimit()
    {
        var importer = new IfoChapterImporter();
        await using var stream = new OversizedSeekableStream();
        stream.Position = stream.Length - 1;

        var result = await importer.ImportAsync(
            new ChapterImportRequest("huge.ifo", stream),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.InputTooLarge);
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

    private sealed class OversizedSeekableStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => PortableInputPolicy.MaxBytes + 1;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => Position = offset;

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static byte[] ReadVtsSample() =>
        File.ReadAllBytes(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO"));

    private static byte[] BuildVmgIfo(params (byte TitleSetNumber, byte TitleWithinTitleSet, byte PartCount)[] titles)
    {
        var bytes = new byte[4096];
        System.Text.Encoding.ASCII.GetBytes("DVDVIDEO-VMG").CopyTo(bytes, 0);
        WriteUInt32BigEndian(bytes, 0xC4, 1);
        WriteUInt16BigEndian(bytes, 2048, (ushort)titles.Length);
        WriteUInt32BigEndian(bytes, 2052, (uint)(8 + titles.Length * 12 - 1));
        for (var index = 0; index < titles.Length; index++)
        {
            bytes[2056 + index * 12 + 1] = 1;
            WriteUInt16BigEndian(bytes, 2056 + index * 12 + 2, titles[index].PartCount);
            bytes[2056 + index * 12 + 6] = titles[index].TitleSetNumber;
            bytes[2056 + index * 12 + 7] = titles[index].TitleWithinTitleSet;
        }

        return bytes;
    }

    private static async Task AssertInvalidIfoAsync(byte[] bytes)
    {
        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_05_0.IFO", content),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidIfo);
    }

    private static (int PgcOffset, int MapOffset, int CellTableOffset) GetFirstPgcOffsets(byte[] bytes)
    {
        var tableOffset = (int)(ReadUInt32BigEndian(bytes, 0xCC) * 2048L);
        var pgcOffset = tableOffset + (int)ReadUInt32BigEndian(bytes, tableOffset + 12);
        return (pgcOffset, ReadUInt16BigEndian(bytes, pgcOffset + 0xE6), ReadUInt16BigEndian(bytes, pgcOffset + 0xE8));
    }

    private static int GetFirstPttEntryOffset(byte[] bytes)
    {
        return GetFirstPttTitleOffset(bytes);
    }

    private static void SetSingleTitlePtt(byte[] bytes, params (ushort Pgc, ushort Program)[] chapters)
    {
        var tableOffset = (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L);
        WriteUInt16BigEndian(bytes, tableOffset, 1);
        WriteUInt32BigEndian(bytes, tableOffset + 8, 12);
        WriteUInt32BigEndian(bytes, tableOffset + 4, (uint)(12 + chapters.Length * 4 - 1));
        for (var index = 0; index < chapters.Length; index++)
        {
            WriteUInt16BigEndian(bytes, tableOffset + 12 + index * 4, chapters[index].Pgc);
            WriteUInt16BigEndian(bytes, tableOffset + 14 + index * 4, chapters[index].Program);
        }
    }

    private static int GetFirstPttTitleOffset(byte[] bytes)
    {
        var tableOffset = (int)(ReadUInt32BigEndian(bytes, 0xC8) * 2048L);
        return tableOffset + (int)ReadUInt32BigEndian(bytes, tableOffset + 8);
    }

    private static TimeSpan ReadCellDuration(byte[] bytes, int pgcOffset, int cellTableOffset, int cellIndex)
    {
        var timeOffset = pgcOffset + cellTableOffset + cellIndex * CellPlaybackEntrySize + 4;
        return IfoChapterImporter.ConvertDvdPlaybackTime(bytes[timeOffset], bytes[timeOffset + 1], bytes[timeOffset + 2], bytes[timeOffset + 3], out _);
    }

    private static async Task<TimeSpan> ImportDurationAsync(byte[] bytes)
    {
        using var content = new MemoryStream(bytes);
        var result = await new IfoChapterImporter().ImportAsync(
            new ChapterImportRequest("VTS_05_0.IFO", content),
            TestContext.Current.CancellationToken);
        Assert.True(result.Success, Diagnostics(result));
        return Assert.Single(result.Groups.Single().Entries).ChapterSet.Duration;
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static ushort ReadUInt16BigEndian(byte[] bytes, int offset) =>
        (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

    private static void WriteUInt32BigEndian(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static void WriteUInt16BigEndian(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }
}
