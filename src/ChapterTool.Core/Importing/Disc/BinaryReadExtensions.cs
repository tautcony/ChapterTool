using System.Text;

namespace ChapterTool.Core.Importing.Disc;

internal static class BinaryReadExtensions
{
    extension(Stream stream)
    {
        public byte[] ReadExactBytes(int length)
        {
            var bytes = new byte[length];
            var read = stream.Read(bytes, 0, length);
            if (read != length)
            {
                throw new EndOfStreamException();
            }

            return bytes;
        }

        public string ReadAscii(int length) =>
            Encoding.ASCII.GetString(stream.ReadExactBytes(length));

        public void SkipBytes(long length)
        {
            stream.Seek(length, SeekOrigin.Current);
            if (stream.Position > stream.Length)
            {
                throw new EndOfStreamException();
            }
        }

        public uint ReadUInt32BigEndian()
        {
            var b = stream.ReadExactBytes(4);
            return b[3] + ((uint)b[2] << 8) + ((uint)b[1] << 16) + ((uint)b[0] << 24);
        }

        public ushort ReadUInt16BigEndian()
        {
            var b = stream.ReadExactBytes(2);
            return (ushort)(b[1] + (b[0] << 8));
        }
    }
}
