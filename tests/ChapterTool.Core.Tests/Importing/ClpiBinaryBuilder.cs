using System.Text;

namespace ChapterTool.Core.Tests.Importing;

internal sealed class ClpiBinaryBuilder : IDisposable
{
    private readonly MemoryStream stream = new();

    public int Position => (int)stream.Position;

    public MemoryStream Build()
    {
        stream.Position = 0;
        return stream;
    }

    public byte[] ToArray() => stream.ToArray();

    public ClpiBinaryBuilder UInt32BE(uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public ClpiBinaryBuilder UInt16BE(ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public ClpiBinaryBuilder Byte(byte value)
    {
        stream.WriteByte(value);
        return this;
    }

    public ClpiBinaryBuilder Reserved(int count)
    {
        stream.Write(new byte[count]);
        return this;
    }

    public ClpiBinaryBuilder Ascii(string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
        return this;
    }

    public ClpiBinaryBuilder SeekTo(int position)
    {
        stream.Position = position;
        return this;
    }

    public ClpiBinaryBuilder Skip(int count)
    {
        stream.Position += count;
        return this;
    }

    public void Dispose() => stream.Dispose();
}
