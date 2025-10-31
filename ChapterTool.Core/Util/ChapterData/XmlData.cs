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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Serialization;
    using ChapterTool.Util.ChapterData.Serializable;

    public static class XmlData
    {
        public static IEnumerable<ChapterInfo> ParseXml(XmlDocument doc)
        {
            var root = doc.DocumentElement;
            if (root == null)
            {
                throw new ArgumentException("Empty Xml file");
            }
            if (root.Name != "Chapters")
            {
                throw new Exception($"Invalid Xml file.\nroot node Name: {root.Name}");
            }

            // Get Entrance for each chapter
            foreach (XmlNode editionEntry in root.ChildNodes)
            {
                if (editionEntry.NodeType == XmlNodeType.Comment) continue;
                if (editionEntry.Name != "EditionEntry")
                {
                    throw new Exception($"Invalid Xml file.\nEntry Name: {editionEntry.Name}");
                }
                var buff = new ChapterInfo { SourceType = "XML", Tag = doc, TagType = doc.GetType() };
                var index = 0;

                // Get all the child nodes in current chapter
                foreach (XmlNode editionEntryChildNode in ((XmlElement)editionEntry).ChildNodes)
                {
                    if (editionEntryChildNode.Name != "ChapterAtom") continue;
                    buff.Chapters.AddRange(ParseChapterAtom(editionEntryChildNode, ++index));
                }

                // remove redundancy chapter node.
                for (var i = 0; i < buff.Chapters.Count - 1; i++)
                {
                    if (buff.Chapters[i].Time == buff.Chapters[i + 1].Time)
                    {
                        buff.Chapters.Remove(buff.Chapters[i--]);
                    }
                }

                // buff.Chapters = buff.Chapters.Distinct().ToList();
                yield return buff;
            }
        }

        private static IEnumerable<Chapter> ParseChapterAtom(XmlNode chapterAtom, int index)
        {
            var startChapter = new Chapter { Number = index };
            var endChapter = new Chapter { Number = index };
            var innerChapterAtom = new List<Chapter>();

            // Get detail info for current chapter node
            foreach (XmlNode chapterAtomChildNode in ((XmlElement)chapterAtom).ChildNodes)
            {
                switch (chapterAtomChildNode.Name)
                {
                    case "ChapterTimeStart":
                        startChapter.Time = ToolKits.RTimeFormat.Match(chapterAtomChildNode.InnerText).Value.ToTimeSpan();
                        break;
                    case "ChapterTimeEnd":
                        endChapter.Time = ToolKits.RTimeFormat.Match(chapterAtomChildNode.InnerText).Value.ToTimeSpan();
                        break;
                    case "ChapterDisplay":
                        try
                        {
                            startChapter.Name = ((XmlElement)chapterAtomChildNode).ChildNodes.Cast<XmlNode>().First(node => node.Name == "ChapterString").InnerText;
                        }
                        catch
                        {
                            startChapter.Name = string.Empty;
                        }
                        endChapter.Name = startChapter.Name;
                        break;
                    case "ChapterAtom": // Handling sub chapters.
                        innerChapterAtom.AddRange(ParseChapterAtom(chapterAtomChildNode, index));
                        break;
                }
            }

            // make sure the sub chapters outputted in correct order.
            yield return startChapter;

            foreach (var chapter in innerChapterAtom)
            {
                yield return chapter;
            }

            if (endChapter.Time.TotalSeconds > startChapter.Time.TotalSeconds)
            {
                yield return endChapter;
            }
        }

        public static Chapters Deserializer(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                return (Chapters)new XmlSerializer(typeof(Chapters)).Deserialize(reader);
            }
        }

        public static IEnumerable<ChapterInfo> ToChapterInfo(this Chapters chapters)
        {
            var index = 0;
            foreach (var entry in chapters.EditionEntry)
            {
                var info = new ChapterInfo();
                foreach (var atom in entry.ChapterAtom)
                {
                    info.Chapters.AddRange(ToChapter(atom, ++index));
                }
                yield return info;
            }
        }

        private static IEnumerable<Chapter> ToChapter(ChapterAtom atom, int index)
        {
            if (atom.ChapterTimeStart != null)
            {
                var startChapter = new Chapter
                {
                    Number = index,
                    Time = ToolKits.RTimeFormat.Match(atom.ChapterTimeStart).Value.ToTimeSpan(),
                    Name = atom.ChapterDisplay.ChapterString ?? string.Empty,
                };
                yield return startChapter;
            }
            if (atom.SubChapterAtom != null)
            {
                foreach (var chapterAtom in atom.SubChapterAtom)
                {
                    foreach (var chapter in ToChapter(chapterAtom, index))
                    {
                        yield return chapter;
                    }
                }
            }

            if (atom.ChapterTimeEnd != null)
            {
                var endChapter = new Chapter
                {
                    Number = index,
                    Time = ToolKits.RTimeFormat.Match(atom.ChapterTimeEnd).Value.ToTimeSpan(),
                    Name = atom.ChapterDisplay.ChapterString ?? string.Empty,
                };
                yield return endChapter;
            }
        }
    }
}