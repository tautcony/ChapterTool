using System.Text;
using ChapterTool.Core.Importing.Disc.Limits;

namespace ChapterTool.Core.Importing.Disc;

internal static class BinaryReadExtensions
{
    extension(Stream input)
    {
        /// <summary>
        /// Executes the ReadExactBytes operation.
        /// </summary>
        /// <param name="length">The span length.</param>
        /// <returns>The operation result.</returns>
        public byte[] ReadExactBytes(int length)
        {
            if (length is < 0 or > DiscBinaryReadLimits.MaximumExactReadBytes)
            {
                throw new InvalidDataException($"Requested binary read length {length} is outside the supported range.");
            }

            if (input is MplsBoundedStream bounded && length > bounded.Remaining)
            {
                throw new InvalidDataException($"Requested binary read length {length} crosses the MPLS container boundary.");
            }

            var bytes = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = input.Read(bytes, offset, length - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }

            return bytes;
        }

        /// <summary>
        /// Executes the ReadAscii operation.
        /// </summary>
        /// <param name="length">The span length.</param>
        /// <returns>The operation result.</returns>
        public string ReadAscii(int length) =>
            Encoding.ASCII.GetString(input.ReadExactBytes(length));

        /// <summary>
        /// Executes the SkipBytes operation.
        /// </summary>
        /// <param name="length">The span length.</param>
        /// <returns>The operation result.</returns>
        public void SkipBytes(long length)
        {
            if (length < 0)
            {
                throw new InvalidDataException("Cannot skip a negative number of bytes.");
            }

            if (!input.CanSeek || input.Position > input.Length || length > input.Length - input.Position)
            {
                throw new EndOfStreamException();
            }

            input.Seek(length, SeekOrigin.Current);
        }

        /// <summary>
        /// Executes the ReadUInt32BigEndian operation.
        /// </summary>
        /// <returns>The operation result.</returns>
        public uint ReadUInt32BigEndian()
        {
            var b = input.ReadExactBytes(4);
            return b[3] + ((uint)b[2] << 8) + ((uint)b[1] << 16) + ((uint)b[0] << 24);
        }

        /// <summary>
        /// Executes the ReadUInt16BigEndian operation.
        /// </summary>
        /// <returns>The operation result.</returns>
        public ushort ReadUInt16BigEndian()
        {
            var b = input.ReadExactBytes(2);
            return (ushort)(b[1] + (b[0] << 8));
        }
    }
}
