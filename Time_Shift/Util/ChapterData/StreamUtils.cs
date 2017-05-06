﻿using System.IO;

namespace ChapterTool.Util.ChapterData
{
    internal static class StreamUtils
    {
        public static byte[] ReadBytes(this Stream fs, int length)
        {
            var ret = new byte[length];
            fs.Read(ret, 0, length);
            return ret;
        }

        public static void Skip(this Stream fs, long length)
        {
            fs.Seek(length, SeekOrigin.Current);
        }

        #region int reader

        public static ulong BEInt64(this Stream fs)
        {
            var b = fs.ReadBytes(8);
            return b[7] + ((ulong)b[6] << 8) + ((ulong)b[5] << 16) + ((ulong)b[4] << 24) +
                ((ulong)b[3] << 32) + ((ulong)b[2] << 40) + ((ulong)b[1] << 48) + ((ulong)b[0] << 56);
        }

        public static uint BEInt32(this Stream fs)
        {
            var b = fs.ReadBytes(4);
            return b[3] + ((uint)b[2] << 8) + ((uint)b[1] << 16) + ((uint)b[0] << 24);
        }

        public static uint LEInt32(this Stream fs)
        {
            var b = fs.ReadBytes(4);
            return b[0] + ((uint)b[1] << 8) + ((uint)b[2] << 16) + ((uint)b[3] << 24);
        }

        public static int BEInt24(this Stream fs)
        {
            var b = fs.ReadBytes(3);
            return b[2] + (b[1] << 8) + (b[0] << 16);
        }

        public static int LEInt24(this Stream fs)
        {
            var b = fs.ReadBytes(3);
            return b[0] + (b[1] << 8) + (b[2] << 16);
        }

        public static int BEInt16(this Stream fs)
        {
            var b = fs.ReadBytes(2);
            return b[1] + (b[0] << 8);
        }

        public static int LEInt16(this Stream fs)
        {
            var b = fs.ReadBytes(2);
            return b[0] + (b[1] << 8);
        }
        #endregion
    }
}