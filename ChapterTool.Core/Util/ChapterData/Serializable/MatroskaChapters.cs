namespace ChapterTool.Util.ChapterData.Serializable
{
    using System;
    using System.Xml.Serialization;

    [Serializable]
    public class Chapters
    {
        [XmlElement("EditionEntry")]
        public EditionEntry[] EditionEntry { get; set; }
    }

    [Serializable]
    public class EditionEntry
    {
        public string EditionUID { get; set; }

        public string EditionFlagHidden { get; set; }

        public string EditionManaged { get; set; }

        public string EditionFlagDefault { get; set; }

        [XmlElement("ChapterAtom")]
        public ChapterAtom[] ChapterAtom { get; set; }
    }

    [Serializable]
    public class ChapterAtom
    {
        public string ChapterTimeStart { get; set; }

        public string ChapterTimeEnd { get; set; }

        public string ChapterUID { get; set; }

        public string ChapterSegmentUID { get; set; }

        public string ChapterSegmentEditionUID { get; set; }

        public string ChapterPhysicalEquiv { get; set; }

        public ChapterTrack ChapterTrack { get; set; }

        public string ChapterFlagHidden { get; set; }

        public string ChapterFlagEnabled { get; set; }

        public ChapterDisplay ChapterDisplay { get; set; }

        [XmlElement("ChapterProcess")]
        public ChapterProcess[] ChapterProcess { get; set; }

        [XmlElement("ChapterAtom")]
        public ChapterAtom[] SubChapterAtom { get; set; }
    }

    [Serializable]
    public class ChapterTrack
    {
        public string ChapterTrackNumber { get; set; }
    }

    [Serializable]
    public class ChapterDisplay
    {
        public string ChapterString { get; set; }

        public string ChapterLanguage { get; set; }

        public string ChapterCountry { get; set; }
    }

    [Serializable]
    public class ChapterProcess
    {
        public string ChapterProcessCodecID { get; set; }

        public string ChapterProcessPrivate { get; set; }

        [XmlElement("ChapterProcessCommand")]
        public ChapterProcessCommand[] ChapterProcessCommand { get; set; }
    }

    [Serializable]
    public class ChapterProcessCommand
    {
        public string ChapterProcessTime { get; set; }

        public string ChapterProcessData { get; set; }
    }
}