using System.Buffers.Binary;
using ChapterTool.Core.Importing.Disc.Index;

namespace ChapterTool.Core.Tests.Importing;

public sealed class IndexImporterTests
{
    private const int HeaderSize = 40;
    private const int AppInfoSize = 40;
    private const int TitleEntrySize = 12;
    private const int IndexesLengthSize = 4;
    private const int IndexesMinContentSize = 64;
    private const int AppInfoDataSize = 36;
    private const int UserDataPadding = 23;

    [Fact]
    public void InvalidHeaderThrows()
    {
        using var stream = new MemoryStream("BAD\x00"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => IndexFile.Read(stream));
    }

    [Fact]
    public void UnsupportedVersionThrows()
    {
        using var stream = new MemoryStream(BuildHeader("0400"));
        Assert.Throws<InvalidDataException>(() => IndexFile.Read(stream));
    }

    [Fact]
    public void TryReadReturnsNullOnError()
    {
        var result = IndexFile.TryRead("/nonexistent/path/index.bdmv");
        Assert.Null(result);
    }

    [Fact]
    public void ValidIndexParsesAppInfoBDMV()
    {
        var bytes = BuildMinimalIndex();
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Equal("INDX", index.TypeIndicator);
        Assert.Equal("0100", index.VersionNumber);
        Assert.NotNull(index.AppInfoBDMV);
        Assert.False(index.AppInfoBDMV.InitialOutputModePreference);
        Assert.False(index.AppInfoBDMV.SSContentExistFlag);
        Assert.Equal((byte)1, index.AppInfoBDMV.VideoFormat);
        Assert.Equal((byte)1, index.AppInfoBDMV.FrameRate);
        Assert.Equal("TEST DISC", index.AppInfoBDMV.UserData.TrimEnd('\0').Trim());
    }

    [Fact]
    public void ValidIndexParsesIndexes()
    {
        var bytes = BuildMinimalIndex();
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.NotNull(index.Indexes);
        Assert.NotNull(index.Indexes.FirstPlaybackTitle);
        Assert.NotNull(index.Indexes.TopMenuTitle);
    }

    [Fact]
    public void RealIndexWithThirtyEightByteIndexesSectionParses()
    {
        var path = FixtureResolver.Fixture(
            "Importing",
            "Disc",
            "Bdmv",
            "Detective Conan The Bride of Halloween/DISC1",
            "BDMV",
            "index.bdmv");

        var index = IndexFile.TryRead(path, out var error);

        Assert.Null(error);
        Assert.NotNull(index);
        Assert.Equal(38U, index.Indexes.Length);
        Assert.Single(index.Indexes.MovieTitles);
        Assert.Equal("00002", index.Indexes.MovieTitles.Single().ObjectData);
    }

    [Fact]
    public void IndexParsesMovieTitles()
    {
        var movieTitles = new (byte ObjectType, byte PlaybackType, string Data)[]
        {
            (1, 0, "00000"),
            (1, 2, "00001"),
        };
        var bytes = BuildIndexWithTitles(movieTitles);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Equal(0, index.Indexes.FirstPlaybackTitle.ObjectType);
        Assert.Equal(0, index.Indexes.TopMenuTitle.ObjectType);
        Assert.Equal(2, index.Indexes.Titles.Count);
        Assert.Equal(2, index.Indexes.MovieTitles.Count());

        var titles = index.Indexes.MovieTitles.ToList();
        Assert.All(titles, title =>
        {
            Assert.True(title.IsMovieObject);
            Assert.True(title.IsMoviePlayback);
            Assert.False(title.IsBDJObject);
            Assert.False(title.IsInteractivePlayback);
        });
    }

    [Fact]
    public void IndexFiltersNonMovieTitles()
    {
        var titles = new (byte ObjectType, byte PlaybackType, string Data)[]
        {
            (1, 0, "00001"),
            (2, 0, "BDJ__"),
            (1, 1, "00002"),
        };
        var bytes = BuildIndexWithTitles(titles);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Equal(3, index.Indexes.Titles.Count);
        Assert.Single(index.Indexes.MovieTitles);

        var movie = index.Indexes.MovieTitles.Single();
        Assert.Equal("00001", movie.ObjectData);
    }

    [Fact]
    public void IndexWithBDJObjectIdentifiesCorrectly()
    {
        var titles = new (byte ObjectType, byte PlaybackType, string Data)[]
        {
            (2, 0, "BDJ__"),
        };
        var bytes = BuildIndexWithTitles(titles);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        var title = index.Indexes.Titles.Single();
        Assert.True(title.IsBDJObject);
        Assert.False(title.IsMovieObject);
        Assert.True(title.IsMoviePlayback);
    }

    [Fact]
    public void IndexWithInteractivePlaybackIdentifiesCorrectly()
    {
        var titles = new (byte ObjectType, byte PlaybackType, string Data)[]
        {
            (1, 1, "00001"),
        };
        var bytes = BuildIndexWithTitles(titles);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        var title = index.Indexes.Titles.Single();
        Assert.True(title.IsMovieObject);
        Assert.True(title.IsInteractivePlayback);
        Assert.False(title.IsMoviePlayback);
    }

    [Fact]
    public void IndexWithAppInfoFlagsParsesCorrectly()
    {
        var bytes = BuildIndexWithAppInfo(
            flags: 0x6A,
            videoFormat: 2,
            frameRate: 3);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.True(index.AppInfoBDMV.InitialOutputModePreference);
        Assert.True(index.AppInfoBDMV.SSContentExistFlag);
        Assert.Equal((byte)2, index.AppInfoBDMV.VideoFormat);
        Assert.Equal((byte)3, index.AppInfoBDMV.FrameRate);
        Assert.Equal((byte)0x0A, index.AppInfoBDMV.InitialDynamicRangeType);
    }

    [Theory]
    [InlineData(0x40, true, false, 0)]
    [InlineData(0x20, false, true, 0)]
    [InlineData(0x0B, false, false, 0x0B)]
    public void IndexAppInfoUsesStandardBitPositions(byte flags, bool outputMode, bool contentExists, byte dynamicRange)
    {
        using var stream = new MemoryStream(BuildIndexWithAppInfo(flags, 2, 3));
        var index = IndexFile.Read(stream);

        Assert.Equal(outputMode, index.AppInfoBDMV.InitialOutputModePreference);
        Assert.Equal(contentExists, index.AppInfoBDMV.SSContentExistFlag);
        Assert.Equal(dynamicRange, index.AppInfoBDMV.InitialDynamicRangeType);
    }

    [Fact]
    public void IndexWithZeroTitlesHasEmptyMovieList()
    {
        var bytes = BuildIndexWithTitles([]);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Empty(index.Indexes.Titles);
        Assert.Empty(index.Indexes.MovieTitles);
    }

    [Fact]
    public void IndexVersion0200IsAccepted()
    {
        using var stream = new MemoryStream(BuildHeader("0200"));
        var index = IndexFile.Read(stream);
        Assert.Equal("0200", index.VersionNumber);
    }

    [Fact]
    public void IndexVersion0300IsAccepted()
    {
        using var stream = new MemoryStream(BuildHeader("0300"));
        var index = IndexFile.Read(stream);
        Assert.Equal("0300", index.VersionNumber);
    }

    [Fact]
    public void IndexVersion0240IsAccepted()
    {
        using var stream = new MemoryStream(BuildHeader("0240"));
        var index = IndexFile.Read(stream);
        Assert.Equal("0240", index.VersionNumber);
    }

    [Fact]
    public void IndexExtensionDataStartAddressZeroUsesStreamEnd()
    {
        var bytes = BuildMinimalIndex(extensionDataStartAddress: 0U);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.NotNull(index.Indexes);
        Assert.NotNull(index.AppInfoBDMV);
    }

    [Fact]
    public void IndexWithShortUserDataParsesCorrectly()
    {
        var bytes = BuildIndexWithShortUserData(16);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Equal("SHORTDATA", index.AppInfoBDMV.UserData.TrimEnd('\0').Trim());
    }

    [Fact]
    public void IndexWithEmptyUserDataParsesCorrectly()
    {
        var bytes = BuildIndexWithShortUserData(0);
        using var stream = new MemoryStream(bytes);
        var index = IndexFile.Read(stream);

        Assert.Equal(string.Empty, index.AppInfoBDMV.UserData.TrimEnd('\0'));
    }

    [Fact]
    public void TryReadWithErrorCapturesExceptionMessage()
    {
        var result = IndexFile.TryRead("/nonexistent/path/index.bdmv", out var error);
        Assert.Null(result);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(3, true, true)]
    public void IndexTitleAccessTypeExposesProhibitedAndHiddenState(byte accessType, bool prohibited, bool hidden)
    {
        var title = new IndexTitleEntry(1, accessType, 0, new IndexHdmvObjectReference(1));

        Assert.Equal(prohibited, title.IsAccessProhibited);
        Assert.Equal(hidden, title.IsHidden);
    }

    [Fact]
    public void IndexExtensionParsesUhdMetadataAndPreservesRawEntry()
    {
        var bytes = new byte[40];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 36);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 24);
        bytes[11] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), 24);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 16);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(24), 8);
        bytes[28] = 0xA1;
        bytes[30] = 0x17;
        using var stream = new MemoryStream(bytes);

        var extension = IndexExtensionData.Read(stream);

        var uhd = Assert.IsType<IndexUhdMetadata>(extension.UhdMetadata);
        Assert.Equal((byte)0x0A, uhd.DiscType);
        Assert.True(uhd.Exists4K);
        Assert.Equal((byte)3, uhd.HdrFlags);
        Assert.True(uhd.Hdr10Plus);
        Assert.True(uhd.DolbyVision);
        Assert.Equal(16, extension.RawEntries["3.1"].Length);
    }

    [Fact]
    public void IndexExtensionRejectsEntryOutsideSection()
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 20);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 24);
        bytes[11] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), 24);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 16);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => IndexExtensionData.Read(stream));
    }

    private static byte[] BuildMinimalIndex(uint? extensionDataStartAddress = null)
    {
        return BuildIndexWithTitles([], extensionDataStartAddress);
    }

    private static byte[] BuildHeader(string version = "0100")
    {
        var indexesAddress = HeaderSize + AppInfoSize;
        var indexesTotalSize = IndexesLengthSize + IndexesMinContentSize;
        var extAddress = indexesAddress + indexesTotalSize;

        using var builder = new IndexBinaryBuilder();
        builder.Ascii("INDX");
        builder.Ascii(version);
        builder.UInt32BE((uint)indexesAddress);
        builder.UInt32BE((uint)extAddress);
        builder.Reserved(24);

        builder.UInt32BE(AppInfoDataSize);
        builder.Byte(0);
        builder.Byte(0);
        builder.Byte(0x10);
        builder.Byte(0x01);
        builder.Ascii("TEST DISC");
        builder.Reserved(UserDataPadding);

        builder.SeekTo(indexesAddress);
        builder.UInt32BE(IndexesMinContentSize);
        builder.Reserved(TitleEntrySize);
        builder.Reserved(TitleEntrySize);
        builder.UInt16BE(0);
        builder.Reserved(IndexesMinContentSize - (TitleEntrySize + TitleEntrySize + 2));

        return builder.ToArray();
    }

    private static byte[] BuildIndexWithTitles(
        (byte ObjectType, byte PlaybackType, string Data)[] titles,
        uint? extensionDataStartAddress = null)
    {
        var indexesAddress = HeaderSize + AppInfoSize;
        var indexesContentSize = TitleEntrySize + TitleEntrySize + 2 + titles.Length * TitleEntrySize;
        var paddedContentSize = Math.Max(indexesContentSize, IndexesMinContentSize);
        var indexesLength = paddedContentSize;
        var indexesTotalSize = IndexesLengthSize + paddedContentSize;
        var extAddress = extensionDataStartAddress ?? (uint)(indexesAddress + indexesTotalSize);

        using var builder = new IndexBinaryBuilder();

        // Header
        builder.Ascii("INDX");
        builder.Ascii("0100");
        builder.UInt32BE((uint)indexesAddress);
        builder.UInt32BE(extAddress);
        builder.Reserved(24);

        // AppInfoBDMV
        builder.UInt32BE(AppInfoDataSize);
        builder.Byte(0);
        builder.Byte(0);
        builder.Byte(0x10);
        builder.Byte(0x01);
        builder.Ascii("TEST DISC");
        builder.Reserved(UserDataPadding);

        // Indexes
        builder.SeekTo(indexesAddress);
        builder.UInt32BE((uint)indexesLength);
        WriteTitleEntry(builder, 0, 0, "      ");
        WriteTitleEntry(builder, 0, 0, "      ");
        builder.UInt16BE(checked((ushort)titles.Length));
        foreach (var title in titles)
        {
            WriteTitleEntry(builder, title.ObjectType, title.PlaybackType, title.Data);
        }

        // Pad remaining to reach padded size
        var paddingNeeded = paddedContentSize - indexesContentSize;
        if (paddingNeeded > 0)
        {
            builder.Reserved(paddingNeeded);
        }

        return builder.ToArray();
    }

    private static byte[] BuildIndexWithAppInfo(byte flags, byte videoFormat, byte frameRate)
    {
        var indexesAddress = HeaderSize + AppInfoSize;
        var indexesLength = IndexesMinContentSize;
        var indexesTotalSize = IndexesLengthSize + indexesLength;
        var extAddress = indexesAddress + indexesTotalSize;

        using var builder = new IndexBinaryBuilder();

        builder.Ascii("INDX");
        builder.Ascii("0100");
        builder.UInt32BE((uint)indexesAddress);
        builder.UInt32BE((uint)extAddress);
        builder.Reserved(24);

        builder.UInt32BE(AppInfoDataSize);
        builder.Byte(0);
        builder.Byte(flags);
        builder.Byte((byte)(videoFormat << 4));
        builder.Byte((byte)(frameRate & 0x0f));
        builder.Ascii("FLAG TEST DISC");
        builder.Reserved(32 - "FLAG TEST DISC".Length);

        builder.SeekTo(indexesAddress);
        builder.UInt32BE((uint)indexesLength);
        builder.Reserved(TitleEntrySize);
        builder.Reserved(TitleEntrySize);
        builder.UInt16BE(0);
        builder.Reserved(indexesLength - (TitleEntrySize + TitleEntrySize + 2));

        return builder.ToArray();
    }

    private static void WriteTitleEntry(IndexBinaryBuilder builder, byte objectType, byte playbackType, string data)
    {
        var firstByte = (byte)((objectType << 6) | ((0 & 0x03) << 4));
        builder.Byte(firstByte);
        builder.Reserved(3);
        var playbackByte = (byte)((playbackType << 6) & 0xC0);
        builder.Byte(playbackByte);
        builder.Byte(0);

        if (objectType == 1)
        {
            builder.UInt16BE(ushort.Parse(data));
            builder.Reserved(4);
            return;
        }

        var padded = data.Length switch
        {
            < 5 => (data + new string('\0', 5 - data.Length)).PadRight(6, '\0'),
            < 6 => data.PadRight(6, '\0'),
            _ => data[..6]
        };
        builder.Ascii(padded);
    }

    private static byte[] BuildIndexWithShortUserData(int userDataLength)
    {
        var appDataSize = 4 + userDataLength;
        var appTotalSize = 4 + appDataSize;
        var indexesAddress = HeaderSize + appTotalSize;
        var indexesLength = IndexesMinContentSize;
        var indexesTotalSize = IndexesLengthSize + indexesLength;
        var extAddress = indexesAddress + indexesTotalSize;

        using var builder = new IndexBinaryBuilder();

        builder.Ascii("INDX");
        builder.Ascii("0100");
        builder.UInt32BE((uint)indexesAddress);
        builder.UInt32BE((uint)extAddress);
        builder.Reserved(24);

        builder.UInt32BE((uint)appDataSize);
        builder.Byte(0);
        builder.Byte(0);
        builder.Byte(0x10);
        builder.Byte(0x01);
        builder.Ascii("SHORTDATA"[..Math.Min(userDataLength, 9)]);
        if (userDataLength > 9)
        {
            builder.Reserved(userDataLength - 9);
        }

        builder.SeekTo(indexesAddress);
        builder.UInt32BE((uint)indexesLength);
        builder.Reserved(TitleEntrySize);
        builder.Reserved(TitleEntrySize);
        builder.UInt16BE(0);
        builder.Reserved(indexesLength - (TitleEntrySize + TitleEntrySize + 2));

        return builder.ToArray();
    }

}
