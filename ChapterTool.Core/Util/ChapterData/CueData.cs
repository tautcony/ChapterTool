// ****************************************************************************
//
// Copyright (C) 2014-2016 TautCony (TautCony@vcb-s.com)
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
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using ChapterTool.ChapterData;

    public class CueData : IData
    {
        public ChapterInfo Chapter { get; private set; }

        /// <summary>
        /// 从文件中获取cue播放列表并转换为ChapterInfo
        /// </summary>
        /// <param name="path"></param>
        /// <param name="log"></param>
        public CueData(string path, Action<string> log = null)
        {
            string cueData;
            var ext = Path.GetExtension(path)?.ToLower();
            switch (ext)
            {
                case ".cue":
                    cueData = File.ReadAllBytes(path).GetUTFString();
                    if (string.IsNullOrEmpty(cueData))
                        throw new InvalidDataException("Empty cue file");
                    break;

                case ".flac":
                    cueData = GetCueFromFlac(path, log);
                    break;

                case ".tak":
                    cueData = GetCueFromTak(path);
                    break;

                default:
                    throw new Exception($"Invalid extension: {ext}");
            }
            if (string.IsNullOrEmpty(cueData))
                throw new Exception($"No Cue detected in {ext} file");
            Chapter = ParseCue(cueData);
        }

        private enum NextState
        {
            NsStart,
            NsNewTrack,
            NsTrack,
            NsError,
            NsFin,
        }

        private static readonly Regex RTitle = new Regex(@"TITLE\s+\""(.+)\""", RegexOptions.Compiled);
        private static readonly Regex RFile = new Regex(@"FILE\s+\""(.+)\""\s+(WAVE|MP3|AIFF|BINARY|MOTOROLA)", RegexOptions.Compiled);
        private static readonly Regex RTrack = new Regex(@"TRACK\s+(\d+)", RegexOptions.Compiled);
        private static readonly Regex RPerformer = new Regex(@"PERFORMER\s+\""(.+)\""", RegexOptions.Compiled);
        private static readonly Regex RTime = new Regex(@"INDEX\s+(?<index>\d+)\s+(?<M>\d{2}):(?<S>\d{2}):(?<F>\d{2})", RegexOptions.Compiled);

        /// <summary>
        /// 解析 cue 播放列表
        /// </summary>
        /// <param name="context">未分行的cue字符串</param>
        /// <returns></returns>
        public static ChapterInfo ParseCue(string context)
        {
            var lines = context.Split('\n');
            var cue = new ChapterInfo { SourceType = "CUE", Tag = context, TagType = context.GetType() };
            var nxState = NextState.NsStart;
            Chapter chapter = null;

            foreach (var line in lines)
            {
                switch (nxState)
                {
                    case NextState.NsStart:
                        var chapterTitleMatch = RTitle.Match(line);
                        var fileMatch = RFile.Match(line);
                        if (chapterTitleMatch.Success)
                        {
                            cue.Title = chapterTitleMatch.Groups[1].Value;

                            // nxState   = NextState.NsNewTrack;
                            break;
                        }

                        // Title 为非必需项，故当读取到File行时跳出
                        if (fileMatch.Success)
                        {
                            cue.SourceName = fileMatch.Groups[1].Value;
                            nxState = NextState.NsNewTrack;
                        }
                        break;

                    case NextState.NsNewTrack:

                        // 读到空行，解析终止
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            nxState = NextState.NsFin;
                            break;
                        }
                        var trackMatch = RTrack.Match(line);

                        // 读取到Track，获取其编号，跳至下一步
                        if (trackMatch.Success)
                        {
                            chapter = new Chapter { Number = int.Parse(trackMatch.Groups[1].Value) };
                            nxState = NextState.NsTrack;
                        }
                        break;

                    case NextState.NsTrack:
                        var trackTitleMatch = RTitle.Match(line);
                        var performerMatch = RPerformer.Match(line);
                        var timeMatch = RTime.Match(line);

                        // 获取章节名
                        if (trackTitleMatch.Success)
                        {
                            Debug.Assert(chapter != null, "chapter must not be null");
                            chapter.Name = trackTitleMatch.Groups[1].Value.Trim('\r');
                            break;
                        }

                        // 获取艺术家名
                        if (performerMatch.Success)
                        {
                            Debug.Assert(chapter != null, "chapter must not be null");
                            chapter.Name += $" [{performerMatch.Groups[1].Value.Trim('\r')}]";
                            break;
                        }

                        // 获取章节时间
                        if (timeMatch.Success)
                        {
                            var trackIndex = int.Parse(timeMatch.Groups["index"].Value);
                            switch (trackIndex)
                            {
                                case 0: // pre-gap of a track, just ignore it.
                                    break;

                                case 1: // beginning of a new track.
                                    Debug.Assert(chapter != null, "chapter must not be null");
                                    var minute = int.Parse(timeMatch.Groups["M"].Value);
                                    var second = int.Parse(timeMatch.Groups["S"].Value);
                                    var millisecond = (int)Math.Round(int.Parse(timeMatch.Groups["F"].Value) * (1000F / 75)); // 最后一项以帧(1s/75)而非以10毫秒为单位
                                    chapter.Time = new TimeSpan(0, 0, minute, second, millisecond);
                                    cue.Chapters.Add(chapter);
                                    nxState = NextState.NsNewTrack; // 当前章节点的必要信息已获得，继续寻找下一章节
                                    break;

                                default:
                                    nxState = NextState.NsError;
                                    break;
                            }
                        }
                        break;

                    case NextState.NsError:
                        throw new Exception("Unable to Parse this cue file");
                    case NextState.NsFin:
                        goto EXIT_1;
                    default:
                        nxState = NextState.NsError;
                        break;
                }
            }
        EXIT_1:
            if (cue.Chapters.Count < 1)
            {
                throw new Exception("Empty cue file");
            }
            cue.Chapters.Sort((c1, c2) => c1.Number.CompareTo(c2.Number)); // 确保无乱序
            cue.Duration = cue.Chapters.Last().Time;
            return cue;
        }

        /// <summary>
        /// 从含有CueSheet的的区块中读取cue
        /// </summary>
        /// <param name="buffer">含有CueSheet的区块</param>
        /// <param name="type">音频格式类型, 大小写不敏感</param>
        /// <returns>UTF-8编码的cue</returns>
        /// <exception cref="T:System.ArgumentException"><paramref name="type"/> 不为 flac 或 tak。</exception>
        private static string GetCueSheet(byte[] buffer, string type)
        {
            type = type.ToLower();
            if (type != "flac" && type != "tak")
            {
                throw new ArgumentException($"Invalid parameter: [{nameof(type)}], which must be 'flac' or 'tak'");
            }
            var length = buffer.Length;

            // 查找 Cuesheet 标记,自动机模型,大小写不敏感
            int state = 0, beginPos = 0;
            for (var i = 0; i < length; ++i)
            {
                if (buffer[i] >= 'A' && buffer[i] <= 'Z')
                    buffer[i] = (byte)(buffer[i] - 'A' + 'a');
                switch ((char)buffer[i])
                {
                    case 'c': state = 1; break; // C
                    case 'u': state = state == 1 ? 2 : 0; break; // Cu
                    case 'e':
                        switch (state)
                        {
                            case 2: state = 3; break; // Cue
                            case 5: state = 6; break; // Cueshe
                            case 6: state = 7; break; // Cueshee
                            default: state = 0; break;
                        }
                        break;

                    case 's': state = state == 3 ? 4 : 0; break; // Cues
                    case 'h': state = state == 4 ? 5 : 0; break; // Cuesh
                    case 't': state = state == 7 ? 8 : 0; break; // Cuesheet
                    default: state = 0; break;
                }
                if (state != 8) continue;
                beginPos = i + 2;
                break;
            }
            var controlCount = type == "flac" ? 3 : type == "tak" ? 6 : 0;
            var endPos = 0;
            state = 0;

            // 查找终止符 0D 0A ? 00 00 00 (连续 controlCount 个终止符以上) (flac为3, tak为6)
            for (var i = beginPos; i < length; ++i)
            {
                switch (buffer[i])
                {
                    case 0: state++; break;
                    default: state = 0; break;
                }
                if (state != controlCount) continue;
                endPos = i - controlCount; // 指向0D 0A后的第一个字符
                break;
            }
            if (beginPos == 0 || endPos <= 1) return string.Empty;

            if ((buffer[endPos - 2] == '\x0D') && (buffer[endPos - 1] == '\x0A'))
                endPos--;

            var cueLength = endPos - beginPos + 1;
            if (cueLength <= 10) return string.Empty;
            var cueSheet = Encoding.UTF8.GetString(buffer, beginPos, cueLength);

            // Debug.WriteLine(cueSheet);
            return cueSheet;
        }

        private const long SizeThreshold = 1 << 20;

        private static string GetCueFromTak(string takPath)
        {
            using (var fs = File.Open(takPath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length < SizeThreshold)
                    return string.Empty;
                var header = new byte[4];
                fs.Read(header, 0, 4);
                if (Encoding.ASCII.GetString(header, 0, 4) != "tBaK")
                    throw new InvalidDataException($"Except an tak but get an {Encoding.ASCII.GetString(header, 0, 4)}");
                fs.Seek(-20480, SeekOrigin.End);
                var buffer = new byte[20480];
                fs.Read(buffer, 0, 20480);
                return GetCueSheet(buffer, "tak");
            }
        }

        private static string GetCueFromFlac(string flacPath, Action<string> log = null)
        {
            try
            {
                FlacData.OnLog += log;
                var info = FlacData.GetMetadataFromFlac(flacPath);
                if (info.VorbisComment.ContainsKey("cuesheet"))
                    return info.VorbisComment["cuesheet"];
                return string.Empty;
            }
            finally
            {
                FlacData.OnLog -= log;
            }
        }

        public int Count { get; } = 1;

        public ChapterInfo this[int index]
        {
            get
            {
                if (index < 0 || index > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
                }
                return Chapter;
            }
        }

        public string ChapterType { get; } = "CUE";
    }
}