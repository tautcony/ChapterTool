using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class StcAwarePtsTests : IDisposable
{
    private readonly string tempBdmvDir;
    private readonly string playlistDir;
    private readonly string clpiDir;

    public StcAwarePtsTests()
    {
        tempBdmvDir = Path.Combine(Path.GetTempPath(), "ChapterTool_StcTest_" + Guid.NewGuid().ToString("N"));
        playlistDir = Path.Combine(tempBdmvDir, "BDMV", "PLAYLIST");
        clpiDir = Path.Combine(tempBdmvDir, "BDMV", "CLIPINF");
        Directory.CreateDirectory(playlistDir);
        Directory.CreateDirectory(clpiDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempBdmvDir))
            {
                Directory.Delete(tempBdmvDir, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    [Fact]
    public void ReadPlaylistInfoUsesClpiStcOffset()
    {
        var evaMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var targetMpls = Path.Combine(playlistDir, "00011.mpls");
        File.Copy(evaMpls, targetMpls);

        var clpiBytes = BuildClpiWithStcOffset(presentationStartTime: 450000U);
        File.WriteAllBytes(Path.Combine(clpiDir, "00002.clpi"), clpiBytes);

        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.NotNull(chapterSet);
        Assert.NotEmpty(chapterSet.Chapters);
        Assert.Equal(TimeSpan.Zero, chapterSet.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(6_349_750), chapterSet.Chapters[^1].StartTime);
    }

    [Fact]
    public void ReadPlaylistInfoWorksWithoutClpiFiles()
    {
        var evaMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var targetMpls = Path.Combine(playlistDir, "00011.mpls");
        File.Copy(evaMpls, targetMpls);

        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.NotNull(chapterSet);
        Assert.NotEmpty(chapterSet.Chapters);
    }

    [Fact]
    public void ReadPlaylistInfoGracefullyHandlesCorruptClpi()
    {
        var evaMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var targetMpls = Path.Combine(playlistDir, "00011.mpls");
        File.Copy(evaMpls, targetMpls);

        File.WriteAllText(Path.Combine(clpiDir, "00002.clpi"), "NOT VALID CLPI DATA");

        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.NotNull(chapterSet);
        Assert.NotEmpty(chapterSet.Chapters);
    }

    [Fact]
    public void ReadPlaylistInfoDoesNotFailWhenClpiDirMissing()
    {
        var evaMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var targetMpls = Path.Combine(playlistDir, "00011.mpls");
        File.Copy(evaMpls, targetMpls);

        Directory.Delete(clpiDir, recursive: true);

        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.NotNull(chapterSet);
        Assert.NotEmpty(chapterSet.Chapters);
    }

    [Fact]
    public void ResolveStcStartPtsReturnsZeroWithoutClpi()
    {
        var evaMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");
        var result = MplsChapterImporter.ReadPlaylistInfo(evaMpls);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Chapters);
        Assert.Equal(TimeSpan.Zero, result.Chapters[0].StartTime);
    }

    [Fact]
    public void ClpiDoesNotBreakChapterExtraction()
    {
        var absMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00001_fch.mpls");
        var targetMpls = Path.Combine(playlistDir, "00001.mpls");
        File.Copy(absMpls, targetMpls);

        var clpiBytes = BuildClpiWithStcOffset(presentationStartTime: 450000U);
        File.WriteAllBytes(Path.Combine(clpiDir, "00000.clpi"), clpiBytes);
        File.WriteAllBytes(Path.Combine(clpiDir, "00040.clpi"), clpiBytes);

        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.NotNull(chapterSet);
        Assert.NotEmpty(chapterSet.Chapters);
        Assert.True(chapterSet.Chapters.Count >= 2, "Expected at least 2 chapters with CLPI");
    }

    [Fact]
    public void ClpiOffsetDoesNotShiftMultiplePlayItemTimeline()
    {
        var sourceMpls = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00020_Terminator2.mpls");
        var targetMpls = Path.Combine(playlistDir, "00020.mpls");
        File.Copy(sourceMpls, targetMpls);

        using (var stream = File.OpenRead(sourceMpls))
        {
            var parsed = MplsPlaylistFile.Read(stream);
            foreach (var clipName in parsed.PlayList.PlayItems
                         .Select(static item => item.ClipName.ClipInformationFileName)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(Path.Combine(clpiDir, clipName + ".clpi"), BuildClpiWithStcOffset(450000));
            }
        }

        var expected = MplsChapterImporter.ReadPlaylistInfo(sourceMpls);
        var actual = MplsChapterImporter.ReadPlaylistInfo(targetMpls);

        Assert.Equal(expected.Duration, actual.Duration);
        Assert.Equal(expected.Chapters.Count, actual.Chapters.Count);
        Assert.Equal(expected.Chapters[0].StartTime, actual.Chapters[0].StartTime);
        Assert.Equal(expected.Chapters[1].StartTime, actual.Chapters[1].StartTime);
        Assert.Equal(expected.Chapters[^1].StartTime, actual.Chapters[^1].StartTime);
    }

    private static byte[] BuildClpiWithStcOffset(uint presentationStartTime)
    {
        using var builder = new ClpiBinaryBuilder();
        const int headerSize = 40;
        const int clipInfoContentSize = 144;
        const int stcSequenceContentSize = 24;

        var seqInfoAddr = checked((uint)(headerSize + 4 + clipInfoContentSize));
        var progInfoAddr = seqInfoAddr + 4 + stcSequenceContentSize;
        var cpiAddr = progInfoAddr + 6;

        // Header
        builder.Ascii("HDMV");
        builder.Ascii("0200");
        builder.UInt32BE(seqInfoAddr);
        builder.UInt32BE(progInfoAddr);
        builder.UInt32BE(cpiAddr);
        builder.UInt32BE(0);
        builder.UInt32BE(0);
        builder.Reserved(12);

        // ClipInfo
        builder.UInt32BE(clipInfoContentSize);
        builder.Reserved(2);
        builder.Byte(1);
        builder.Byte(1);
        builder.Reserved(3);
        builder.Byte(0);
        builder.UInt32BE(45000000);
        builder.UInt32BE(1000000);
        builder.Reserved(128);

        // SequenceInfo
        builder.SeekTo((int)seqInfoAddr);
        builder.UInt32BE(stcSequenceContentSize);
        builder.Byte(0);
        builder.Byte(1);
        builder.UInt32BE(0);
        builder.Byte(1);
        builder.Byte(0);
        builder.UInt16BE(0x1011);
        builder.UInt32BE(0);
        builder.UInt32BE(presentationStartTime);
        builder.UInt32BE(45000000);

        // ProgramInfo (minimal)
        builder.SeekTo((int)progInfoAddr);
        builder.UInt32BE(2);
        builder.Byte(0);
        builder.Byte(0);

        // CPI (empty)
        builder.SeekTo((int)cpiAddr);
        builder.UInt32BE(0);

        return builder.ToArray();
    }
}
