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

    public class Chapter
    {
        /// <summary>Chapter Number</summary>
        public int Number { get; set; }

        /// <summary>Chapter TimeStamp</summary>
        public TimeSpan Time { get; set; }

        /// <summary>Chapter Name</summary>
        public string Name { get; set; }

        /// <summary>Frame Count</summary>
        public string FramesInfo { get; set; } = string.Empty;

        public override string ToString() => $"{Name} - {Time.Time2String()}";

        public Chapter()
        {
        }

        public Chapter(string name, TimeSpan time, int number)
        {
            Number = number;
            Time = time;
            Name = name;
        }

        public int IsAccuracy(decimal fps, decimal accuracy, Expression expr = null)
        {
            var frames = (decimal)Time.TotalMilliseconds * fps / 1000M;
            if (expr != null) frames = expr.Eval(Time.TotalSeconds, fps) * fps;
            var rounded = Math.Round(frames, MidpointRounding.AwayFromZero);
            return Math.Abs(frames - rounded) < accuracy ? 1 : 0;
        }
    }
}
