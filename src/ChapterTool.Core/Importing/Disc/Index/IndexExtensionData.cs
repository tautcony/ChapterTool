namespace ChapterTool.Core.Importing.Disc.Index;

#pragma warning disable SA1503

internal sealed record IndexExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    IReadOnlyList<IndexExtensionEntry> Entries,
    IReadOnlyDictionary<string, byte[]> RawEntries,
    IndexUhdMetadata? UhdMetadata)
{
    internal static IndexExtensionData Read(Stream stream)
    {
        var sectionStart = stream.Position;
        var length = stream.ReadUInt32BigEndian();
        if (length == 0) return new IndexExtensionData(0, 0, [], new Dictionary<string, byte[]>(), null);
        using var container = MplsBoundedStream.Create(stream, length, 8, IndexParseLimits.MaximumExtensionLength, "INDEX extension data");
        var dataBlockStart = container.ReadUInt32BigEndian();
        container.SkipBytes(3);
        var count = container.ReadByteChecked();
        if (8L + count * 12L > length)
            throw new InvalidDataException("INDEX extension entry count exceeds the supported bounds.");

        var entries = new List<IndexExtensionEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = new IndexExtensionEntry(
                container.ReadUInt16BigEndian(),
                container.ReadUInt16BigEndian(),
                container.ReadUInt32BigEndian(),
                container.ReadUInt32BigEndian());
            if ((ulong)entry.StartAddress + entry.Length > length + 4UL)
                throw new InvalidDataException("INDEX extension entry exceeds the extension section.");
            entries.Add(entry);
        }

        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        IndexUhdMetadata? uhd = null;
        foreach (var entry in entries)
        {
            var entryPosition = sectionStart + entry.StartAddress;
            if (entryPosition < sectionStart || entryPosition + entry.Length > sectionStart + length + 4)
                throw new InvalidDataException("INDEX extension entry address is outside the section.");
            container.Position = entry.StartAddress - 4;
            var bytes = container.ReadExactBytes(checked((int)entry.Length));
            raw[$"{entry.Type}.{entry.Version}"] = bytes;
            if (entry is { Type: 3, Version: 1 }) uhd = IndexUhdMetadata.Read(bytes);
        }

        container.Complete("INDEX extension data");
        return new IndexExtensionData(length, dataBlockStart, entries, raw, uhd);
    }
}

internal sealed record IndexExtensionEntry(ushort Type, ushort Version, uint StartAddress, uint Length);

internal sealed record IndexUhdMetadata(
    byte DiscType,
    bool Exists4K,
    byte HdrFlags,
    bool Hdr10Plus,
    bool DolbyVision)
{
    internal static IndexUhdMetadata Read(byte[] data)
    {
        if (data.Length < 12) throw new InvalidDataException("INDEX UHD extension is truncated.");
        var discType = (byte)(data[4] >> 4);
        var exists4K = (data[4] & 0x01) != 0;
        var hdr10Plus = (data[6] & 0x10) != 0;
        var dolbyVision = (data[6] & 0x04) != 0;
        var hdrFlags = (byte)(data[6] & 0x03);
        return new IndexUhdMetadata(discType, exists4K, hdrFlags, hdr10Plus, dolbyVision);
    }
}
