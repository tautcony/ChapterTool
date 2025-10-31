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

namespace ChapterTool.Util
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class ChapterInfo
    {
        /// <summary>
        /// The title of Chapter
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Corresponding Video file
        /// </summary>
        public string SourceName { get; set; } = string.Empty;

        public string SourceIndex { get; set; } = string.Empty;

        public string SourceType { get; set; } = string.Empty;

        public decimal FramesPerSecond { get; set; }

        public TimeSpan Duration { get; set; }

        public List<Chapter> Chapters { get; set; } = new List<Chapter>();

        public Expression Expr { get; set; } = Expression.Empty;

        public Type? TagType { get; set; }

        private object? _tag;
        public object? Tag
        {
            get => _tag;
            set
            {
                if (value == null)
                    return;
                _tag = value;
            }
        }

        public override string ToString() => $"{Title} - {SourceType} - {Duration.Time2String()} - [{Chapters.Count} Chapters]";

        /// <summary>
        /// 将分开多段的 ifo 章节合并为一个章节
        /// </summary>
        /// <param name="source">解析获得的分段章节</param>
        /// <param name="type">章节源格式</param>
        /// <returns></returns>
        public static ChapterInfo CombineChapter(List<ChapterInfo> source, string type = "DVD")
        {
            var fullChapter = new ChapterInfo
            {
                Title = "FULL Chapter",
                SourceType = type,
                FramesPerSecond = source.First().FramesPerSecond,
            };
            var duration = TimeSpan.Zero;
            var name = new ChapterName();
            foreach (var chapterClip in source)
            {
                foreach (var item in chapterClip.Chapters)
                {
                    fullChapter.Chapters.Add(new Chapter
                    {
                        Time = duration + item.Time,
                        Number = name.Index,
                        Name = name.Get(),
                    });
                }
                duration += chapterClip.Duration; // 每次加上当前段的总时长作为下一段位移的基准
            }
            fullChapter.Duration = duration;
            return fullChapter;
        }

        private string Time2String(Chapter item)
        {
            return item.Time2String(this);
        }

        public void ChangeFps(decimal fps)
        {
            for (var i = 0; i < Chapters.Count; i++)
            {
                var c = Chapters[i];
                var frames = (decimal)c.Time.TotalSeconds * FramesPerSecond;
                Chapters[i] = new Chapter
                {
                    Name = c.Name,
                    Time = new TimeSpan((long)Math.Round(frames / fps * TimeSpan.TicksPerSecond)),
                };
            }

            var totalFrames = (decimal)Duration.TotalSeconds * FramesPerSecond;
            Duration = new TimeSpan((long)Math.Round(totalFrames / fps * TimeSpan.TicksPerSecond));
            FramesPerSecond = fps;
        }

        #region UpdateInfo

        /// <summary>
        /// 以新的时间基准更新剩余章节
        /// </summary>
        /// <param name="shift">剩余章节的首个章节点的时间</param>
        public void UpdateInfo(TimeSpan shift)
        {
            Chapters.ForEach(item => item.Time -= shift);
        }

        /// <summary>
        /// 根据输入的数值向后位移章节序号
        /// </summary>
        /// <param name="shift">位移量</param>
        public void UpdateInfo(int shift)
        {
            var index = 0;
            Chapters.ForEach(item => item.Number = ++index + shift);
        }

        /// <summary>
        /// 根据给定的章节名模板更新章节
        /// </summary>
        /// <param name="chapterNameTemplate"></param>
        public void UpdateInfo(string chapterNameTemplate)
        {
            if (string.IsNullOrWhiteSpace(chapterNameTemplate)) return;
            using (var cn = chapterNameTemplate.Trim(' ', '\r', '\n').Split('\n').ToList().GetEnumerator()) // 移除首尾多余空行
            {
                Chapters.ForEach(item => item.Name = cn.MoveNext() ? cn.Current : item.Name.Trim('\r')); // 确保无多余换行符
            }
        }

        #endregion

        /// <summary>
        /// 生成 OGM 样式章节
        /// </summary>
        /// <param name="autoGenName">不使用章节名</param>
        /// <returns></returns>
        public string GetText(bool autoGenName)
        {
            var lines = new StringBuilder();
            var name = ChapterName.GetChapterName();
            foreach (var item in Chapters.Where(c => c.Time != TimeSpan.MinValue))
            {
                lines.Append($"CHAPTER{item.Number:D2}={Time2String(item)}{Environment.NewLine}");
                lines.Append($"CHAPTER{item.Number:D2}NAME={(autoGenName ? name() : item.Name)}");
                lines.Append(Environment.NewLine);
            }
            return lines.ToString();
        }

        public string[] GetQpfile() => Chapters.Where(c => c.Time != TimeSpan.MinValue).Select(c => c.FramesInfo.TrimEnd('K', '*') + "I").ToArray();

        public static void Chapter2Qpfile(string ipath, string opath, double fps, string tcfile = "")
        {
            var ilines = File.ReadAllLines(ipath);
            string[]? tclines = null;
            var olines = new List<string>();
            int tcindex = 0, tcframe = 0;
            if (!string.IsNullOrEmpty(tcfile))
            {
                tclines = File.ReadAllLines(tcfile);
                tcindex = 0;
                foreach (var tcline in tclines)
                {
                    if (char.IsDigit(tcline.Trim().First()))
                    {
                        tcframe = 0;
                        break;
                    }
                    ++tcindex;
                    if (tcindex >= tclines.Length)
                        throw new IndexOutOfRangeException("TC index out of range! TC file and Chapter file mismatch?");
                }
            }

            foreach (var line in ilines.Select(i => i.Trim().ToLower()))
            {
                if (!line.StartsWith("chapter")) continue;
                var segments = line.Substring(7).Split('=');
                if (segments.Length < 2) continue;
                if (!segments[0].All(char.IsDigit)) continue;
                if (int.TryParse(segments[0], out _)) continue;
                var times = segments[1].Split(':');
                if (times.Length > 3) continue;
                var time = 0.0;
                try
                {
                    time = times.Aggregate(time, (current, t) => (current * 60) + double.Parse(t));
                }
                catch (Exception)
                {
                    continue;
                }
                int frame;
                if (string.IsNullOrEmpty(tcfile))
                {
                    frame = (int)(time + (0.001 * fps));
                }
                else
                {
                    var timeLower = (time - 0.0005) * 1000;
                    while (true)
                    {
                        if (tclines != null && double.Parse(tclines[tcindex]) >= timeLower) break;
                        while (true)
                        {
                            ++tcindex;
                            if (tclines != null && tcindex >= tclines.Length)
                            {
                                throw new IndexOutOfRangeException(
                                    "TC index out of range! TC file and Chapter file mismatch?");
                            }

                            if (tclines != null && char.IsDigit(tclines[tcindex].Trim().First())) break;
                        }
                        ++tcframe;
                    }
                    frame = tcframe;
                }
                olines.Add($"{frame} I");
            }
            File.WriteAllLines(opath, olines);
        }

        public string[] GetCelltimes() => Chapters.Where(c => c.Time != TimeSpan.MinValue).Select(c => ((long)Math.Round((decimal)c.Time.TotalSeconds * FramesPerSecond)).ToString()).ToArray();

        public string GetTsmuxerMeta()
        {
            string text = $"--custom-{Environment.NewLine}chapters=";
            text = Chapters.Where(c => c.Time != TimeSpan.MinValue).Aggregate(text, (current, chapter) => current + Time2String(chapter) + ";");
            text = text.Substring(0, text.Length - 1);
            return text;
        }

        public string[] GetTimecodes() => Chapters.Where(c => c.Time != TimeSpan.MinValue).Select(Time2String).ToArray();

        public void SaveXml(string filename, string lang, bool autoGenName)
        {
            if (string.IsNullOrWhiteSpace(lang)) lang = "und";
            var rndb = new Random();
            var xmlchap = new XmlTextWriter(filename, Encoding.UTF8) { Formatting = Formatting.Indented };
            xmlchap.WriteStartDocument();
            xmlchap.WriteComment("<!DOCTYPE Tags SYSTEM \"matroskatags.dtd\">");
            xmlchap.WriteStartElement("Chapters");
            xmlchap.WriteStartElement("EditionEntry");
            xmlchap.WriteElementString("EditionFlagHidden", "0");
            xmlchap.WriteElementString("EditionFlagDefault", "0");
            xmlchap.WriteElementString("EditionUID", Convert.ToString(rndb.Next(1, int.MaxValue)));
            var name = ChapterName.GetChapterName();
            foreach (var item in Chapters.Where(c => c.Time != TimeSpan.MinValue))
            {
                xmlchap.WriteStartElement("ChapterAtom");
                xmlchap.WriteStartElement("ChapterDisplay");
                xmlchap.WriteElementString("ChapterString", autoGenName ? name() : item.Name);
                xmlchap.WriteElementString("ChapterLanguage", lang);
                xmlchap.WriteEndElement();
                xmlchap.WriteElementString("ChapterUID", Convert.ToString(rndb.Next(1, int.MaxValue)));
                xmlchap.WriteElementString("ChapterTimeStart", Time2String(item) + "000");
                xmlchap.WriteElementString("ChapterFlagHidden", "0");
                xmlchap.WriteElementString("ChapterFlagEnabled", "1");
                xmlchap.WriteEndElement();
            }
            xmlchap.WriteEndElement();
            xmlchap.WriteEndElement();
            xmlchap.Flush();
            xmlchap.Close();
        }

        public StringBuilder GetCue(string sourceFileName, bool autoGenName)
        {
            var cueBuilder = new StringBuilder();
            cueBuilder.AppendLine("REM Generate By ChapterTool");
            cueBuilder.AppendLine($"TITLE \"{Title}\"");

            cueBuilder.AppendLine($"FILE \"{sourceFileName}\" WAVE");
            var index = 0;
            var name = ChapterName.GetChapterName();
            foreach (var chapter in Chapters.Where(c => c.Time != TimeSpan.MinValue))
            {
                cueBuilder.AppendLine($"  TRACK {++index:D2} AUDIO");
                cueBuilder.AppendLine($"    TITLE \"{(autoGenName ? name() : chapter.Name)}\"");
                cueBuilder.AppendLine($"    INDEX 01 {chapter.Time.ToCueTimeStamp()}");
            }
            return cueBuilder;
        }

        class ChapterItemJson
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("time")]
            public double Time { get; set; }
        }

        class ChapterJson
        {
            [JsonPropertyName("sourceName")]
            public string? SourceName { get; set; }

            [JsonPropertyName("chapter")]
            public List<ChapterItemJson> Chapter { get; set; } = new();
        }

        public StringBuilder GetJson(bool autoGenName)
        {
            var jsonObject = new ChapterJson
            {
                SourceName = SourceType == "MPLS" ? $"{SourceName}.m2ts" : null,
                Chapter = new List<ChapterItemJson>(),
            };

            var baseTime = TimeSpan.Zero;
            Chapter? prevChapter = null;
            var name = ChapterName.GetChapterName();
            foreach (var chapter in Chapters)
            {
                if (chapter.Time == TimeSpan.MinValue && prevChapter != null)
                {
                    baseTime = prevChapter.Time; // update base time
                    name = ChapterName.GetChapterName();
                    var initChapterName = autoGenName ? name() : prevChapter.Name;
                    jsonObject.Chapter.Add(new ChapterItemJson
                    {
                        Name = initChapterName,
                        Time = 0,
                    });
                    continue;
                }
                var time = chapter.Time - baseTime;
                var chapterName = (autoGenName ? name() : chapter.Name);
                jsonObject.Chapter.Add(new ChapterItemJson
                {
                    Name = chapterName,
                    Time = time.TotalSeconds,
                });
                prevChapter = chapter;
            }
            var ret = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
            return new StringBuilder(ret);
        }
    }
}
