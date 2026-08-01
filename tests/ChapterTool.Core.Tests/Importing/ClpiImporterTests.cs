using ChapterTool.Core.Importing.Disc.Clpi;

namespace ChapterTool.Core.Tests.Importing;

public sealed class ClpiImporterTests
{
    private const int HeaderSize = 40;

    [Fact]
    public void ValidClpiParsesClipInfo()
    {
        var bytes = BuildMinimalClpi();
        using var stream = new MemoryStream(bytes);
        var clpi = ClpiFile.Read(stream);

        Assert.Equal("HDMV", clpi.TypeIndicator);
        Assert.NotNull(clpi.ClipInfo);
        Assert.Equal((byte)1, clpi.ClipInfo.ClipStreamType);
        Assert.Equal(45000000U, clpi.ClipInfo.TSRecordingRate);
        Assert.Equal(1000000U, clpi.ClipInfo.NumberOfSourcePackets);
        Assert.False(clpi.ClipInfo.IsCC5);
        Assert.False(clpi.ClipInfo.IsAtcDelta);
    }

    [Fact]
    public void ValidClpiParsesSequenceInfo()
    {
        var bytes = BuildClpiWithSequenceInfo();
        using var stream = new MemoryStream(bytes);
        var clpi = ClpiFile.Read(stream);

        Assert.NotNull(clpi.SequenceInfo);
        Assert.Single(clpi.SequenceInfo.ATCSequences);
        var atc = clpi.SequenceInfo.ATCSequences[0];
        Assert.Single(atc.STCSequences);
        Assert.Equal((ushort)0x1011, atc.STCSequences[0].PCRPID);
        Assert.Equal(0U, atc.STCSequences[0].PresentationStartTime);
    }

    [Fact]
    public void ClpiWithCC5ParsesCorrectly()
    {
        var bytes = BuildClpiWithCC5();
        using var stream = new MemoryStream(bytes);
        var clpi = ClpiFile.Read(stream);
        Assert.True(clpi.ClipInfo.IsCC5);
    }

    [Fact]
    public void InvalidHeaderThrows()
    {
        using var stream = new MemoryStream([.. "BAD\0"u8]);
        Assert.Throws<InvalidDataException>(() => ClpiFile.Read(stream));
    }

    [Fact]
    public void TryReadReturnsNullOnError()
    {
        var result = ClpiFile.TryRead("/nonexistent/path.clpi");
        Assert.Null(result);
    }

    [Fact]
    public void TryReadWithErrorCapturesExceptionMessage()
    {
        var result = ClpiFile.TryRead("/nonexistent/path.clpi", out var error);
        Assert.Null(result);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void ClpiVersion0240IsAccepted()
    {
        using var stream = new MemoryStream(BuildMinimalClpi("0240"));
        var clpi = ClpiFile.Read(stream);
        Assert.Equal("0240", clpi.VersionNumber);
    }

    [Fact]
    public void SequenceInfoFindSTCSequenceReturnsCorrectEntry()
    {
        var bytes = BuildClpiWithTwoSTCSequences();
        using var stream = new MemoryStream(bytes);
        var clpi = ClpiFile.Read(stream);

        Assert.NotNull(clpi.SequenceInfo);
        var stc0 = clpi.SequenceInfo.FindSTCSequence(0);
        Assert.NotNull(stc0);
        Assert.Equal(0U, stc0.PresentationStartTime);

        var stc1 = clpi.SequenceInfo.FindSTCSequence(1);
        Assert.NotNull(stc1);
        Assert.Equal(450000U, stc1.PresentationStartTime);

        Assert.Null(clpi.SequenceInfo.FindSTCSequence(99));
    }

    [Fact]
    public void RealClpiParsesCpiEntryPointMap()
    {
        var path = FixtureResolver.Fixture(
            "Importing",
            "Disc",
            "Bdmv",
            "MAYONAKA_PUNCH",
            "MAYONAKA_PUNCH_DISC2",
            "BDMV",
            "CLIPINF",
            "00007.clpi");

        using var stream = File.OpenRead(path);
        var clpi = ClpiFile.Read(stream);

        Assert.NotNull(clpi.CPI);
        Assert.Single(clpi.CPI.StreamEntries);
        Assert.Single(clpi.CPI.EPMaps);
        Assert.NotEmpty(clpi.CPI.EPMaps[0].CoarseEntries);
        Assert.NotEmpty(clpi.CPI.EPMaps[0].FineEntries);
    }

    [Fact]
    public void RealClpiExposesAtcDeltaAndTsTypeMetadata()
    {
        var path = FixtureResolver.Fixture(
            "Importing",
            "Disc",
            "Bdmv",
            "MAYONAKA_PUNCH",
            "MAYONAKA_PUNCH_DISC2",
            "BDMV",
            "CLIPINF",
            "00006.clpi");

        using var stream = File.OpenRead(path);
        var clpi = ClpiFile.Read(stream);

        Assert.True(clpi.ClipInfo.IsAtcDelta);
        Assert.NotEmpty(clpi.ClipInfo.AtcDeltas);
        Assert.NotNull(clpi.ClipInfo.TsTypeInfo);
        Assert.Equal("HDMV", clpi.ClipInfo.TsTypeInfo.FormatIdentifier);
    }

    [Fact]
    public void VideoCodingType20AndIsrcAreExposed()
    {
        var payload = new byte[17];
        payload[0] = 0x20;
        payload[1] = 0x23;
        payload[2] = 0x20;
        "ISRC-TEST-01"u8.ToArray().CopyTo(payload, 5);
        using var stream = new MemoryStream([17, .. payload]);

        var codingInfo = ClpiStreamCodingInfo.Read(stream);

        Assert.Equal((byte)0x20, codingInfo.StreamCodingType);
        Assert.Equal((byte)2, codingInfo.VideoFormat);
        Assert.Equal((byte)3, codingInfo.FrameRate);
        Assert.Equal((byte)2, codingInfo.VideoAspect);
        Assert.Equal("ISRC-TEST-01"u8.ToArray(), codingInfo.Isrc);
    }

    [Fact]
    public void SubtitleClipInfoExposesFontRecords()
    {
        using var builder = new ClpiBinaryBuilder();
        builder.UInt32BE(154)
            .Reserved(2)
            .Byte(1)
            .Byte(6)
            .Reserved(3)
            .Byte(0)
            .UInt32BE(45000000)
            .UInt32BE(1000)
            .Reserved(128)
            .UInt16BE(0)
            .Byte(0)
            .Byte(1)
            .Ascii("F0001")
            .Byte(0);

        using var stream = builder.Build();
        var clipInfo = ClpiClipInfo.Read(stream);

        Assert.Single(clipInfo.Fonts);
        Assert.Equal("F0001", clipInfo.Fonts[0].FileId);
    }

    [Fact]
    public void ClpiExtensionDataUsesSectionRelativeAddresses()
    {
        using var builder = new ClpiBinaryBuilder();
        builder.UInt32BE(32)
            .UInt32BE(24)
            .Reserved(3)
            .Byte(1)
            .UInt16BE(2)
            .UInt16BE(4)
            .UInt32BE(24)
            .UInt32BE(12)
            .UInt32BE(8)
            .UInt32BE(1)
            .UInt32BE(1234);

        using var stream = builder.Build();
        var extension = ClpiExtensionData.Read(stream);

        Assert.Single(extension.ExtDataEntries);
        Assert.Equal(12, extension.DataBlock.Length);
        Assert.Equal(1234U, Assert.Single(Assert.IsType<ClpiExtentStartPoints>(extension.ExtentStartPoints).Points));
        Assert.Equal(12, extension.RawEntries["2.4"].Length);
    }

    [Fact]
    public void ClpiPacketLookupUsesStcAndNearestPrecedingEntryPoint()
    {
        var sequence = new ClpiSequenceInfo(0, [
            new ClpiATCSequence(0, 1, 0, [new ClpiSTCSequence(0x1011, 100, 0, 90_000)])
        ]);
        var cpi = new ClpiCPI(
            0,
            1,
            [new ClpiEPStreamEntry(0x1011, 1, 1, 2, 0)],
            [
                new ClpiEPMap(
                    [new ClpiEPCoarseEntry(0, 0, 0)],
                    [
                        new ClpiEPFineEntry(0, 0, 100, 100),
                        new ClpiEPFineEntry(0, 0, 200, 200)
                    ])
            ]);
        var file = new ClpiFile(
            "HDMV",
            "0200",
            0,
            0,
            0,
            0,
            0,
            new ClpiClipInfo(0, 1, 1, 0, 1_000, false, null, [], []),
            sequence,
            null,
            cpi,
            null);

        var lookup = file.LookupPacket(0, 60_000);

        Assert.NotNull(lookup);
        Assert.Equal(200U, lookup.SourcePacketNumber);
        Assert.Equal(200U << 8, lookup.EntryTimestamp);
        Assert.Equal((ushort)0x1011, lookup.StreamPID);
    }

    private static byte[] BuildMinimalClpi(string version = "0200")
    {
        using var builder = new ClpiBinaryBuilder();
        var seqInfoAddr = checked((uint)(HeaderSize + 4 + ClipContentSize));

        WriteHeader(builder, seqInfoAddr, seqInfoAddr + 6, seqInfoAddr + 12, version);
        WriteClipInfo(builder, isCC5: false);
        WriteEmptySection(builder, seqInfoAddr, 2);
        WriteEmptySection(builder, seqInfoAddr + 6, 2);
        WriteEmptyCPI(builder, seqInfoAddr + 12);
        return builder.ToArray();
    }

    private static byte[] BuildClpiWithSequenceInfo()
    {
        using var builder = new ClpiBinaryBuilder();
        const int stcContent = 1 + 1 + 4 + 1 + 1 + 2 + 4 + 4 + 4;
        var seqInfoAddr = checked((uint)(HeaderSize + 4 + ClipContentSize));
        var progInfoAddr = seqInfoAddr + 4 + stcContent;

        WriteHeader(builder, seqInfoAddr, progInfoAddr, progInfoAddr + 6);
        WriteClipInfo(builder, isCC5: false);

        builder.SeekTo((int)seqInfoAddr);
        builder.UInt32BE(stcContent);
        builder.Byte(0);
        builder.Byte(1);
        builder.UInt32BE(0);
        builder.Byte(1);
        builder.Byte(0);
        builder.UInt16BE(0x1011);
        builder.UInt32BE(0);
        builder.UInt32BE(0);
        builder.UInt32BE(45000000);

        WriteEmptySection(builder, progInfoAddr, 2);
        WriteEmptyCPI(builder, progInfoAddr + 6);
        return builder.ToArray();
    }

    private static byte[] BuildClpiWithCC5()
    {
        using var builder = new ClpiBinaryBuilder();
        var seqInfoAddr = checked((uint)(HeaderSize + 4 + ClipCC5ContentSize));

        WriteHeader(builder, seqInfoAddr, seqInfoAddr + 6, seqInfoAddr + 12);
        WriteClipInfo(builder, isCC5: true);
        WriteEmptySection(builder, seqInfoAddr, 2);
        WriteEmptySection(builder, seqInfoAddr + 6, 2);
        WriteEmptyCPI(builder, seqInfoAddr + 12);
        return builder.ToArray();
    }

    private static byte[] BuildClpiWithTwoSTCSequences()
    {
        using var builder = new ClpiBinaryBuilder();
        const int stcContent = 1 + 1 + 4 + 1 + 1 + 2 + 4 + 4 + 4 + 2 + 4 + 4 + 4;
        var seqInfoAddr = checked((uint)(HeaderSize + 4 + ClipContentSize));
        var progInfoAddr = seqInfoAddr + 4 + stcContent;

        WriteHeader(builder, seqInfoAddr, progInfoAddr, progInfoAddr + 6);
        WriteClipInfo(builder, isCC5: false);

        builder.SeekTo((int)seqInfoAddr);
        builder.UInt32BE(stcContent);
        builder.Byte(0);
        builder.Byte(1);
        builder.UInt32BE(0);
        builder.Byte(2);
        builder.Byte(0);
        builder.UInt16BE(0x1011);
        builder.UInt32BE(0);
        builder.UInt32BE(0);
        builder.UInt32BE(45000000);
        builder.UInt16BE(0x1012);
        builder.UInt32BE(1000);
        builder.UInt32BE(450000);
        builder.UInt32BE(20000000);

        WriteEmptySection(builder, progInfoAddr, 2);
        WriteEmptyCPI(builder, progInfoAddr + 6);
        return builder.ToArray();
    }

    private const int ClipContentSize = 144;
    private const int ClipCC5ContentSize = 160;

    private static void WriteEmptyCPI(ClpiBinaryBuilder builder, long addr)
    {
        builder.SeekTo((int)addr);
        builder.UInt32BE(0);
    }

    private static void WriteHeader(ClpiBinaryBuilder builder, long seqInfoAddr, long progInfoAddr, long cpiAddr, string version = "0200")
    {
        builder.Ascii("HDMV");
        builder.Ascii(version);
        builder.UInt32BE(checked((uint)seqInfoAddr));
        builder.UInt32BE(checked((uint)progInfoAddr));
        builder.UInt32BE(checked((uint)cpiAddr));
        builder.UInt32BE(0);
        builder.UInt32BE(0);
        builder.Reserved(12);
    }

    private static void WriteClipInfo(ClpiBinaryBuilder builder, bool isCC5)
    {
        builder.UInt32BE(isCC5 ? (uint)ClipCC5ContentSize : ClipContentSize);
        builder.Reserved(2);
        builder.Byte(1);
        builder.Byte(1);
        builder.Reserved(3);
        builder.Byte(isCC5 ? (byte)1 : (byte)0);
        builder.UInt32BE(45000000);
        builder.UInt32BE(1000000);
        builder.Reserved(128);
        if (isCC5)
        {
            builder.Reserved(16);
        }
    }

    private static long WriteEmptySection(ClpiBinaryBuilder builder, long addr, int contentBytes)
    {
        builder.SeekTo((int)addr);
        if (contentBytes < 2)
        {
            contentBytes = 2;
        }

        builder.UInt32BE((uint)contentBytes);
        for (var i = 0; i < contentBytes; i++)
        {
            builder.Byte(0);
        }

        return addr + 4 + (uint)contentBytes;
    }
}
