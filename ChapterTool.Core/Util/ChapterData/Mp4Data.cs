// ****************************************************************************
//
// Copyright (C) 2014-2015 TautCony (TautCony@vcb-s.com)
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
    using ChapterTool.Util;
    using Knuckleball;

    public class Mp4Data
    {
        public ChapterInfo Chapter { get; private set; }

        public Mp4Data(string path)
        {
            var file = MP4File.Open(path);
            if (file.Chapters == null) return;
            Chapter = new ChapterInfo();
            var index = 0;
            foreach (var chapterClip in file.Chapters)
            {
                Chapter.Chapters.Add(new Util.Chapter(chapterClip.Title, Chapter.Duration, ++index));
                Chapter.Duration += chapterClip.Duration;
            }
        }
    }
}