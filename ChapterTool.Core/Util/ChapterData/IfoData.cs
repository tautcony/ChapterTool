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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class IfoData
    {
        public static IEnumerable<ChapterInfo> GetStreams(string ifoFile)
        {
            var pgcCount = IfoParser.GetPGCnb(ifoFile);
            for (var i = 1; i <= pgcCount; i++)
            {
                yield return GetChapterInfo(ifoFile, i);
            }
        }

        private static ChapterInfo GetChapterInfo(string location, int titleSetNum)
        {
            var titleRegex = new Regex(@"^VTS_(\d+)_0\.IFO", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var result = titleRegex.Match(location);
            if (result.Success) titleSetNum = int.Parse(result.Groups[1].Value);

            var pgc = new ChapterInfo
            {
                SourceType = "DVD",
            };
            var fileName = Path.GetFileNameWithoutExtension(location);
            Debug.Assert(fileName != null, "file name must not be null");
            if (fileName.Count(ch => ch == '_') == 2)
            {
                var barIndex = fileName.LastIndexOf('_');
                pgc.Title = pgc.SourceName = $"{fileName.Substring(0, barIndex)}_{titleSetNum}";
            }

            pgc.Chapters = GetChapters(location, titleSetNum, out var duration, out var isNTSC);
            pgc.Duration = duration;
            pgc.FramesPerSecond = isNTSC ? 30000M / 1001 : 25;

            if (pgc.Duration.TotalSeconds < 10)
                pgc = null;

            return pgc;
        }

        private static List<Chapter> GetChapters(string ifoFile, int programChain, out IfoTimeSpan duration, out bool isNTSC)
        {
            var chapters = new List<Chapter>();
            duration = IfoTimeSpan.Zero;
            isNTSC = true;

            var stream = new FileStream(ifoFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var pcgItPosition = stream.GetPCGIP_Position();
            var programChainPrograms = -1;
            var programTime = TimeSpan.Zero;
            if (programChain >= 0)
            {
                var chainOffset = stream.GetChainOffset(pcgItPosition, programChain);

                // programTime          = stream.ReadTimeSpan(pcgItPosition, chainOffset, out _) ?? TimeSpan.Zero;
                programChainPrograms = stream.GetNumberOfPrograms(pcgItPosition, chainOffset);
            }
            else
            {
                var programChains = stream.GetProgramChains(pcgItPosition);
                for (var curChain = 1; curChain <= programChains; curChain++)
                {
                    var chainOffset = stream.GetChainOffset(pcgItPosition, curChain);
                    var time = stream.ReadTimeSpan(pcgItPosition, chainOffset, out _);
                    if (time == null) break;

                    if (time.Value <= programTime) continue;
                    programChain = curChain;
                    programChainPrograms = stream.GetNumberOfPrograms(pcgItPosition, chainOffset);
                    programTime = time.Value;
                }
            }
            if (programChain < 0) return null;

            chapters.Add(new Chapter { Name = "Chapter 01", Time = TimeSpan.Zero });

            var longestChainOffset = stream.GetChainOffset(pcgItPosition, programChain);
            int programMapOffset = IfoParser.ToInt16(stream.GetFileBlock((pcgItPosition + longestChainOffset) + 230, 2));
            int cellTableOffset = IfoParser.ToInt16(stream.GetFileBlock((pcgItPosition + longestChainOffset) + 0xE8, 2));
            for (var currentProgram = 0; currentProgram < programChainPrograms; ++currentProgram)
            {
                int entryCell = stream.GetFileBlock(((pcgItPosition + longestChainOffset) + programMapOffset) + currentProgram, 1)[0];
                var exitCell = entryCell;
                if (currentProgram < (programChainPrograms - 1))
                    exitCell = stream.GetFileBlock(((pcgItPosition + longestChainOffset) + programMapOffset) + (currentProgram + 1), 1)[0] - 1;

                var totalTime = IfoTimeSpan.Zero;
                for (var currentCell = entryCell; currentCell <= exitCell; currentCell++)
                {
                    var cellStart = cellTableOffset + ((currentCell - 1) * 0x18);
                    var bytes = stream.GetFileBlock((pcgItPosition + longestChainOffset) + cellStart, 4);
                    var cellType = bytes[0] >> 6;
                    if (cellType == 0x00 || cellType == 0x01)
                    {
                        bytes = stream.GetFileBlock(((pcgItPosition + longestChainOffset) + cellStart) + 4, 4);
                        var ret = IfoParser.ReadTimeSpan(bytes, out isNTSC) ?? IfoTimeSpan.Zero;
                        totalTime.IsNTSC = ret.IsNTSC;
                        totalTime += ret;
                    }
                }

                duration.IsNTSC = totalTime.IsNTSC;
                duration += totalTime;
                if (currentProgram + 1 < programChainPrograms)
                    chapters.Add(new Chapter { Name = $"Chapter {currentProgram + 2:D2}", Time = duration });
            }
            stream.Dispose();
            return chapters;
        }
    }

    public struct IfoTimeSpan
    {
        public long TotalFrames { get; set; }

        public bool IsNTSC { get; set; }

        public int RawFrameRate => IsNTSC ? 30 : 25;

        private decimal TimeFrameRate => IsNTSC ? 30000M / 1001 : 25;

        public int Hours => (int)Math.Round(TotalFrames / TimeFrameRate / 3600);

        public int Minutes => (int)Math.Round(TotalFrames / TimeFrameRate / 60) % 60;

        public int Second => (int)Math.Round(TotalFrames / TimeFrameRate) % 60;

        public static readonly IfoTimeSpan Zero = new IfoTimeSpan(true);

        public IfoTimeSpan(bool isNTSC)
        {
            TotalFrames = 0;
            IsNTSC = isNTSC;
        }

        private IfoTimeSpan(long totalFrames, bool isNTSC)
        {
            IsNTSC = isNTSC;
            TotalFrames = totalFrames;
        }

        public IfoTimeSpan(int seconds, int frames, bool isNTSC)
        {
            IsNTSC = isNTSC;
            TotalFrames = frames;
            TotalFrames += seconds * RawFrameRate;
        }

        public IfoTimeSpan(int hour, int minute, int second, int frames, bool isNTSC)
        {
            IsNTSC = isNTSC;
            TotalFrames = frames;
            TotalFrames += ((hour * 3600) + (minute * 60) + second) * RawFrameRate;
        }

        public IfoTimeSpan(TimeSpan time, bool isNTSC)
        {
            IsNTSC = isNTSC;
            TotalFrames = 0;
            TotalFrames = (long)Math.Round((decimal)time.TotalSeconds / TimeFrameRate);
        }

        public static implicit operator TimeSpan(IfoTimeSpan time)
        {
            return new TimeSpan((long)Math.Round(time.TotalFrames / time.TimeFrameRate * TimeSpan.TicksPerSecond));
        }

        #region Operator
        private static void FrameRateModeCheck(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            if (t1.IsNTSC ^ t2.IsNTSC)
                throw new InvalidOperationException("Unmatch frames rate mode");
        }

        public static IfoTimeSpan operator +(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return new IfoTimeSpan(t1.TotalFrames + t2.TotalFrames, t1.IsNTSC);
        }

        public static IfoTimeSpan operator -(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return new IfoTimeSpan(t1.TotalFrames - t2.TotalFrames, t1.IsNTSC);
        }

        public static bool operator <(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames < t2.TotalFrames;
        }

        public static bool operator >(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames > t2.TotalFrames;
        }

        public static bool operator <=(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames <= t2.TotalFrames;
        }

        public static bool operator >=(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames >= t2.TotalFrames;
        }

        public static bool operator ==(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames == t2.TotalFrames;
        }

        public static bool operator !=(IfoTimeSpan t1, IfoTimeSpan t2)
        {
            FrameRateModeCheck(t1, t2);
            return t1.TotalFrames != t2.TotalFrames;
        }
        #endregion

        public override int GetHashCode()
        {
            return ((TotalFrames << 1) | (IsNTSC ? 1L : 0L)).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != GetType())
                return false;
            var time = (IfoTimeSpan)obj;
            return TotalFrames == time.TotalFrames && IsNTSC == time.IsNTSC;
        }

        public override string ToString()
        {
            return $"{Hours:D2}:{Minutes:D2}:{Second:D2}.{TotalFrames % RawFrameRate}f [{TotalFrames}{(IsNTSC ? 'N' : 'P')}]";
        }
    }
}
