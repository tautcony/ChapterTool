// ****************************************************************************
//
// Copyright (C) 2014-2017 TautCony (TautCony@vcb-s.com)
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
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class FlacInfo
    {
        public long RawLength { get; set; }

        public long TrueLength { get; set; }

        public double CompressRate => TrueLength / (double)RawLength;

        public bool HasCover { get; set; }

        public string Encoder { get; set; }

        public Dictionary<string, string> VorbisComment { get; }

        public FlacInfo()
        {
            VorbisComment = new Dictionary<string, string>();
        }
    }

    // https://xiph.org/flac/format.html
    public static class FlacData
    {
        private const long SizeThreshold = 1 << 20;

        public static event Action<string> OnLog;

        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Reviewed.")]
        private enum BlockType
        {
            STREAMINFO = 0x00,
            PADDING,
            APPLICATION,
            SEEKTABLE,
            VORBIS_COMMENT,
            CUESHEET,
            PICTURE,
        }

        public static FlacInfo GetMetadataFromFlac(string flacPath)
        {
            using (var fs = File.Open(flacPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Length < SizeThreshold) return new FlacInfo();
                var info = new FlacInfo { TrueLength = fs.Length };
                var header = Encoding.ASCII.GetString(fs.ReadBytes(4), 0, 4);
                if (header != "fLaC")
                    throw new InvalidDataException($"Except an flac but get an {header}");

                // METADATA_BLOCK_HEADER
                // 1-bit Last-metadata-block flag
                // 7-bit BLOCK_TYPE
                // 24-bit Length
                while (fs.Position < fs.Length)
                {
                    var blockHeader = fs.BEInt32();
                    var lastMetadataBlock = blockHeader >> 31 == 0x1;
                    var blockType = (BlockType)((blockHeader >> 24) & 0x7f);
                    var length = blockHeader & 0xffffff;
                    info.TrueLength -= length;
                    OnLog?.Invoke($"|+{blockType} with Length: {length}");
                    switch (blockType)
                    {
                        case BlockType.STREAMINFO:
                            Debug.Assert(length == 34, "Stream info block length must be 34");
                            ParseStreamInfo(fs, ref info);
                            break;
                        case BlockType.VORBIS_COMMENT:
                            ParseVorbisComment(fs, ref info);
                            break;
                        case BlockType.PICTURE:
                            ParsePicture(fs, ref info);
                            break;
                        case BlockType.PADDING:
                        case BlockType.APPLICATION:
                        case BlockType.SEEKTABLE:
                        case BlockType.CUESHEET:
                            fs.Seek(length, SeekOrigin.Current);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Invalid BLOCK_TYPE: 0x{blockType:X2}");
                    }
                    if (lastMetadataBlock) break;
                }
                return info;
            }
        }

        private static void ParseStreamInfo(Stream fs, ref FlacInfo info)
        {
            var minBlockSize = fs.BEInt16();
            var maxBlockSize = fs.BEInt16();
            var minFrameSize = fs.BEInt24();
            var maxFrameSize = fs.BEInt24();
            var buffer = fs.ReadBytes(8);
            var br = new BitReader(buffer);
            var sampleRate = br.GetBits(20);
            var channelCount = br.GetBits(3) + 1;
            var bitPerSample = br.GetBits(5) + 1;
            var totalSample = br.GetBits(36);
            var md5 = fs.ReadBytes(16);
            info.RawLength = channelCount * bitPerSample / 8 * totalSample;
            OnLog?.Invoke($" | minimum block size: {minBlockSize}, maximum block size: {maxBlockSize}");
            OnLog?.Invoke($" | minimum frame size: {minFrameSize}, maximum frame size: {maxFrameSize}");
            OnLog?.Invoke($" | Sample rate: {sampleRate}Hz, bits per sample: {bitPerSample}-bit");
            OnLog?.Invoke($" | Channel count: {channelCount}");
            var md5String = md5.Aggregate(string.Empty, (current, item) => current + $"{item:X2}");
            OnLog?.Invoke($" | MD5: {md5String}");
        }

        private static void ParseVorbisComment(Stream fs, ref FlacInfo info)
        {
            // only here in flac use little-endian
            var vendorLength = (int)fs.LEInt32();
            var vendorRawStringData = fs.ReadBytes(vendorLength);
            var vendor = Encoding.UTF8.GetString(vendorRawStringData, 0, vendorLength);
            info.Encoder = vendor;
            OnLog?.Invoke($" | Vendor: {vendor}");
            var userCommentListLength = fs.LEInt32();
            for (var i = 0; i < userCommentListLength; ++i)
            {
                var commentLength = (int)fs.LEInt32();
                var commentRawStringData = fs.ReadBytes(commentLength);
                var comment = Encoding.UTF8.GetString(commentRawStringData, 0, commentLength);
                var splitterIndex = comment.IndexOf('=');
                var key = comment.Substring(0, splitterIndex);
                var value = comment.Substring(splitterIndex + 1, comment.Length - 1 - splitterIndex);
                info.VorbisComment[key] = value;
                var summary = value.Length > 25 ? value.Substring(0, 25) + "..." : value;
                OnLog?.Invoke($" | [{key}] = '{summary.Replace('\n', ' ')}'");
            }
        }

        private static readonly string[] PictureTypeName =
        {
            "Other", "32x32 pixels 'file icon'", "Other file icon",
            "Cover (front)", "Cover (back)", "Leaflet page",
            "Media", "Lead artist/lead performer/soloist", "Artist/performer",
            "Conductor", "Band/Orchestra", "Composer",
            "Lyricist/text writer", "Recording Location", "During recording",
            "During performance", "Movie/video screen capture", "A bright coloured fish",
            "Illustration", "Band/artist logotype", "Publisher/Studio logotype",
            "Reserved",
        };

        private static void ParsePicture(Stream fs, ref FlacInfo info)
        {
            var pictureType = fs.BEInt32();
            var mimeStringLength = (int)fs.BEInt32();
            var mimeType = Encoding.ASCII.GetString(fs.ReadBytes(mimeStringLength), 0, mimeStringLength);
            var descriptionLength = (int)fs.BEInt32();
            var description = Encoding.UTF8.GetString(fs.ReadBytes(descriptionLength), 0, descriptionLength);
            var pictureWidth = fs.BEInt32();
            var pictureHeight = fs.BEInt32();
            var colorDepth = fs.BEInt32();
            var indexedColorCount = fs.BEInt32();
            var pictureDataLength = fs.BEInt32();
            fs.Seek(pictureDataLength, SeekOrigin.Current);
            info.TrueLength -= pictureDataLength;
            info.HasCover = true;
            if (pictureType > 20) pictureType = 21;
            OnLog?.Invoke($" | picture type: {PictureTypeName[pictureType]}");
            OnLog?.Invoke($" | picture format type: {mimeType}");
            if (descriptionLength > 0)
                OnLog?.Invoke($" | description: {description}");
            OnLog?.Invoke($" | attribute: {pictureWidth}px*{pictureHeight}px@{colorDepth}-bit");
            if (indexedColorCount != 0)
                OnLog?.Invoke($" | indexed-color color: {indexedColorCount}");
        }
    }
}
