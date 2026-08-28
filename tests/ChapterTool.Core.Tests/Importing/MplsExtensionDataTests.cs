using System.Text;
using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MplsExtensionDataTests
{
    [Fact]
    public void ReadParsesPipSubpathAndStaticMetadataEntries()
    {
        using var stream = new MemoryStream(BuildSectionWithThreeEntryTypes());

        var result = MplsExtensionData.Read(stream);

        Assert.Equal(156U, result.Length);
        Assert.Equal(3, result.ExtDataEntries.Count);
        Assert.Equal((ushort)1, result.ExtDataEntries[0].ExtDataType);
        Assert.Equal((ushort)2, result.ExtDataEntries[1].ExtDataType);
        Assert.Equal((ushort)3, result.ExtDataEntries[2].ExtDataType);

        var pip = Assert.Single(result.PipMetadata);
        Assert.Equal((ushort)1, pip.ClipReference);
        Assert.Equal((byte)0, pip.TimelineType);
        Assert.False(pip.LumaKeyFlag);
        Assert.False(pip.TrickPlayFlag);
        var pipData = Assert.Single(pip.Data);
        Assert.Equal(10U, pipData.Time);

        var subPath = Assert.Single(result.ExtensionSubPaths);
        Assert.Equal((byte)1, subPath.SubPathType);
        Assert.False(subPath.IsRepeatSubPath);
        var subPlayItem = Assert.Single(subPath.SubPlayItems);
        Assert.Equal("00001", subPlayItem.ClipName.ClipInformationFileName);
        Assert.Equal("M2TS", subPlayItem.ClipName.ClipCodecIdentifier);
        Assert.Equal((byte)0, subPlayItem.ConnectionCondition);

        var staticMetadata = Assert.Single(result.StaticMetadata);
        Assert.Equal((byte)1, staticMetadata.DynamicRangeType);
    }

    [Fact]
    public void ReadThrowsWhenEntryTableExceedsDeclaredLength()
    {
        using var stream = new MemoryStream(ExtensionSection(
            length: 8,
            dataBlockStartAddress: 0,
            entries: [],
            entriesCountOverride: 1,
            dataBlock: []));

        var exception = Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
        Assert.Contains("cannot contain its declared entries", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadThrowsWhenEntryExceedsExtensionSection()
    {
        using var stream = new MemoryStream(ExtensionSection(
            length: 20,
            dataBlockStartAddress: 0,
            entries: [new ExtensionEntry(0, 0, 0, uint.MaxValue)],
            dataBlock: []));

        var exception = Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
        Assert.Contains("exceeds the extension section", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadThrowsWhenDataBlockStartAddressIsInvalid()
    {
        using var stream = new MemoryStream(ExtensionSection(
            length: 20,
            dataBlockStartAddress: 4,
            entries: [new ExtensionEntry(0, 0, 0, 0)],
            dataBlock: []));

        var exception = Assert.Throws<InvalidDataException>(() => MplsExtensionData.Read(stream));
        Assert.Contains("data block start address", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSkipsEntriesOutsideTheDataBlock()
    {
        using var stream = new MemoryStream(ExtensionSection(
            length: 24,
            dataBlockStartAddress: 24,
            entries: [new ExtensionEntry(1, 1, 0, 28)],
            dataBlock: new byte[4]));

        var result = MplsExtensionData.Read(stream);

        Assert.Empty(result.PipMetadata);
    }

    private static byte[] BuildSectionWithThreeEntryTypes()
    {
        var pipData = PipEntryData();
        var subPathData = SubPathEntryData();
        var staticData = StaticMetadataEntryData();
        var dataBlock = Concat(pipData, subPathData, staticData);

        return ExtensionSection(
            length: (uint)(8 + 3 * 12 + dataBlock.Length),
            dataBlockStartAddress: 48,
            entries:
            [
                new ExtensionEntry(1, 1, 48, (uint)pipData.Length),
                new ExtensionEntry(2, 2, (uint)(48 + pipData.Length), (uint)subPathData.Length),
                new ExtensionEntry(3, 5, (uint)(48 + pipData.Length + subPathData.Length), (uint)staticData.Length)
            ],
            dataBlock: dataBlock);
    }

    private static byte[] PipEntryData()
    {
        using var builder = new MplsSectionBuilder();
        builder.UInt32BE(0);              // reserved header
        builder.UInt16BE(1);              // entry count
        builder.UInt16BE(1);              // clip reference
        builder.Byte(0);                  // secondary video reference
        builder.Byte(0);                  // reserved
        builder.UInt16BE(0);              // flags: no luma key, no trick play
        builder.UInt16BE(0);              // reserved (non-luma-key branch)
        builder.UInt16BE(0);              // reserved
        builder.UInt32BE(20);             // data address (relative to this entry)
        builder.UInt16BE(1);              // data count
        builder.UInt32BE(10);             // time
        builder.UInt32BE(0);              // packed position and scale
        return builder.ToArray();
    }

    private static byte[] SubPathEntryData()
    {
        using var builder = new MplsSectionBuilder();
        builder.UInt32BE(0);              // reserved header
        builder.UInt16BE(1);              // count
        builder.UInt32BE(36);             // subpath length
        builder.Byte(0);                  // reserved
        builder.Byte(1);                  // subpath type
        builder.UInt16BE(0);              // flag field
        builder.Byte(0);                  // reserved
        builder.Byte(1);                  // number of subplay items
        builder.UInt16BE(28);             // subplay item length
        builder.Ascii("00001");           // clip information file name
        builder.Ascii("M2TS");            // clip codec identifier
        builder.Reserved(3);
        builder.Byte(0);                  // flag field: no multi-clip entries
        builder.Byte(0);                  // ref to STC id
        builder.UInt32BE(0);              // in time
        builder.UInt32BE(0);              // out time
        builder.UInt16BE(0);              // sync play item id
        builder.UInt32BE(0);              // sync start PTS
        return builder.ToArray();
    }

    private static byte[] StaticMetadataEntryData()
    {
        using var builder = new MplsSectionBuilder();
        builder.UInt32BE(0);              // reserved header
        builder.Byte(1);                  // count
        builder.Reserved(3);
        builder.Byte(0x10);               // dynamic range type in high nibble
        builder.Reserved(3);
        for (var i = 0; i < 3; i++)
        {
            builder.UInt16BE(0);          // primary x
            builder.UInt16BE(0);          // primary y
        }

        for (var i = 0; i < 6; i++)
        {
            builder.UInt16BE(0);          // white point and luminance values
        }

        return builder.ToArray();
    }

    private static byte[] ExtensionSection(
        uint length,
        uint dataBlockStartAddress,
        IReadOnlyList<ExtensionEntry> entries,
        byte[] dataBlock,
        int entriesCountOverride = -1)
    {
        using var builder = new MplsSectionBuilder();
        builder.UInt32BE(length);
        builder.UInt32BE(dataBlockStartAddress);
        builder.Reserved(3);
        builder.Byte((byte)(entriesCountOverride >= 0 ? entriesCountOverride : entries.Count));
        foreach (var entry in entries)
        {
            builder.UInt16BE(entry.Type);
            builder.UInt16BE(entry.Version);
            builder.UInt32BE(entry.StartAddress);
            builder.UInt32BE(entry.Length);
        }

        builder.Raw(dataBlock);
        return builder.ToArray();
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(static array => array.Length)];
        var offset = 0;
        foreach (var array in arrays)
        {
            array.CopyTo(result, offset);
            offset += array.Length;
        }

        return result;
    }

    private sealed record ExtensionEntry(ushort Type, ushort Version, uint StartAddress, uint Length);

    private sealed class MplsSectionBuilder : IDisposable
    {
        private readonly MemoryStream stream = new();

        public MplsSectionBuilder UInt32BE(uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            return this;
        }

        public MplsSectionBuilder UInt16BE(ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            return this;
        }

        public MplsSectionBuilder Byte(byte value)
        {
            stream.WriteByte(value);
            return this;
        }

        public MplsSectionBuilder Reserved(int count)
        {
            stream.Write(new byte[count]);
            return this;
        }

        public MplsSectionBuilder Ascii(string value)
        {
            stream.Write(Encoding.ASCII.GetBytes(value));
            return this;
        }

        public MplsSectionBuilder Raw(byte[] bytes)
        {
            stream.Write(bytes);
            return this;
        }

        public byte[] ToArray() => stream.ToArray();

        public void Dispose() => stream.Dispose();
    }
}
