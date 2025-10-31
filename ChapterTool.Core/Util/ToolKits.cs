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
    using System.Text;
    using System.Text.RegularExpressions;

    public static class ToolKits
    {
        /// <summary>
        /// 将TimeSpan对象转换为 hh:mm:ss.sss 形式的字符串
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static string Time2String(this TimeSpan time)
        {
            var millisecond = (int)Math.Round((time.TotalSeconds - Math.Floor(time.TotalSeconds)) * 1000);
            return $"{time.Hours:D2}:{time.Minutes:D2}:" +
                   (millisecond == 1000 ?
                       $"{time.Seconds + 1:D2}.000" :
                       $"{time.Seconds:D2}.{millisecond:D3}"
                   );
        }

        /// <summary>
        /// 将给定的章节点时间以平移、修正信息修正后转换为 hh:mm:ss.sss 形式的字符串
        /// </summary>
        /// <param name="item">章节点</param>
        /// <param name="info">章节信息</param>
        /// <returns></returns>
        public static string Time2String(this Chapter item, ChapterInfo info)
        {
            return new TimeSpan((long)(info.Expr.Eval(item.Time.TotalSeconds, info.FramesPerSecond) * TimeSpan.TicksPerSecond)).Time2String();
        }

        public static readonly Regex RTimeFormat = new Regex(@"(?<Hour>\d+)\s*:\s*(?<Minute>\d+)\s*:\s*(?<Second>\d+)\s*[\.,]\s*(?<Millisecond>\d{3})", RegexOptions.Compiled);

        /// <summary>
        /// 将符合 hh:mm:ss.sss 形式的字符串转换为TimeSpan对象
        /// </summary>
        /// <param name="input">时间字符串</param>
        /// <returns></returns>
        public static TimeSpan ToTimeSpan(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return TimeSpan.Zero;
            var timeMatch = RTimeFormat.Match(input);
            if (!timeMatch.Success) return TimeSpan.Zero;
            var hour = int.Parse(timeMatch.Groups["Hour"].Value);
            var minute = int.Parse(timeMatch.Groups["Minute"].Value);
            var second = int.Parse(timeMatch.Groups["Second"].Value);
            var millisecond = int.Parse(timeMatch.Groups["Millisecond"].Value);
            return new TimeSpan(0, hour, minute, second, millisecond);
        }

        public static string ToCueTimeStamp(this TimeSpan input)
        {
            var frames = (int)Math.Round(input.Milliseconds * 75 / 1000F);
            if (frames > 99) frames = 99;
            return $"{(input.Hours * 60) + input.Minutes:D2}:{input.Seconds:D2}:{frames:D2}";
        }

        /// <summary>
        /// Detects BOM and converts byte array to UTF string
        /// </summary>
        /// <param name="buffer">Byte array to convert</param>
        /// <returns>UTF string</returns>
        public static string? GetUTFString(this byte[] buffer)
        {
            if (buffer == null) return null;
            if (buffer.Length <= 3) return Encoding.UTF8.GetString(buffer);
            if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return new UTF8Encoding(false).GetString(buffer, 3, buffer.Length - 3);
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode.GetString(buffer);
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(buffer);
            return Encoding.UTF8.GetString(buffer);
        }
    }
}
