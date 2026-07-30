using System.Text;

namespace ChapterTool.Core.Tests.Importing;

/// <summary>
/// Fluent builder for constructing MPLS binary test data.
/// Replaces manual byte-by-byte construction and scattered WriteUInt* helpers.
/// </summary>
internal sealed class MplsBinaryBuilder : IDisposable
{
    private readonly MemoryStream stream = new();

    public int Position => (int)stream.Position;

    /// <summary>Reset position to 0 and return the underlying stream.</summary>
    public MemoryStream Build()
    {
        stream.Position = 0;
        return stream;
    }

    public byte[] ToArray() => stream.ToArray();

    public MplsBinaryBuilder UInt32BE(uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public MplsBinaryBuilder UInt16BE(ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public MplsBinaryBuilder Byte(byte value)
    {
        stream.WriteByte(value);
        return this;
    }

    public MplsBinaryBuilder Reserved(int count)
    {
        stream.Write(new byte[count]);
        return this;
    }

    public MplsBinaryBuilder Ascii(string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
        return this;
    }

    /// <summary>Write a 5-char info file name + 4-char codec identifier.</summary>
    public MplsBinaryBuilder ClipName(string infoFileName, string codecId)
    {
        Ascii(infoFileName.PadRight(5)[..5]);
        Ascii(codecId.PadRight(4)[..4]);
        return this;
    }

    public MplsBinaryBuilder SeekTo(int position)
    {
        stream.Position = position;
        return this;
    }

    /// <summary>
    /// Write an extension-data entry table header: 3 reserved bytes + 1-byte count,
    /// followed by each entry (type, version, startAddress, length).
    /// For error-testing, pass count without actual entries.
    /// </summary>
    public MplsBinaryBuilder ExtensionDataEntryTable(
        byte numberOfEntries,
        params (ushort Type, ushort Version, uint StartAddress, uint Length)[] entries)
    {
        Reserved(3);
        Byte(numberOfEntries);
        foreach (var (type, version, start, length) in entries)
        {
            UInt16BE(type);
            UInt16BE(version);
            UInt32BE(start);
            UInt32BE(length);
        }

        return this;
    }

    public void Dispose() => stream.Dispose();
}
