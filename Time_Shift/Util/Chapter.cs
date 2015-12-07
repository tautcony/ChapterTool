using System;

namespace ChapterTool.Util
{
    public class Chapter
    {
        public Chapter()
        {
            FramsInfo = string.Empty;
        }
        /// <summary>Chapter Number</summary>
        public int Number { get; set; }
        /// <summary>Chapter TimeStamp</summary>
        public TimeSpan Time { get; set; }
        /// <summary>Chapter Name</summary>
        public string Name { get; set; }
        /// <summary>Fram Count</summary>
        public string FramsInfo { get; set; }
        public override string ToString() => $"{Name} - {ConvertMethod.Time2String(Time)}";
    }
}