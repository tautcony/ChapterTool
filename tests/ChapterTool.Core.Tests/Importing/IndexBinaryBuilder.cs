using System.Text;

namespace ChapterTool.Core.Tests.Importing;

internal sealed class IndexBinaryBuilder : IDisposable
{
    private readonly MemoryStream stream = new();

    public int Position => (int)stream.Position;

    public MemoryStream Build()
    {
        stream.Position = 0;
        return stream;
    }

    public byte[] ToArray() => stream.ToArray();

    public IndexBinaryBuilder UInt32BE(uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public IndexBinaryBuilder UInt16BE(ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
        return this;
    }

    public IndexBinaryBuilder Byte(byte value)
    {
        stream.WriteByte(value);
        return this;
    }

    public IndexBinaryBuilder Reserved(int count)
    {
        stream.Write(new byte[count]);
        return this;
    }

    public IndexBinaryBuilder Ascii(string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
        return this;
    }

    public IndexBinaryBuilder SeekTo(int position)
    {
        stream.Position = position;
        return this;
    }

    public void Dispose() => stream.Dispose();
}
