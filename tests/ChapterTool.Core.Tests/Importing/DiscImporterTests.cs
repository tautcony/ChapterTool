using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class DiscImporterTests
{
    [Fact]
    public void MplsPlaylistFileReadMapsSinglePlayItemSampleToWikiAlignedFields()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls"));

        var file = MplsPlaylistFile.Read(stream);

        Assert.Equal("MPLS", file.TypeIndicator);
        Assert.Equal("0200", file.VersionNumber);
        Assert.True(file.PlayListStartAddress > 0);
        Assert.True(file.PlayListMarkStartAddress > file.PlayListStartAddress);
        Assert.True(file.AppInfoPlayList.Length > 0);
        Assert.Equal(8, file.AppInfoPlayList.UOMaskTable.FlagField.Length);
        Assert.Equal(0U, file.ExtensionDataStartAddress);
        Assert.Null(file.ExtensionData);
        Assert.Equal(1, file.PlayList.NumberOfPlayItems);
        Assert.Empty(file.PlayList.SubPaths);

        var playItem = file.PlayList.PlayItems.Single();
        Assert.Equal("00002", playItem.ClipName.ClipInformationFileName);
        Assert.Equal("M2TS", playItem.ClipName.ClipCodecIdentifier);
        Assert.False(playItem.IsMultiAngle);
        Assert.Equal(188460000U, playItem.INTime);
        Assert.Equal(474480000U, playItem.OUTTime);
        Assert.Equal(8, playItem.UOMaskTable.FlagField.Length);
        Assert.Equal(1, playItem.STNTable.NumberOfPrimaryVideoStreamEntries);
        Assert.Empty(playItem.STNTable.SubPathStreamEntries);

        var primaryVideo = playItem.STNTable.PrimaryVideoStreamEntries[0];
        Assert.Equal(0x01, primaryVideo.StreamEntry.StreamType);
        Assert.True(primaryVideo.StreamEntry.RefToStreamPID > 0);
        Assert.Equal(0x1B, primaryVideo.StreamAttributes.StreamCodingType);
        Assert.Equal((byte)2, primaryVideo.StreamAttributes.FrameRate.GetValueOrDefault());

        var firstMark = file.PlayListMark.Marks[0];
        Assert.Equal(0x01, firstMark.MarkType);
        Assert.Equal(0, firstMark.RefToPlayItemID);
        Assert.Equal(188460000U, firstMark.MarkTimeStamp);
        Assert.True(firstMark.EntryESPID > 0);
        Assert.Equal(0U, firstMark.Duration);
    }

    [Fact]
    public void MplsPlaylistFileReadMapsMultiAngleSampleToWikiAlignedFields()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00002_tanji.mpls"));

        var file = MplsPlaylistFile.Read(stream);

        Assert.Equal(9, file.PlayList.NumberOfPlayItems);
        var multiAngle = file.PlayList.PlayItems[1];
        Assert.True(multiAngle.IsMultiAngle);
        Assert.Equal("00006&00007", multiAngle.FullName);
        Assert.NotNull(multiAngle.MultiAngle);
        Assert.Equal(2, multiAngle.MultiAngle.NumberOfAngles);
        Assert.Single(multiAngle.MultiAngle.Angles);
        Assert.Equal("00007", multiAngle.MultiAngle.Angles.Single().ClipName.ClipInformationFileName);
        Assert.Equal("M2TS", multiAngle.MultiAngle.Angles.Single().ClipName.ClipCodecIdentifier);
        Assert.Equal(24000d / 1001d, MplsFrameRate(multiAngle));

        var marksByPlayItem = file.PlayListMark.Marks
            .Where(static mark => mark.MarkType == 0x01)
            .GroupBy(static mark => mark.RefToPlayItemID)
            .ToDictionary(static group => group.Key, static group => group.Select(mark => mark.MarkTimeStamp).ToArray());
        Assert.Equal([189000000U], marksByPlayItem[0]);
        Assert.False(marksByPlayItem.ContainsKey(1));
        Assert.Equal([195654375U, 216264339U], marksByPlayItem[2]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ExactBinaryReadRejectsNegativeAndOversizedLengthsBeforeAllocation(int length)
    {
        using var stream = new MemoryStream();

        Assert.Throws<InvalidDataException>(() => stream.ReadExactBytes(length));
    }

    [Fact]
    public void MplsPlaylistFileRejectsOversizedPlaylistAndSubpathCounts()
    {
        using var playItemCountStream = new MemoryStream(MinimalMpls(
            numberOfPlayItems: checked(MplsParseLimits.MaximumPlayItems + 1)));
        using var subpathCountStream = new MemoryStream(MinimalMpls(
            numberOfSubPaths: checked(MplsParseLimits.MaximumSubPaths + 1)));

        Assert.Throws<InvalidDataException>(() => MplsPlaylistFile.Read(playItemCountStream));
        Assert.Throws<InvalidDataException>(() => MplsPlaylistFile.Read(subpathCountStream));
    }

    [Fact]
    public void MplsPlaylistFileRejectsContainerLengthSmallerThanConsumedContent()
    {
        using var stream = new MemoryStream(MinimalMpls(playlistLength: 5));

        Assert.Throws<InvalidDataException>(() => MplsPlaylistFile.Read(stream));
    }

    [Fact]
    public void MplsPlaylistFileRejectsChildReadsPastPlaylistParentBoundary()
    {
        using var stream = new MemoryStream(MinimalMpls(playlistLength: 6, numberOfPlayItems: 1));

        Assert.ThrowsAny<InvalidDataException>(() => MplsPlaylistFile.Read(stream));
    }

    [Fact]
    public void MplsPlaylistFileRejectsOversizedExtensionDataBeforeAllocation()
    {
        using var stream = new MemoryStream(MinimalMpls(extensionLength: checked(MplsParseLimits.MaximumExtensionDataLength + 1)));

        Assert.Throws<InvalidDataException>(() => MplsPlaylistFile.Read(stream));
    }

    [Fact]
    public async Task MplsImporterReadsSinglePlayItemSample()
    {
        var importer = new MplsChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var entry = result.Groups.Single().Entries.Single();
        var info = entry.ChapterSet;
        Assert.Equal(ChapterImportFormat.Mpls, info.ImportFormat);
        Assert.Equal("00002", info.SourceName);
        Assert.Equal(24, info.FramesPerSecond);
        Assert.Equal(46, info.Chapters.Count);
        Assert.Equal(TimeSpan.Zero, info.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromMilliseconds(14417), info.Chapters[1].StartTime);
        Assert.Equal(MplsTimes(
            0, 648750, 984375, 23799375, 27487500, 28044375, 28276875, 28918125, 29195625, 36823125, 41679375,
            52321875, 56593125, 62563125, 73524375, 83199375, 95167500, 100741875, 106155000, 116420625,
            120845625, 126307500, 129403125, 139273125, 141071250, 142704375, 147866250, 151578750, 157603125,
            163599375, 170810625, 178768125, 186941250, 191786250, 192165000, 202076250, 213168750, 222028125,
            228003750, 236915625, 244306875, 253316250, 260053125, 271863750, 284366250, 285738750),
            info.Chapters.Select(chapter => chapter.StartTime));
        Assert.Contains(entry.ReferencedMediaFiles ?? [], reference => reference.RelativePath == Path.Combine("..", "STREAM", "00002.m2ts"));
    }

    [Fact]
    public async Task MplsImporterReadsFchSampleWithLegacyTimestamps()
    {
        var importer = new MplsChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00001_fch.mpls")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal("00001", info.SourceName);
        Assert.Equal(24000d / 1001d, info.FramesPerSecond);
        Assert.Equal(MplsChapterImporter.PtsToTime(163027149 - 90000), info.Duration);
        Assert.Equal(MplsTimes(0, 41963170, 96516418, 96831733, 98138038, 102186457, 131841081, 158573411, 162621830), info.Chapters.Select(chapter => chapter.StartTime));
    }

    [Fact]
    public async Task MplsImporterReadsMultiAngleSample()
    {
        var importer = new MplsChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00002_tanji.mpls")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var infos = result.Groups.Single().Entries.Select(entry => entry.ChapterSet).ToArray();
        Assert.Equal(9, infos.Length);
        Assert.Equal(["00005", "00006&00007", "00008", "00009&00010", "00011", "00012", "00013&00014", "00015", "00016"], infos.Select(info => info.SourceName));
        Assert.All(infos, info => Assert.Equal(24000d / 1001d, info.FramesPerSecond));
        Assert.Equal(TimeSpan.Zero, infos[1].Chapters.Single().StartTime);
        Assert.Equal(MplsTimes(0, 20609964), infos[2].Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(MplsTimes(0, 4185431, 8233850, 23263865), infos[5].Chapters.Select(chapter => chapter.StartTime));
    }

    [Fact]
    public async Task MplsImporterRejectsInvalidHeader()
    {
        var importer = new MplsChapterImporter();
        using var stream = new MemoryStream("BAD!"u8.ToArray());

        var result = await importer.ImportAsync(new ChapterImportRequest("bad.mpls", stream), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidMpls);
    }

    [Fact]
    public async Task IfoImporterReadsExistingSample()
    {
        var importer = new IfoChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Ifo", "VTS_05_0.IFO")),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var entry = result.Groups.Single().Entries.Single();
        var info = entry.ChapterSet;
        Assert.Equal(ChapterImportFormat.DvdIfo, info.ImportFormat);
        Assert.Equal("VTS_05_1", info.SourceName);
        Assert.Equal(7, info.Chapters.Count);
        Assert.Equal("Chapter 07", info.Chapters[6].Name);
        Assert.Equal("01:49:12.679", new ChapterTimeFormatter().Format(info.Chapters[6].StartTime));
        Assert.Contains(entry.ReferencedMediaFiles ?? [], reference => reference.RelativePath == "VTS_05_1.VOB");
    }

    [Fact]
    public void IfoPlaybackTimeConvertsNtscAndPal()
    {
        var ntsc = IfoChapterImporter.ConvertDvdPlaybackTime(0x00, 0x00, 0x01, 0xC0 | 0x15, out var isNtsc);
        var pal = IfoChapterImporter.ConvertDvdPlaybackTime(0x00, 0x00, 0x01, 0x40 | 0x10, out var isPalNtsc);

        Assert.True(isNtsc);
        Assert.False(isPalNtsc);
        Assert.True(ntsc > TimeSpan.FromSeconds(1.5));
        Assert.Equal(TimeSpan.FromSeconds(1.4), pal);
    }

    [Fact]
    public void IfoBcdToIntMatchesLegacyValidByteValues()
    {
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var high = value >> 4;
            var low = value & 0x0f;
            if (high <= 9 && low <= 9)
            {
                Assert.Equal(high * 10 + low, IfoChapterImporter.BcdToInt((byte)value));
            }
        }
    }

    [Fact]
    public void IfoPlaybackTimePreservesLegacyCumulativeNtscFrames()
    {
        var cells = new[]
        {
            new[] { 0, 0, 5, 0 }, new[] { 0, 0, 15, 0 }, new[] { 0, 1, 29, 28 }, new[] { 0, 0, 10, 0 },
            new[] { 0, 7, 54, 16 }, new[] { 0, 6, 40, 16 }, new[] { 0, 5, 8, 22 }, new[] { 0, 1, 19, 28 },
            new[] { 0, 0, 14, 28 }, new[] { 0, 0, 10, 2 }, new[] { 0, 0, 6, 0 }, new[] { 0, 0, 5, 0 },
            new[] { 0, 2, 44, 26 }, new[] { 0, 1, 29, 26 }, new[] { 0, 0, 10, 0 }, new[] { 0, 5, 35, 20 },
            new[] { 0, 5, 21, 20 }, new[] { 0, 6, 16, 18 }, new[] { 0, 1, 19, 28 }, new[] { 0, 0, 14, 28 },
            new[] { 0, 0, 10, 0 }, new[] { 0, 0, 6, 0 }
        };
        var expectedFrames = new[]
        {
            150, 600, 3298, 3598, 17834, 29850, 39112, 41510, 41958, 42260, 42440, 42590,
            47536, 50232, 50532, 60602, 70252, 81550, 83948, 84396, 84696, 84876
        };

        var total = TimeSpan.Zero;
        var actualFrames = new List<int>();
        foreach (var cell in cells)
        {
            total += IfoChapterImporter.ConvertDvdPlaybackTime(
                ToBcd(cell[0]),
                ToBcd(cell[1]),
                ToBcd(cell[2]),
                (byte)(0xC0 | ToBcd(cell[3])),
                out var isNtsc);
            Assert.True(isNtsc);
            actualFrames.Add((int)Math.Round(total.TotalSeconds * (30000d / 1001d)));
        }

        Assert.Equal(expectedFrames, actualFrames);
    }

    [Fact]
    public async Task IfoImporterRejectsInvalidStructure()
    {
        var importer = new IfoChapterImporter();
        using var stream = new MemoryStream("bad"u8.ToArray());
        var path = Path.GetTempFileName();
        await File.WriteAllBytesAsync(path, stream.ToArray());

        try
        {
            var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidIfo);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task XplImporterReadsSyntheticTitle()
    {
        var importer = new XplChapterImporter();
        using var stream = new MemoryStream("""
                                            <Playlist xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist">
                                              <TitleSet timeBase="60fps" tickBase="24fps">
                                                <Title id="title-id" displayName="Main" titleDuration="00:10:00:00" tickBaseDivisor="1">
                                                  <PrimaryAudioVideoClip src="ADV_OBJ/main.evo" />
                                                  <ChapterList>
                                                    <Chapter id="c1" displayName="Start" titleTimeBegin="00:00:00:00" />
                                                    <Chapter id="c2" displayName="Middle" titleTimeBegin="00:01:00:12" />
                                                  </ChapterList>
                                                </Title>
                                              </TitleSet>
                                            </Playlist>
                                            """u8.ToArray());

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.xpl", stream), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var info = result.Groups.Single().Entries.Single().ChapterSet;
        Assert.Equal(ChapterImportFormat.HdDvdXpl, info.ImportFormat);
        Assert.Equal("Main", info.Title);
        Assert.Equal("ADV_OBJ/main.evo", info.SourceName);
        Assert.Equal(TimeSpan.FromSeconds(60.5), info.Chapters[1].StartTime);
        Assert.Contains(result.Groups.Single().Entries.Single().ReferencedMediaFiles ?? [], reference => reference.RelativePath == Path.Combine("..", "HVDVD_TS", "main.evo"));
    }

    [Fact]
    public async Task XplImporterPreservesLegacyDefaultsAndNamePrecedence()
    {
        var importer = new XplChapterImporter();
        using var stream = new MemoryStream("""
                                            <Playlist xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist">
                                              <TitleSet>
                                                <Title id="title-id" displayName="Display Title" titleDuration="00:00:10:12">
                                                  <PrimaryAudioVideoClip src="ADV_OBJ/one.evo" />
                                                  <ChapterList>
                                                    <Chapter id="chapter-id" displayName="Display Chapter" titleTimeBegin="00:00:01:12" />
                                                  </ChapterList>
                                                </Title>
                                                <Title id="Second Title" titleDuration="00:00:20:00">
                                                  <ChapterList>
                                                    <Chapter id="Second Chapter" titleTimeBegin="00:00:02:00" />
                                                  </ChapterList>
                                                </Title>
                                              </TitleSet>
                                            </Playlist>
                                            """u8.ToArray());

        var result = await importer.ImportAsync(new ChapterImportRequest("movie.xpl", stream), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var infos = result.Groups.Single().Entries.Select(entry => entry.ChapterSet).ToArray();
        Assert.Equal(2, infos.Length);
        Assert.Equal("Display Title", infos[0].Title);
        Assert.Equal("Display Chapter", infos[0].Chapters.Single().Name);
        Assert.Equal(TimeSpan.FromSeconds(1.5), infos[0].Chapters.Single().StartTime);
        Assert.Equal(TimeSpan.FromSeconds(10.5), infos[0].Duration);
        Assert.Equal("Second Title", infos[1].Title);
        Assert.Equal("Second Chapter", infos[1].Chapters.Single().Name);
        Assert.Equal(TimeSpan.FromSeconds(2), infos[1].Chapters.Single().StartTime);
    }

    [Theory]
    [InlineData("<Playlist />")]
    [InlineData("<Playlist xmlns=\"http://www.dvdforum.org/2005/HDDVDVideo/Playlist\"><TitleSet><Title><ChapterList><Chapter /></ChapterList></Title></TitleSet></Playlist>")]
    [InlineData("<Playlist xmlns=\"http://www.dvdforum.org/2005/HDDVDVideo/Playlist\"><TitleSet timeBase=\"bad\"><Title titleDuration=\"00:00:01:00\"><ChapterList><Chapter titleTimeBegin=\"bad\" /></ChapterList></Title></TitleSet></Playlist>")]
    public async Task XplImporterDiagnosesMalformedXml(string xml)
    {
        var importer = new XplChapterImporter();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var result = await importer.ImportAsync(new ChapterImportRequest("bad.xpl", stream), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.XplParseFailed || diagnostic.Code == ChapterDiagnosticCode.XplNoChapters);
    }

    private static TimeSpan[] MplsTimes(params uint[] ptsOffsets) =>
        ptsOffsets.Select(MplsChapterImporter.PtsToTime).ToArray();

    private static byte ToBcd(int value) =>
        (byte)(((value / 10) << 4) | (value % 10));

    private static byte[] MinimalMpls(
        uint playlistLength = 6,
        ushort numberOfPlayItems = 0,
        ushort numberOfSubPaths = 0,
        uint? extensionLength = null)
    {
        const uint playlistAddress = 64;
        const uint playlistMarkAddress = 80;
        const uint extensionAddress = 96;
        using var stream = new MemoryStream();
        stream.Write("MPLS"u8);
        stream.Write("0200"u8);
        WriteUInt32BigEndian(stream, playlistAddress);
        WriteUInt32BigEndian(stream, playlistMarkAddress);
        WriteUInt32BigEndian(stream, extensionLength is null ? 0 : extensionAddress);
        stream.Write(new byte[20]);

        WriteUInt32BigEndian(stream, 14);
        stream.Write(new byte[14]);

        stream.Position = playlistAddress;
        WriteUInt32BigEndian(stream, playlistLength);
        WriteUInt16BigEndian(stream, 0);
        WriteUInt16BigEndian(stream, numberOfPlayItems);
        WriteUInt16BigEndian(stream, numberOfSubPaths);

        stream.Position = playlistMarkAddress;
        WriteUInt32BigEndian(stream, 2);
        WriteUInt16BigEndian(stream, 0);

        if (extensionLength is not null)
        {
            stream.Position = extensionAddress;
            WriteUInt32BigEndian(stream, extensionLength.Value);
        }

        return stream.ToArray();
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static double MplsFrameRate(MplsPlayItem playItem)
    {
        var frameRateCode = playItem.STNTable.PrimaryVideoStreamEntries[0].StreamAttributes.FrameRate;
        return frameRateCode switch
        {
            1 => 24000d / 1001d,
            2 => 24,
            3 => 25,
            4 => 30000d / 1001d,
            6 => 50,
            7 => 60000d / 1001d,
            _ => 0
        };
    }
}
