using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
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

    [Fact]
    public void MplsParseLimitsValidateContainerLengthRejectsTooSmall()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.ValidateContainerLength(13, 14, 64 * 1024, "test-container"));
        Assert.Contains("test-container", ex.Message);
        Assert.Contains("13", ex.Message);
    }

    [Fact]
    public void MplsParseLimitsValidateContainerLengthRejectsTooLarge()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.ValidateContainerLength(64 * 1024 + 1, 14, 64 * 1024, "test-container"));
        Assert.Contains("test-container", ex.Message);
    }

    [Fact]
    public void MplsParseLimitsValidateCountRejectsNegativeAndOversized()
    {
        var ex1 = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.ValidateCount(-1, 100, "widget"));
        Assert.Contains("widget", ex1.Message);

        var ex2 = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.ValidateCount(101, 100, "widget"));
        Assert.Contains("widget", ex2.Message);
    }

    [Fact]
    public void MplsParseLimitsValidateCountByBudgetRejectsBudgetViolation()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.ValidateCountByBudget(100, 10, 500, "widge"));
        Assert.Contains("widge", ex.Message);

        MplsParseLimits.ValidateCountByBudget(50, 10, 500, "widge");
    }

    [Fact]
    public void MplsParseLimitsSeekToAddressAcceptsValidAddress()
    {
        using var stream = new MemoryStream(new byte[100]);
        MplsParseLimits.SeekToAddress(stream, 50, "test");
        Assert.Equal(50, stream.Position);
    }

    [Fact]
    public void MplsParseLimitsSeekToAddressRejectsAddressPastEnd()
    {
        using var stream = new MemoryStream(new byte[100]);
        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsParseLimits.SeekToAddress(stream, 101, "test-section"));
        Assert.Contains("test-section", ex.Message);
    }

    [Fact]
    public void MplsBoundedStreamCreateRejectsContainerExceedingParent()
    {
        using var inner = new MemoryStream(new byte[100]);
        inner.Position = 80;

        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsBoundedStream.Create(inner, 30, 1, 1000, "overflow"));
        Assert.Contains("overflow", ex.Message);
    }

    [Fact]
    public void MplsBoundedStreamCreateToAddressRejectsAddressPastStreamEnd()
    {
        using var inner = new MemoryStream(new byte[100]);

        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsBoundedStream.CreateToAddress(inner, 101, "past-end"));
        Assert.Contains("past-end", ex.Message);
    }

    [Fact]
    public void MplsBoundedStreamCreateToAddressRejectsEndAddressBeforePosition()
    {
        using var inner = new MemoryStream(new byte[100]);
        inner.Position = 60;

        var ex = Assert.Throws<InvalidDataException>(() =>
            MplsBoundedStream.CreateToAddress(inner, 50, "before-pos"));
        Assert.Contains("before-pos", ex.Message);
    }

    [Fact]
    public void MplsBoundedStreamReadRespectsRemainingBudget()
    {
        using var inner = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        using var bounded = MplsBoundedStream.Create(inner, 5, 1, 100, "limited");

        var buffer = new byte[10];
        var read = bounded.Read(buffer, 0, 10);
        Assert.Equal(5, read);
        Assert.Equal([1, 2, 3, 4, 5], buffer[..5]);
        Assert.Equal(5, bounded.Position);

        // Second read returns 0 (exhausted)
        Assert.Equal(0, bounded.Read(buffer, 0, 10));
    }

    [Fact]
    public void MplsBoundedStreamCompleteAdvancesInnerStreamPosition()
    {
        // Partial consumption: skips unread remainder
        using (var inner = new MemoryStream(new byte[100]))
        {
            inner.Position = 10;
            using var bounded = MplsBoundedStream.Create(inner, 20, 1, 100, "skip-remainder");
            bounded.ReadByte();
            bounded.Complete("skip-remainder");
            Assert.Equal(10 + 20, inner.Position);
        }

        // Exact consumption: nothing to skip
        using (var inner = new MemoryStream(new byte[100]))
        {
            inner.Position = 10;
            using var bounded = MplsBoundedStream.Create(inner, 10, 1, 100, "exact");
            bounded.ReadExactBytes(10);
            bounded.Complete("exact");
            Assert.Equal(20, inner.Position);
        }
    }

    [Fact]
    public void MplsBoundedStreamSeekWithBeginOrigin()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "seek-begin");

        Assert.Equal(10, bounded.Seek(10, SeekOrigin.Begin));
        Assert.Equal(10, bounded.Position);
    }

    [Fact]
    public void MplsBoundedStreamSeekWithCurrentOrigin()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "seek-current");

        bounded.Seek(10, SeekOrigin.Begin);
        Assert.Equal(25, bounded.Seek(15, SeekOrigin.Current));
    }

    [Fact]
    public void MplsBoundedStreamSeekWithEndOrigin()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "seek-end");

        Assert.Equal(50, bounded.Seek(0, SeekOrigin.End));
        Assert.Equal(30, bounded.Seek(-20, SeekOrigin.End));
    }

    [Fact]
    public void MplsBoundedStreamSeekRejectsCrossingBoundary()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "seek-boundary");

        Assert.Throws<InvalidDataException>(() => bounded.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<InvalidDataException>(() => bounded.Seek(51, SeekOrigin.Begin));
        Assert.Throws<InvalidDataException>(() => bounded.Seek(1, SeekOrigin.End));
    }

    [Fact]
    public void MplsBoundedStreamReadRejectsInvalidArguments()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "read-args");

        Assert.Throws<ArgumentNullException>(() => bounded.Read(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounded.Read(new byte[10], -1, 1));
    }

    [Fact]
    public void UOMaskTableWithAllBitsSetVerifiedViaRead()
    {
        var maskBytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        using var stream = new MemoryStream(maskBytes);
        var mask = MplsUOMaskTable.Read(stream);

        Assert.Equal(8, mask.FlagField.Length);
        Assert.True(mask.MenuCall);
        Assert.True(mask.TitleSearch);
        Assert.True(mask.ChapterSearch);
        Assert.True(mask.TimeSearch);
        Assert.True(mask.Stop);
        Assert.True(mask.PauseOn);
        Assert.True(mask.ForwardPlay);
        Assert.True(mask.BackwardPlay);
        Assert.True(mask.Resume);
        Assert.True(mask.SelectButton);
        Assert.True(mask.ActivateButton);
    }

    [Fact]
    public void UOMaskTableWithAllBitsClearedVerifiedViaRead()
    {
        var maskBytes = "\0\0\0\0\0\0\0\0"u8.ToArray();
        using var stream = new MemoryStream(maskBytes);
        var mask = MplsUOMaskTable.Read(stream);

        Assert.False(mask.MenuCall);
        Assert.False(mask.TitleSearch);
        Assert.False(mask.ChapterSearch);
        Assert.False(mask.TimeSearch);
        Assert.False(mask.SkipToNextPoint);
        Assert.False(mask.SkipToPrevPoint);
        Assert.False(mask.Stop);
        Assert.False(mask.PauseOn);
        Assert.False(mask.StillOff);
        Assert.False(mask.ForwardPlay);
        Assert.False(mask.BackwardPlay);
        Assert.False(mask.Resume);
        Assert.False(mask.MoveUpSelectedButton);
        Assert.False(mask.MoveDownSelectedButton);
        Assert.False(mask.MoveLeftSelectedButton);
        Assert.False(mask.MoveRightSelectedButton);
        Assert.False(mask.SelectButton);
        Assert.False(mask.ActivateButton);
        Assert.False(mask.SelectAndActivateButton);
        Assert.False(mask.PrimaryAudioStreamNumberChange);
        Assert.False(mask.AngleNumberChange);
        Assert.False(mask.PopupOn);
        Assert.False(mask.PopupOff);
        Assert.False(mask.PrimaryPGEnableDisable);
        Assert.False(mask.PrimaryPGStreamNumberChange);
        Assert.False(mask.SecondaryVideoEnableDisable);
        Assert.False(mask.SecondaryVideoStreamNumberChange);
        Assert.False(mask.SecondaryAudioEnableDisable);
        Assert.False(mask.SecondaryAudioStreamNumberChange);
        Assert.False(mask.SecondaryPGStreamNumberChange);
    }

    [Fact]
    public void MplsExtensionDataReadReturnsEmptyOnZeroLength()
    {
        using var builder = new MplsBinaryBuilder();
        builder.UInt32BE(0);
        using var stream = builder.Build();
        var extData = MplsExtensionData.Read(stream);

        Assert.Equal(0U, extData.Length);
        Assert.Empty(extData.ExtDataEntries);
        Assert.Empty(extData.DataBlock);
    }

    [Fact]
    public void MplsExtensionDataRejectsEntriesLengthExceedingContainer()
    {
        using var builder = new MplsBinaryBuilder();
        builder.UInt32BE(30)
            .UInt32BE(28)
            .ExtensionDataEntryTable(10);
        using var stream = builder.Build();
        Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
    }

    [Fact]
    public void MplsExtensionDataRejectsInvalidDataBlockStartAddress()
    {
        using var builder = new MplsBinaryBuilder();
        builder.UInt32BE(40)
            .UInt32BE(5)
            .ExtensionDataEntryTable(1);
        using var stream = builder.Build();
        Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
    }

    [Fact]
    public void MplsExtensionDataRejectsEntriesOverflowingContainer()
    {
        using var builder = new MplsBinaryBuilder();
        builder.UInt32BE(15)
            .UInt32BE(20)
            .ExtensionDataEntryTable(1);
        using var stream = builder.Build();
        Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
    }

    [Fact]
    public void MplsPlaylistFileReadsFixtureWithSubpaths()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00001_MPEG_II.mpls"));
        var file = MplsPlaylistFile.Read(stream);

        Assert.NotEmpty(file.PlayList.SubPaths);
        var subPath = file.PlayList.SubPaths[0];
        Assert.NotNull(subPath);
        Assert.NotEmpty(subPath.SubPlayItems);
    }

    [Fact]
    public void MplsExtensionDataUsesSectionRelativeAddressesAndParsesPipMetadata()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00020_Terminator2.mpls"));
        var file = MplsPlaylistFile.Read(stream);

        Assert.NotNull(file.ExtensionData);
        Assert.Single(file.ExtensionData.ExtDataEntries);
        Assert.Equal((ushort)1, file.ExtensionData.ExtDataEntries[0].ExtDataType);
        Assert.Equal(29, file.ExtensionData.PipMetadata.Count);
        Assert.Equal(98, file.ExtensionData.PipMetadata.Sum(static item => item.Data.Count));
        Assert.All(file.ExtensionData.PipMetadata, static item => Assert.InRange(item.TimelineType, (byte)0, (byte)15));
    }

    [Fact]
    public void MplsPlaylistFileReadsPaddingZeroFixture()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00003_Padding_Zero.mpls"));
        var file = MplsPlaylistFile.Read(stream);

        Assert.Equal(2, file.PlayList.NumberOfPlayItems);
        Assert.Equal(24000d / 1001d, MplsFrameRate(file.PlayList.PlayItems[0]));
    }

    [Fact]
    public void MplsVersion0240IsAccepted()
    {
        using var stream = new MemoryStream(MinimalMpls(version: "0240"));
        var file = MplsPlaylistFile.Read(stream);
        Assert.Equal("0240", file.VersionNumber);
    }

    [Fact]
    public void MplsImporterReadPlaylistInfoParsesFchFixture()
    {
        var path = FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00001_fch.mpls");
        var chapterSet = MplsChapterImporter.ReadPlaylistInfo(path);

        Assert.NotNull(chapterSet);
        Assert.Equal("00001", chapterSet.SourceName);
        Assert.Equal(ChapterImportFormat.Mpls, chapterSet.ImportFormat);
        Assert.Equal(24000d / 1001d, chapterSet.FramesPerSecond, 3);
        Assert.Equal(9, chapterSet.Chapters.Count);
    }

    [Fact]
    public void MplsChapterImporterReturnsEmptyForEmptyPlayItems()
    {
        using var stream = new MemoryStream(MinimalMpls(numberOfPlayItems: 0, numberOfSubPaths: 0));

        var parsed = MplsPlaylistFile.Read(stream);
        Assert.Empty(parsed.PlayList.PlayItems);
        Assert.Empty(parsed.PlayList.SubPaths);
    }

    [Fact]
    public void MplsPlaylistFileRejectsInvalidVersion()
    {
        using var builder = new MplsBinaryBuilder();
        builder.Ascii("MPLS9999").Reserved(36);
        using var stream = builder.Build();
        Assert.Throws<InvalidDataException>(() => MplsPlaylistFile.Read(stream));
    }

    [Fact]
    public void BinaryReadExtensionsSkipBytesRejectsNegative()
    {
        using var stream = new MemoryStream(new byte[10]);
        Assert.Throws<InvalidDataException>(() => stream.SkipBytes(-1));
    }

    [Fact]
    public void BinaryReadExtensionsSkipBytesThrowsOnExhaustedStream()
    {
        using var stream = new MemoryStream(new byte[10]);
        stream.Position = 10;
        Assert.Throws<EndOfStreamException>(() => stream.SkipBytes(1));
    }

    [Fact]
    public void BinaryReadExtensionsReadAsciiProducesString()
    {
        using var stream = new MemoryStream("HELLO!"u8.ToArray());
        Assert.Equal("HELLO!", stream.ReadAscii(6));
    }

    [Fact]
    public void BinaryReadExtensionsReadUInt16AndUInt32BigEndian()
    {
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05, 0x06]);
        Assert.Equal((ushort)0x0102, stream.ReadUInt16BigEndian());
        Assert.Equal(0x03040506U, stream.ReadUInt32BigEndian());
    }

    [Fact]
    public void BinaryReadExtensionsReadExactBytesThrowsEndOfStream()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        Assert.Throws<EndOfStreamException>(() => stream.ReadExactBytes(10));
    }

    [Fact]
    public void MplsStreamReadExtensionsReadByteChecked()
    {
        using var empty = new MemoryStream([]);
        Assert.Throws<EndOfStreamException>(() => empty.ReadByteChecked());

        using var data = new MemoryStream([0xAB]);
        Assert.Equal(0xAB, data.ReadByteChecked());
    }

    [Fact]
    public void MplsBoundedStreamReadExactBytesCrossingBoundaryThrows()
    {
        using var inner = new MemoryStream("ABCDEFGHIJ"u8.ToArray());
        using var bounded = MplsBoundedStream.Create(inner, 5, 1, 100, "cross");

        Assert.Equal("ABCDE"u8.ToArray(), bounded.ReadExactBytes(5));

        // Reading more should throw (Read returns 0)
        Assert.Throws<InvalidDataException>(() => bounded.ReadExactBytes(1));
    }

    [Fact]
    public void MplsBoundedStreamSeekWithInvalidOriginThrows()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "bad-origin");

        Assert.Throws<ArgumentOutOfRangeException>(() => bounded.Seek(0, (SeekOrigin)99));
    }

    [Fact]
    public void MplsBoundedStreamWriteThrowsNotSupported()
    {
        using var inner = new MemoryStream(new byte[100]);
        using var bounded = MplsBoundedStream.Create(inner, 50, 1, 100, "write-test");

        Assert.Throws<NotSupportedException>(() => bounded.Write([], 0, 0));
        Assert.Throws<NotSupportedException>(() => bounded.SetLength(10));
        Assert.False(bounded.CanWrite);
    }

    [Fact]
    public void MplsClipNameToStringReturnsClipAndCodec()
    {
        using var stream = new MemoryStream("00002M2TS"u8.ToArray());
        var clipName = MplsClipName.Read(stream);

        Assert.Equal("00002", clipName.ClipInformationFileName);
        Assert.Equal("M2TS", clipName.ClipCodecIdentifier);
        Assert.Equal("00002.M2TS", clipName.ToString());
    }

    [Fact]
    public void MplsContainerReadExtensionsSkipContainerRemainder()
    {
        // Partial consumption: skips the unconsumed bytes
        using (var stream = new MemoryStream(new byte[100]))
        {
            stream.Write(new byte[10]);
            var startPos = stream.Position = 10;
            stream.Position = startPos + 20;
            stream.SkipContainerRemainder(startPos, 50, "test-section");
            Assert.Equal(startPos + 50, stream.Position);
        }

        // Exact consumption: no-op
        using (var stream = new MemoryStream(new byte[100]))
        {
            var startPos = stream.Position = 10;
            stream.Position = startPos + 30;
            stream.SkipContainerRemainder(startPos, 30, "test-section");
            Assert.Equal(startPos + 30, stream.Position);
        }
    }

    [Fact]
    public void MplsContainerReadExtensionsSkipContainerRemainderThrowsOnOverConsumed()
    {
        using var stream = new MemoryStream(new byte[100]);
        var startPos = stream.Position = 10;

        // Consumed more than the container length
        stream.Position = startPos + 40;
        Assert.Throws<InvalidDataException>(() =>
            stream.SkipContainerRemainder(startPos, 30, "over-test"));
    }

    [Fact]
    public void MplsSubPlayItemReadsMultiClipEntries()
    {
        const int subPlayItemContainerLength = 50;
        const int subPathContainerLength = 58;

        using var builder = new MplsBinaryBuilder();
        builder
            .UInt32BE(subPathContainerLength)
            .Byte(0)
            .Byte(2)
            .UInt16BE(0)
            .Byte(0)
            .Byte(1)
            .UInt16BE(subPlayItemContainerLength)
            .ClipName("00001", "M2TS")
            .Reserved(3)
            .Byte(0x01)
            .Byte(0x00)
            .UInt32BE(0)
            .UInt32BE(0)
            .UInt16BE(0)
            .UInt32BE(0)
            .Byte(2)
            .Byte(0)
            .ClipName("00002", "M2TS")
            .Byte(0x01)
            .ClipName("00003", "M2TS")
            .Byte(0x02);

        using var stream = builder.Build();
        var subPath = MplsSubPath.Read(stream);

        Assert.Equal(subPathContainerLength, (int)subPath.Length);
        Assert.Equal(2, subPath.SubPathType);
        Assert.Single(subPath.SubPlayItems);

        var item = subPath.SubPlayItems[0];
        Assert.True(item.IsMultiClipEntries);
        Assert.Equal("00001", item.ClipName.ClipInformationFileName);
        Assert.Equal(2, item.MultiClipEntries.Count);
        Assert.Equal("00002", item.MultiClipEntries[0].ClipName.ClipInformationFileName);
        Assert.Equal(0x01, item.MultiClipEntries[0].RefToSTCID);
        Assert.Equal("00003", item.MultiClipEntries[1].ClipName.ClipInformationFileName);
        Assert.Equal(0x02, item.MultiClipEntries[1].RefToSTCID);
        Assert.Equal(0x00, item.ConnectionCondition);
    }

    [Fact]
    public void MplsPlayListMarkRejectsLengthCannotContainDeclaredMarks()
    {
        using var builder = new MplsBinaryBuilder();
        builder.UInt32BE(14)
            .UInt16BE(2);
        using var stream = builder.Build();
        Assert.Throws<InvalidDataException>(() => MplsPlayListMark.Read(stream));
    }

    private static TimeSpan[] MplsTimes(params uint[] ptsOffsets) =>
        ptsOffsets.Select(MplsChapterImporter.PtsToTime).ToArray();

    private static byte ToBcd(int value) =>
        (byte)(((value / 10) << 4) | (value % 10));

    private static byte[] MinimalMpls(
        string version = "0200",
        uint playlistLength = 6,
        ushort numberOfPlayItems = 0,
        ushort numberOfSubPaths = 0,
        uint? extensionLength = null)
    {
        const uint playlistAddress = 64;
        const uint playlistMarkAddress = 80;
        const uint extensionAddress = 96;
        using var b = new MplsBinaryBuilder();
        b.Ascii("MPLS" + version)
            .UInt32BE(playlistAddress)
            .UInt32BE(playlistMarkAddress)
            .UInt32BE(extensionLength is null ? 0 : extensionAddress)
            .Reserved(20)
            .UInt32BE(14).Reserved(14)
            .SeekTo((int)playlistAddress)
            .UInt32BE(playlistLength)
            .Reserved(2)
            .UInt16BE(numberOfPlayItems)
            .UInt16BE(numberOfSubPaths)
            .SeekTo((int)playlistMarkAddress)
            .UInt32BE(2)
            .UInt16BE(0);

        if (extensionLength is not null)
        {
            b.SeekTo((int)extensionAddress)
                .UInt32BE(extensionLength.Value);
        }

        return b.ToArray();
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
