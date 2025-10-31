// ****************************************************************************
//
// Copyright (C) 2009-2015 Kurtnoise (kurtnoise@free.fr)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************

namespace ChapterTool.Util.ChapterData
{
    using System;
    using System.Diagnostics;
    using System.IO;

    public static class IfoParser
    {
        internal static byte[] GetFileBlock(this FileStream ifoStream, long position, int count)
        {
            if (position < 0) throw new Exception("Invalid Ifo file");
            var buf = new byte[count];
            ifoStream.Seek(position, SeekOrigin.Begin);
            ifoStream.Read(buf, 0, count);
            return buf;
        }

        private static byte? GetFrames(byte value)
        {
            // check whether the second bit of value is 1
            if (((value >> 6) & 0x01) == 1)
            {
                return (byte)BcdToInt((byte)(value & 0x3F)); // only last 6 bits is in use, show as BCD code
            }
            return null;
        }

        internal static long GetPCGIP_Position(this FileStream ifoStream)
        {
            return ToFilePosition(ifoStream.GetFileBlock(0xCC, 4));
        }

        internal static int GetProgramChains(this FileStream ifoStream, long pcgitPosition)
        {
            return ToInt16(ifoStream.GetFileBlock(pcgitPosition, 2));
        }

        internal static uint GetChainOffset(this FileStream ifoStream, long pcgitPosition, int programChain)
        {
            return ToInt32(ifoStream.GetFileBlock((pcgitPosition + (8 * programChain)) + 4, 4));
        }

        internal static int GetNumberOfPrograms(this FileStream ifoStream, long pcgitPosition, uint chainOffset)
        {
            return ifoStream.GetFileBlock((pcgitPosition + chainOffset) + 2, 1)[0];
        }

        internal static IfoTimeSpan? ReadTimeSpan(this FileStream ifoStream, long pcgitPosition, uint chainOffset, out bool isNTSC)
        {
            return ReadTimeSpan(ifoStream.GetFileBlock((pcgitPosition + chainOffset) + 4, 4), out isNTSC);
        }

        /// <param name="playbackBytes">
        /// byte[0] hours in bcd format<br/>
        /// byte[1] minutes in bcd format<br/>
        /// byte[2] seconds in bcd format<br/>
        /// byte[3] milliseconds in bcd format (2 high bits are the frame rate)
        /// </param>
        /// <param name="isNTSC">frame rate mode of the chapter</param>
        internal static IfoTimeSpan? ReadTimeSpan(byte[] playbackBytes, out bool isNTSC)
        {
            var frames = GetFrames(playbackBytes[3]);
            var fpsMask = playbackBytes[3] >> 6;
            Debug.Assert(fpsMask == 0x01 || fpsMask == 0x03, "only 25fps or 30fps is supported");

            // var fps = fpsMask == 0x01 ? 25M : fpsMask == 0x03 ? (30M / 1.001M) : 0;
            isNTSC = fpsMask == 0x03;
            if (frames == null) return null;
            try
            {
                var hours = BcdToInt(playbackBytes[0]);
                var minutes = BcdToInt(playbackBytes[1]);
                var seconds = BcdToInt(playbackBytes[2]);
                return new IfoTimeSpan(hours, minutes, seconds, (int)frames, isNTSC);
            }
            catch (Exception exception)
            {
                Logger.Log(exception.Message);
                return null;
            }
        }

        /// <summary>
        /// get number of PGCs
        /// </summary>
        /// <param name="fileName">name of the IFO file</param>
        /// <returns>number of PGS as an integer</returns>
        public static int GetPGCnb(string fileName)
        {
            var ifoStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var offset = ToInt32(GetFileBlock(ifoStream, 0xCC, 4));    // Read PGC offset
            ifoStream.Seek((2048 * offset) + 0x01, SeekOrigin.Begin);               // Move to beginning of PGC

            // long VTS_PGCITI_start_position = ifoStream.Position - 1;
            var nPGCs = ifoStream.ReadByte();       // Number of PGCs
            ifoStream.Close();
            return nPGCs;
        }

        internal static short ToInt16(byte[] bytes) => (short)((bytes[0] << 8) + bytes[1]);

        private static uint ToInt32(byte[] bytes) => (uint)((bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3]);

        public static int BcdToInt(byte value) => ((0xFF & (value >> 4)) * 10) + (value & 0x0F);

        private static long ToFilePosition(byte[] bytes) => ToInt32(bytes) * 0x800L;
    }
}
