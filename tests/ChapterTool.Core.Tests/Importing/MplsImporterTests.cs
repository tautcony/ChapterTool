using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MplsImporterTests
{
    [Theory]
    [MemberData(nameof(SampleExpectations))]
    public async Task SampleMplsFilesMatchExpectedPlaylistShape(SampleExpectation sample)
    {
        var importer = new MplsChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Mpls", sample.FileName)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var options = result.Groups.Single().Options;
        Assert.Equal(sample.ExpectedOptionCount, options.Count);
        for (var i = 0; i < sample.ExpectedOptions.Length; i++)
        {
            var expected = sample.ExpectedOptions[i];
            var actual = options[i];
            var info = actual.ChapterInfo;
            Assert.Equal("MPLS", info.SourceType);
            Assert.Equal(expected.SourceName, info.SourceName);
            Assert.Equal(expected.ChapterCount, info.Chapters.Count);
            Assert.Equal(expected.FramesPerSecond, info.FramesPerSecond, precision: 3);
            Assert.Equal(expected.Duration, info.Duration);
            Assert.Equal(expected.FirstTime, info.Chapters[0].Time);
            Assert.Equal(expected.LastTime, info.Chapters[^1].Time);
            Assert.Equal(expected.MediaReferenceCount, actual.MediaReferences?.Count ?? 0);
        }
    }

    [Fact]
    public async Task InvalidSampleMplsReturnsInvalidMplsDiagnostic()
    {
        var importer = new MplsChapterImporter();

        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00001_Invalid.mpls")),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidMpls");
    }

    [Fact]
    public async Task InvalidExtensionDataAddressReturnsInvalidMplsDiagnostic()
    {
        var importer = new MplsChapterImporter();
        using var stream = new MemoryStream(MplsWithInvalidExtensionDataAddress());

        var result = await importer.ImportAsync(new ChapterImportRequest("bad-extension.mpls", stream), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidMpls");
    }

    public static TheoryData<SampleExpectation> SampleExpectations() =>
    [
        Sample("00000_HEVC.mpls", [new("00000", 1, 0, Ms(16850), TimeSpan.Zero, TimeSpan.Zero, 1)]),
        Sample("00000_tsMuxer.mpls", [new("00000", 4, 24, Ms(209792), TimeSpan.Zero, TimeSpan.FromMinutes(3), 1)]),
        Sample("00001_fch.mpls",
        [
            new("00001", 9, 24000d / 1001d, Ms(3620826), TimeSpan.Zero, Ms(3613818), 1)
        ]),

        Sample("00001_Hidan_no_Aria_AA.mpls",
        [
            new("00002", 6, 24000d / 1001d, Ms(1422087), TimeSpan.Zero, Ms(1406030), 1),
            new("00003", 6, 24000d / 1001d, Ms(1422254), Ms(1001), Ms(1405988), 1)
        ]),

        Sample("00001_konobi.mpls",
        [
            new("00001", 14, 24000d / 1001d, Ms(2902900), TimeSpan.Zero, Ms(2871118), 1)
        ]),

        Sample("00001_LinkPoint.mpls",
        [
            new("00001", 9, 24000d / 1001d, Ms(3620826), TimeSpan.Zero, Ms(3613818), 1)
        ]),

        Sample("00001_MPEG_II.mpls",
        [
            new("00003", 24, 30000d / 1001d, Ms(5576237), TimeSpan.Zero, Ms(5560288), 1),
            new("00018", 6, 30000d / 1001d, Ms(1394059), TimeSpan.Zero, Ms(1378043), 1),
            new("00019", 13, 30000d / 1001d, Ms(2787985), TimeSpan.Zero, Ms(2786984), 1)
        ]),

        Sample("00001_tako.mpls",
        [
            new("00002", 15, 24000d / 1001d, Ms(2799630), TimeSpan.Zero, Ms(2798629), 1)
        ]),

        Sample("00001_TwoChapter.mpls",
        [
            new("00001", 13, 24000d / 1001d, Ms(1452951), TimeSpan.Zero, Ms(1361860), 1),
            new("00002", 13, 24000d / 1001d, Ms(1452785), TimeSpan.Zero, Ms(1361819), 1),
            new("00003", 1, 24000d / 1001d, Ms(8008), TimeSpan.Zero, TimeSpan.Zero, 1)
        ]),

        Sample("00002_issue1.mpls",
        [
            new("00003", 1, 24000d / 1001d, Ms(238238), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00004", 1, 24000d / 1001d, Ms(224224), Ms(1001), Ms(1001), 1),
            new("00005", 1, 24000d / 1001d, Ms(364364), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00006", 1, 24000d / 1001d, Ms(285285), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00007", 1, 24000d / 1001d, Ms(403403), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00008", 1, 24000d / 1001d, Ms(905905), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00009", 1, 24000d / 1001d, Ms(19019), TimeSpan.Zero, TimeSpan.Zero, 1)
        ]),

        Sample("00002_Menu.mpls", 166,
        [
            new("00012", 1, 24000d / 1001d, Ms(217634), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00013", 1, 24000d / 1001d, Ms(217634), TimeSpan.Zero, TimeSpan.Zero, 1)
        ]),

        Sample("00002_MultiAngle.mpls",
        [
            new("00005", 1, 24000d / 1001d, Ms(36995), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00006&00007", 1, 24000d / 1001d, Ms(111028), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00008", 2, 24000d / 1001d, Ms(937436), TimeSpan.Zero, Ms(457999), 1),
            new("00009&00010", 1, 24000d / 1001d, Ms(116450), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00011", 2, 24000d / 1001d, Ms(219177), TimeSpan.Zero, Ms(213130), 1),
            new("00012", 4, 24000d / 1001d, Ms(1289622), TimeSpan.Zero, Ms(516975), 1),
            new("00013&00014", 1, 24000d / 1001d, Ms(108483), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00015", 2, 24000d / 1001d, Ms(22814), TimeSpan.Zero, Ms(16808), 1),
            new("00016", 7, 24000d / 1001d, Ms(1421045), TimeSpan.Zero, Ms(1420044), 1)
        ]),

        Sample("00002_MultiAngle2.mpls",
        [
            new("00020", 6, 24000d / 1001d, Ms(1421086), TimeSpan.Zero, Ms(1414997), 1),
            new("00021", 4, 24000d / 1001d, Ms(1028611), TimeSpan.Zero, Ms(779988), 1),
            new("00022&00023", 1, 24000d / 1001d, Ms(344344), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00024", 2, 24000d / 1001d, Ms(48048), TimeSpan.Zero, Ms(47047), 1)
        ]),

        Sample("00002_tanji.mpls",
        [
            new("00005", 1, 24000d / 1001d, Ms(36995), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00006&00007", 1, 24000d / 1001d, Ms(111028), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00008", 2, 24000d / 1001d, Ms(937436), TimeSpan.Zero, Ms(457999), 1),
            new("00009&00010", 1, 24000d / 1001d, Ms(116450), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00011", 2, 24000d / 1001d, Ms(219177), TimeSpan.Zero, Ms(213130), 1),
            new("00012", 4, 24000d / 1001d, Ms(1289622), TimeSpan.Zero, Ms(516975), 1),
            new("00013&00014", 1, 24000d / 1001d, Ms(108483), TimeSpan.Zero, TimeSpan.Zero, 2),
            new("00015", 2, 24000d / 1001d, Ms(22814), TimeSpan.Zero, Ms(16808), 1),
            new("00016", 7, 24000d / 1001d, Ms(1421045), TimeSpan.Zero, Ms(1420044), 1)
        ]),

        Sample("00003_Padding_Zero.mpls",
        [
            new("00005", 12, 24000d / 1001d, Ms(2743574), TimeSpan.Zero, Ms(2693983), 1),
            new("00006", 2, 24000d / 1001d, Ms(286703), TimeSpan.Zero, Ms(285285), 1)
        ]),

        Sample("00011_24_Eva.mpls", [new("00002", 46, 24, Ms(6356000), TimeSpan.Zero, Ms(6349750), 1)]),
        Sample("00020_Terminator2.mpls",
        [
            new("00229", 9, 24000d / 1001d, Ms(931722), TimeSpan.Zero, Ms(818609), 1),
            new("00001", 2, 24000d / 1001d, Ms(85752), TimeSpan.Zero, Ms(60185), 1),
            new("00002", 1, 24000d / 1001d, Ms(122122), Ms(43835), Ms(43835), 1),
            new("00221", 2, 24000d / 1001d, Ms(232858), Ms(12554), Ms(209626), 1),
            new("00030", 7, 24000d / 1001d, Ms(885343), Ms(184017), Ms(737779), 1),
            new("00031", 1, 24000d / 1001d, Ms(180222), Ms(72531), Ms(72531), 1),
            new("00005", 1, 24000d / 1001d, Ms(25442), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00006", 1, 24000d / 1001d, Ms(181556), Ms(29947), Ms(29947), 1),
            new("00007", 2, 24000d / 1001d, Ms(47839), Ms(11136), Ms(40999), 1),
            new("00008", 12, 24000d / 1001d, Ms(1448906), Ms(68527), Ms(1427926), 1),
            new("00032", 1, 24000d / 1001d, Ms(260427), Ms(61895), Ms(61895), 1),
            new("00010", 3, 24000d / 1001d, Ms(105939), Ms(7716), Ms(96013), 1),
            new("00011", 1, 24000d / 1001d, Ms(111903), Ms(78078), Ms(78078), 1),
            new("00012", 1, 24000d / 1001d, Ms(95971), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00013", 2, 24000d / 1001d, Ms(180472), Ms(12512), Ms(153028), 1),
            new("00014", 1, 24000d / 1001d, Ms(160911), Ms(122122), Ms(122122), 1),
            new("00015", 1, 24000d / 1001d, Ms(141975), Ms(127753), Ms(127753), 1),
            new("00016", 3, 24000d / 1001d, Ms(497831), Ms(246037), Ms(413288), 1),
            new("00017", 1, 24000d / 1001d, Ms(84042), Ms(18560), Ms(18560), 1),
            new("00018", 4, 24000d / 1001d, Ms(739030), Ms(313855), Ms(724515), 1),
            new("00019", 2, 24000d / 1001d, Ms(88797), Ms(25150), Ms(75742), 1),
            new("00020", 10, 24000d / 1001d, Ms(1329411), Ms(115073), Ms(1124332), 1),
            new("00021", 2, 24000d / 1001d, Ms(53262), Ms(4213), Ms(43543), 1),
            new("00022", 4, 24000d / 1001d, Ms(289539), Ms(23440), Ms(244744), 1),
            new("00023", 3, 24000d / 1001d, Ms(190816), Ms(75033), Ms(143477), 1),
            new("00024", 2, 24000d / 1001d, Ms(407532), Ms(43335), Ms(176551), 1),
            new("00025", 1, 24000d / 1001d, Ms(40540), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00026", 1, 24000d / 1001d, Ms(242951), Ms(2127), Ms(2127), 1),
            new("00027", 2, 24000d / 1001d, Ms(43085), Ms(500), Ms(42834), 1)
        ]),

        Sample("00800_4Angle.mpls", 37,
        [
            new("00081", 1, 24000d / 1001d, Ms(37120), TimeSpan.Zero, TimeSpan.Zero, 1),
            new("00082&00083&00084&00085", 1, 24000d / 1001d, Ms(27527), TimeSpan.Zero, TimeSpan.Zero, 4)
        ])
    ];

    public sealed record SampleExpectation(string FileName, int ExpectedOptionCount, OptionExpectation[] ExpectedOptions)
    {
        public SampleExpectation(string fileName, OptionExpectation[] expectedOptions)
            : this(fileName, expectedOptions.Length, expectedOptions)
        {
        }
    }

    public sealed record OptionExpectation(
        string SourceName,
        int ChapterCount,
        double FramesPerSecond,
        TimeSpan Duration,
        TimeSpan FirstTime,
        TimeSpan LastTime,
        int MediaReferenceCount);

    private static SampleExpectation Sample(string fileName, OptionExpectation[] expectedOptions) =>
        new(fileName, expectedOptions);

    private static SampleExpectation Sample(string fileName, int expectedOptionCount, OptionExpectation[] expectedOptions) =>
        new(fileName, expectedOptionCount, expectedOptions);

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

    private static byte[] MplsWithInvalidExtensionDataAddress()
    {
        using var stream = new MemoryStream();
        stream.Write("MPLS"u8);
        stream.Write("0200"u8);
        WriteUInt32BigEndian(stream, 50);
        WriteUInt32BigEndian(stream, 70);
        WriteUInt32BigEndian(stream, 82);
        stream.Write(new byte[20]);

        WriteUInt32BigEndian(stream, 14);
        stream.WriteByte(0);
        stream.WriteByte(0);
        WriteUInt16BigEndian(stream, 0);
        stream.Write(new byte[8]);
        WriteUInt16BigEndian(stream, 0);

        stream.Position = 50;
        WriteUInt32BigEndian(stream, 6);
        WriteUInt16BigEndian(stream, 0);
        WriteUInt16BigEndian(stream, 0);
        WriteUInt16BigEndian(stream, 0);

        stream.Position = 70;
        WriteUInt32BigEndian(stream, 2);
        WriteUInt16BigEndian(stream, 0);

        stream.Position = 82;
        WriteUInt32BigEndian(stream, 4);
        WriteUInt32BigEndian(stream, 8);
        stream.Write(new byte[3]);
        stream.WriteByte(0);
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
}
