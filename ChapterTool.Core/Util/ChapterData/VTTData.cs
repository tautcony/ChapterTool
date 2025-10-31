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
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class VTTData
    {
        public static ChapterInfo GetChapterInfo(string text)
        {
            var info = new ChapterInfo { SourceType = "WebVTT", Tag = text, TagType = text.GetType() };
            text = text.Replace("\r", string.Empty);
            var nodes = Regex.Split(text, "\n\n");
            if (nodes.Length < 1 || nodes[0].IndexOf("WEBVTT", StringComparison.Ordinal) < 0)
            {
                throw new Exception($"ERROR: Empty or invalid file type");
            }
            var index = 0;
            nodes.Skip(1).ToList().ForEach(node =>
            {
                var lines = node.Split('\n');
                lines = lines.SkipWhile(line => line.IndexOf("-->", StringComparison.Ordinal) < 0).ToArray();
                if (lines.Length < 2)
                {
                    throw new Exception($"+Parser Failed: Happened at [{node}]");
                }
                var times = Regex.Split(lines[0], "-->").Select(TimeSpan.Parse).ToArray();
                info.Chapters.Add(new Chapter(lines[1], times[0], ++index));
            });
            return info;
        }
    }
}
