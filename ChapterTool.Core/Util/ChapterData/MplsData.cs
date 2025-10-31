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
    using System.IO;
    using System.Linq;
    using System.Text;

    // https://github.com/lerks/BluRay/wiki/MPLS
    public class MplsData
    {
        private readonly MplsHeader _mplsHeader;
        private readonly PlayList _playList;
        private readonly PlayListMark _playListMark;
        private readonly ExtensionData _extensionData;

        public string Version => _mplsHeader.TypeIndicator.ToString();

        public PlayItem[] PlayItems => _playList.PlayItems;

        public SubPath[] SubPaths => _playList.SubPaths;

        public Mark[] Marks => _playListMark.Marks;

        public static readonly decimal[] FrameRate = { 0M, 24000M / 1001, 24M, 25M, 30000M / 1001, 0M, 50M, 60000M / 1001 };

        public static event Action<string> OnLog;

        public MplsData(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                _mplsHeader = new MplsHeader(stream);

                stream.Seek(_mplsHeader.PlayListStartAddress, SeekOrigin.Begin);
                _playList = new PlayList(stream);

                stream.Seek(_mplsHeader.PlayListMarkStartAddress, SeekOrigin.Begin);
                _playListMark = new PlayListMark(stream);

                if (_mplsHeader.ExtensionDataStartAddress != 0)
                {
                    stream.Seek(_mplsHeader.ExtensionDataStartAddress, SeekOrigin.Begin);
                    _extensionData = new ExtensionData(stream);
                }
            }
            StreamAttribution.OnLog += OnLog;
            foreach (var item in PlayItems)
            {
                foreach (var s in item.STNTable.StreamEntries)
                {
                    OnLog?.Invoke($"+{s.GetType()}");
                    StreamAttribution.LogStreamAttributes(s, item.ClipName);
                }
            }
            StreamAttribution.OnLog -= OnLog;
        }

        public MplsGroup GetChapters()
        {
            var ret = new MplsGroup();
            for (var i = 0; i < PlayItems.Length; ++i)
            {
                var playItem = PlayItems[i];
                var attr = playItem.STNTable.StreamEntries.First(item => item is PrimaryVideoStreamEntry);
                var info = new ChapterInfo
                {
                    SourceType = "MPLS",
                    SourceName = PlayItems[i].FullName,
                    Duration = Pts2Time(playItem.TimeInfo.DeltaTime),
                    FramesPerSecond = FrameRate[attr.StreamAttributes.FrameRate],
                };

                var index = i;
                Func<Mark, bool> filter = item => item.MarkType == 0x01 && item.RefToPlayItemID == index;
                if (!Marks.Any(filter))
                {
                    OnLog?.Invoke($"PlayItem without any marks, index: {index}");
                    info.Chapters = new List<Chapter> { new Chapter { Time = Pts2Time(0), Number = 1, Name = "Chapter 1" } };
                    ret.Add(info);
                    continue;
                }
                var offset = Marks.First(filter).MarkTimeStamp;
                if (playItem.TimeInfo.INTime < offset)
                {
                    OnLog?.Invoke($"{{PlayItems[{i}]: first time stamp => {offset}, in time => {playItem.TimeInfo.INTime}}}");
                    offset = playItem.TimeInfo.INTime;
                }
                var name = new ChapterName();
                info.Chapters = Marks.Where(filter).Select(mark => new Chapter
                {
                    Time = Pts2Time(mark.MarkTimeStamp - offset),
                    Number = name.Index,
                    Name = name.Get(),
                }).ToList();
                ret.Add(info);
            }
            return ret;
        }

        public static TimeSpan Pts2Time(uint pts)
        {
            var total = pts / 45000M;
            var secondPart = Math.Floor(total);
            var millisecondPart = Math.Round((total - secondPart) * 1000M, MidpointRounding.AwayFromZero);
            return new TimeSpan(0, 0, 0, (int)secondPart, (int)millisecondPart);
        }
    }

    internal struct TypeIndicator
    {
        public string Header; // 4
        public string Version; // 4

        public override string ToString() => Header + Version;
    }

    internal class MplsHeader
    {
        public TypeIndicator TypeIndicator;
        public uint PlayListStartAddress;
        public uint PlayListMarkStartAddress;
        public uint ExtensionDataStartAddress;
        public AppInfoPlayList AppInfoPlayList;

        // 20bytes reserved
        public MplsHeader(Stream stream)
        {
            TypeIndicator.Header = Encoding.ASCII.GetString(stream.ReadBytes(4));
            if (TypeIndicator.Header != "MPLS")
                throw new Exception($"Invalid file type: {TypeIndicator.Header}");
            TypeIndicator.Version = Encoding.ASCII.GetString(stream.ReadBytes(4));
            if (TypeIndicator.Version != "0100" &&
                TypeIndicator.Version != "0200" &&
                TypeIndicator.Version != "0300")
                throw new Exception($"Invalid mpls version: {TypeIndicator.Version}");
            PlayListStartAddress = stream.BEInt32();
            PlayListMarkStartAddress = stream.BEInt32();
            ExtensionDataStartAddress = stream.BEInt32();
            stream.Skip(20);
            AppInfoPlayList = new AppInfoPlayList(stream);
        }
    }

    internal class AppInfoPlayList
    {
        public uint Length;

        // 1byte reserved
        public byte PlaybackType;

        // if PlaybackType == 0x02 || PlaybackType == 0x03:
        public ushort PlaybackCount;
        public UOMaskTable UOMaskTable;

        public ushort FlagField { private get; set; }

        public bool RandomAccessFlag => ((FlagField >> 15) & 1) == 1;

        public bool AudioMixFlag => ((FlagField >> 14) & 1) == 1;

        public bool LosslessBypassFlag => ((FlagField >> 13) & 1) == 1;

        public AppInfoPlayList(Stream stream)
        {
            Length = stream.BEInt32();
            var position = stream.Position;
            stream.Skip(1);
            PlaybackType = (byte)stream.ReadByte();
            PlaybackCount = (ushort)stream.BEInt16();
            UOMaskTable = new UOMaskTable(stream);
            FlagField = (ushort)stream.BEInt16();
            stream.Skip(Length - (stream.Position - position));
        }
    }

    public class UOMaskTable
    {
        private readonly byte[] _flagField;
        public bool MenuCall;
        public bool TitleSearch;
        public bool ChapterSearch;
        public bool TimeSearch;
        public bool SkipToNextPoint;
        public bool SkipToPrevPoint;
        public bool Stop;
        public bool PauseOn;
        public bool StillOff;
        public bool ForwardPlay;
        public bool BackwardPlay;
        public bool Resume;
        public bool MoveUpSelectedButton;
        public bool MoveDownSelectedButton;
        public bool MoveLeftSelectedButton;
        public bool MoveRightSelectedButton;
        public bool SelectButton;
        public bool ActivateButton;
        public bool SelectAndActivateButton;
        public bool PrimaryAudioStreamNumberChange;
        public bool AngleNumberChange;
        public bool PopupOn;
        public bool PopupOff;
        public bool PGEnableDisable;
        public bool PGStreamNumberChange;
        public bool SecondaryVideoEnableDisable;
        public bool SecondaryVideoStreamNumberChange;
        public bool SecondaryAudioEnableDisable;
        public bool SecondaryAudioStreamNumberChange;
        public bool SecondaryPGStreamNumberChange;

        public UOMaskTable(Stream stream)
        {
            _flagField = stream.ReadBytes(8);
            var br = new BitReader(_flagField);
            MenuCall = br.GetBit();
            TitleSearch = br.GetBit();
            ChapterSearch = br.GetBit();
            TimeSearch = br.GetBit();
            SkipToNextPoint = br.GetBit();
            SkipToPrevPoint = br.GetBit();

            br.Skip(1);

            Stop = br.GetBit();
            PauseOn = br.GetBit();

            br.Skip(1);

            StillOff = br.GetBit();
            ForwardPlay = br.GetBit();
            BackwardPlay = br.GetBit();
            Resume = br.GetBit();
            MoveUpSelectedButton = br.GetBit();
            MoveDownSelectedButton = br.GetBit();
            MoveLeftSelectedButton = br.GetBit();
            MoveRightSelectedButton = br.GetBit();
            SelectButton = br.GetBit();
            ActivateButton = br.GetBit();
            SelectAndActivateButton = br.GetBit();
            PrimaryAudioStreamNumberChange = br.GetBit();

            br.Skip(1);

            AngleNumberChange = br.GetBit();
            PopupOn = br.GetBit();
            PopupOff = br.GetBit();
            PGEnableDisable = br.GetBit();
            PGStreamNumberChange = br.GetBit();
            SecondaryVideoEnableDisable = br.GetBit();
            SecondaryVideoStreamNumberChange = br.GetBit();
            SecondaryAudioEnableDisable = br.GetBit();
            SecondaryAudioStreamNumberChange = br.GetBit();

            br.Skip(1);

            SecondaryPGStreamNumberChange = br.GetBit();

            br.Skip(30);
        }

        public override string ToString()
        {
            return $"{{MenuCall: {MenuCall}, TitleSearch: {TitleSearch}, ChapterSearch: {ChapterSearch}, TimeSearch: {TimeSearch}, SkipToNextPoint: {SkipToNextPoint}, SkipToPrevPoint: {SkipToPrevPoint}, Stop: {Stop}, PauseOn: {PauseOn}, StillOff: {StillOff}, ForwardPlay: {ForwardPlay}, BackwardPlay: {BackwardPlay}, Resume: {Resume}, MoveUpSelectedButton: {MoveUpSelectedButton}, MoveDownSelectedButton: {MoveDownSelectedButton}, MoveLeftSelectedButton: {MoveLeftSelectedButton}, MoveRightSelectedButton: {MoveRightSelectedButton}, SelectButton: {SelectButton}, ActivateButton: {ActivateButton}, SelectAndActivateButton: {SelectAndActivateButton}, PrimaryAudioStreamNumberChange: {PrimaryAudioStreamNumberChange}, AngleNumberChange: {AngleNumberChange}, PopupOn: {PopupOn}, PopupOff: {PopupOff}, PGEnableDisable: {PGEnableDisable}, PGStreamNumberChange: {PGStreamNumberChange}, SecondaryVideoEnableDisable: {SecondaryVideoEnableDisable}, SecondaryVideoStreamNumberChange: {SecondaryVideoStreamNumberChange}, SecondaryAudioEnableDisable: {SecondaryAudioEnableDisable}, SecondaryAudioStreamNumberChange: {SecondaryAudioStreamNumberChange}, SecondaryPGStreamNumberChange: {SecondaryPGStreamNumberChange}}}";
        }
    }

    internal class PlayList
    {
        public uint Length;

        // 2bytes reserved
        public ushort NumberOfPlayItems;
        public ushort NumberOfSubPaths;
        public PlayItem[] PlayItems;
        public SubPath[] SubPaths;

        public PlayList(Stream stream)
        {
            Length = stream.BEInt32();
            var position = stream.Position;
            stream.Skip(2);
            NumberOfPlayItems = (ushort)stream.BEInt16();
            NumberOfSubPaths = (ushort)stream.BEInt16();
            PlayItems = new PlayItem[NumberOfPlayItems];
            SubPaths = new SubPath[NumberOfSubPaths];
            for (var i = 0; i < NumberOfPlayItems; ++i)
            {
                PlayItems[i] = new PlayItem(stream);
            }
            for (var i = 0; i < NumberOfSubPaths; ++i)
            {
                SubPaths[i] = new SubPath(stream);
            }
            stream.Skip(Length - (stream.Position - position));
        }

        public override string ToString()
        {
            return $"PlayList: {{PlayItems[{NumberOfPlayItems}], SubPaths[{NumberOfSubPaths}]}}";
        }
    }

    public class ClipName
    {
        public string ClipInformationFileName; // 5
        public string ClipCodecIdentifier; // 4

        public ClipName(Stream stream)
        {
            ClipInformationFileName = Encoding.ASCII.GetString(stream.ReadBytes(5));
            ClipCodecIdentifier = Encoding.ASCII.GetString(stream.ReadBytes(4));
        }

        public override string ToString()
        {
            return $"{ClipInformationFileName}.{ClipCodecIdentifier}";
        }
    }

    public class ClipNameWithRef
    {
        public ClipName ClipName;
        public byte RefToSTCID;

        public ClipNameWithRef(Stream stream)
        {
            ClipName = new ClipName(stream);
            RefToSTCID = (byte)stream.ReadByte();
        }
    }

    public class TimeInfo
    {
        public uint INTime;
        public uint OUTTime;

        public uint DeltaTime => OUTTime - INTime;

        public TimeInfo(Stream stream)
        {
            INTime = stream.BEInt32();
            OUTTime = stream.BEInt32();
        }

        public override string ToString()
        {
            return $"{{INTime: {INTime}, OUTTime: {OUTTime}}}";
        }
    }

    public class PlayItem
    {
        public ushort Length;
        public ClipName ClipName;
        private readonly ushort _flagField1;

        public bool IsMultiAngle => ((_flagField1 >> 4) & 1) == 1;

        public byte ConnectionCondition => (byte)(_flagField1 & 0x0f);

        public byte RefToSTCID;
        public TimeInfo TimeInfo;
        public UOMaskTable UOMaskTable;
        private readonly byte _flagField2;

        public bool PlayItemRandomAccessFlag => (_flagField2 >> 7) == 1;

        public byte StillMode;

        // if StillMode == 0x01:
        public ushort StillTime;

        // if IsMultiAngle:
        public MultiAngle MultiAngle;
        public STNTable STNTable;

        public PlayItem(Stream stream)
        {
            Length = (ushort)stream.BEInt16();
            var position = stream.Position;
            ClipName = new ClipName(stream);
            _flagField1 = (ushort)stream.BEInt16();
            RefToSTCID = (byte)stream.ReadByte();
            TimeInfo = new TimeInfo(stream);
            UOMaskTable = new UOMaskTable(stream);
            _flagField2 = (byte)stream.ReadByte();
            StillMode = (byte)stream.ReadByte();
            StillTime = (ushort)stream.BEInt16();
            if (IsMultiAngle)
            {
                MultiAngle = new MultiAngle(stream);
            }
            STNTable = new STNTable(stream);
            stream.Skip(Length - (stream.Position - position));
        }

        public string FullName
        {
            get
            {
                if (!IsMultiAngle) return ClipName.ClipInformationFileName;
                var ret = ClipName.ClipInformationFileName;
                foreach (var angle in MultiAngle.Angles)
                {
                    ret += $"&{angle.ClipName.ClipInformationFileName}";
                }
                return ret;
            }
        }
    }

    public class MultiAngle
    {
        public byte NumberOfAngles;
        private readonly byte _flagField;

        public bool IsDifferentAudios => _flagField >> 2 == 1;

        public bool IsSeamlessAngleChange => ((_flagField >> 1) & 0x01) == 1;

        public ClipNameWithRef[] Angles;

        public MultiAngle(Stream stream)
        {
            NumberOfAngles = (byte)stream.ReadByte();
            _flagField = (byte)stream.ReadByte();
            Angles = new ClipNameWithRef[NumberOfAngles - 1];
            for (var i = 0; i < NumberOfAngles - 1; ++i)
            {
                Angles[i] = new ClipNameWithRef(stream);
            }
        }
    }

    public class SubPath
    {
        public uint Length;

        // 1byte reserved
        public byte SubPathType;
        private readonly ushort _flagField;

        public bool IsRepeatSubPath => (_flagField & 1) == 1;

        public byte NumberOfSubPlayItems;
        public SubPlayItem[] SubPlayItems;

        public SubPath(Stream stream)
        {
            Length = stream.BEInt32();
            var position = stream.Position;
            stream.Skip(2);
            SubPathType = (byte)stream.ReadByte();
            _flagField = (ushort)stream.BEInt16();
            NumberOfSubPlayItems = (byte)stream.ReadByte();
            SubPlayItems = new SubPlayItem[NumberOfSubPlayItems];
            for (var i = 1; i < NumberOfSubPlayItems; ++i)
            {
                SubPlayItems[i] = new SubPlayItem(stream);
            }
            stream.Skip(Length - (stream.Position - position));
        }
    }

    public class SubPlayItem
    {
        public ushort Length;
        public ClipName ClipName;

        // 3bytes reserved
        // 3bits reserved
        private readonly byte _flagField;

        private byte ConnectionCondition => (byte)(_flagField >> 1);

        private bool IsMultiClipEntries => (_flagField & 1) == 1;

        public byte RefToSTCID;
        public TimeInfo TimeInfo;
        public ushort SyncPlayItemID;
        public uint SyncStartPTS;

        // if IsMultiClipEntries == 1:
        public byte NumberOfMultiClipEntries;
        public ClipNameWithRef[] MultiClipNameEntries;

        public SubPlayItem(Stream stream)
        {
            Length = (ushort)stream.BEInt16();
            var position = stream.Position;
            ClipName = new ClipName(stream);
            stream.Skip(3);
            _flagField = (byte)stream.ReadByte();
            RefToSTCID = (byte)stream.ReadByte();
            TimeInfo = new TimeInfo(stream);
            SyncPlayItemID = (ushort)stream.BEInt16();
            SyncStartPTS = stream.BEInt32();

            if (IsMultiClipEntries)
            {
                NumberOfMultiClipEntries = (byte)stream.ReadByte();
                MultiClipNameEntries = new ClipNameWithRef[NumberOfMultiClipEntries - 1];
                for (var i = 0; i < NumberOfMultiClipEntries - 1; ++i)
                {
                    MultiClipNameEntries[i] = new ClipNameWithRef(stream);
                }
            }
            stream.Skip(Length - (stream.Position - position));
        }
    }

    public class STNTable
    {
        public ushort Length;

        // 2bytes reserve
        public byte NumberOfPrimaryVideoStreamEntries;
        public byte NumberOfPrimaryAudioStreamEntries;
        public byte NumberOfPrimaryPGStreamEntries;
        public byte NumberOfPrimaryIGStreamEntries;
        public byte NumberOfSecondaryAudioStreamEntries;
        public byte NumberOfSecondaryVideoStreamEntries;
        public byte NumberOfSecondaryPGStreamEntries;

        public BasicStreamEntry[] StreamEntries;

        public STNTable(Stream stream)
        {
            Length = (ushort)stream.BEInt16();
            var position = stream.Position;
            stream.Skip(2);
            NumberOfPrimaryVideoStreamEntries = (byte)stream.ReadByte();
            NumberOfPrimaryAudioStreamEntries = (byte)stream.ReadByte();
            NumberOfPrimaryPGStreamEntries = (byte)stream.ReadByte();
            NumberOfPrimaryIGStreamEntries = (byte)stream.ReadByte();
            NumberOfSecondaryAudioStreamEntries = (byte)stream.ReadByte();
            NumberOfSecondaryVideoStreamEntries = (byte)stream.ReadByte();
            NumberOfSecondaryPGStreamEntries = (byte)stream.ReadByte();
            stream.Skip(5);

            StreamEntries = new BasicStreamEntry[
                NumberOfPrimaryVideoStreamEntries +
                NumberOfPrimaryAudioStreamEntries +
                NumberOfPrimaryPGStreamEntries +
                NumberOfPrimaryIGStreamEntries +
                NumberOfSecondaryAudioStreamEntries +
                NumberOfSecondaryVideoStreamEntries +
                NumberOfSecondaryPGStreamEntries];
            var index = 0;
            for (var i = 0; i < NumberOfPrimaryVideoStreamEntries; ++i) StreamEntries[index++] = new PrimaryVideoStreamEntry(stream);
            for (var i = 0; i < NumberOfPrimaryAudioStreamEntries; ++i) StreamEntries[index++] = new PrimaryAudioStreamEntry(stream);
            for (var i = 0; i < NumberOfPrimaryPGStreamEntries; ++i) StreamEntries[index++] = new PrimaryPGStreamEntry(stream);
            for (var i = 0; i < NumberOfSecondaryPGStreamEntries; ++i) StreamEntries[index++] = new SecondaryPGStreamEntry(stream);
            for (var i = 0; i < NumberOfPrimaryIGStreamEntries; ++i) StreamEntries[index++] = new PrimaryIGStreamEntry(stream);
            for (var i = 0; i < NumberOfSecondaryAudioStreamEntries; ++i) StreamEntries[index++] = new SecondaryAudioStreamEntry(stream);
            for (var i = 0; i < NumberOfSecondaryVideoStreamEntries; ++i) StreamEntries[index++] = new SecondaryVideoStreamEntry(stream);
            stream.Skip(Length - (stream.Position - position));
        }

        public override string ToString()
        {
            return $"{{PrimaryVideo: {NumberOfPrimaryVideoStreamEntries}, PrimaryAudio: {NumberOfPrimaryAudioStreamEntries}, PrimaryPG: {NumberOfPrimaryPGStreamEntries}, PrimaryIG: {NumberOfPrimaryIGStreamEntries}, SecondaryAudio: {NumberOfSecondaryAudioStreamEntries}, SecondaryVideo: {NumberOfSecondaryVideoStreamEntries}, SecondaryPG: {NumberOfSecondaryPGStreamEntries}}}";
        }
    }

    public class BasicStreamEntry
    {
        public StreamEntry StreamEntry;
        public StreamAttributes StreamAttributes;

        public BasicStreamEntry(Stream stream)
        {
            StreamEntry = new StreamEntry(stream);
            StreamAttributes = new StreamAttributes(stream);
        }
    }

    public class PrimaryVideoStreamEntry : BasicStreamEntry
    {
        public PrimaryVideoStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class PrimaryAudioStreamEntry : BasicStreamEntry
    {
        public PrimaryAudioStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class PrimaryPGStreamEntry : BasicStreamEntry
    {
        public PrimaryPGStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class SecondaryPGStreamEntry : BasicStreamEntry
    {
        public SecondaryPGStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class PrimaryIGStreamEntry : BasicStreamEntry
    {
        public PrimaryIGStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class SecondaryAudioStreamEntry : BasicStreamEntry
    {
        public SecondaryAudioStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class SecondaryVideoStreamEntry : BasicStreamEntry
    {
        public SecondaryVideoStreamEntry(Stream stream) : base(stream)
        {
        }
    }

    public class StreamEntry
    {
        public byte Length;
        public byte StreamType;

        public byte RefToSubPathID;
        public byte RefToSubClipID;
        public ushort RefToStreamPID;

        public StreamEntry(Stream stream)
        {
            Length = (byte)stream.ReadByte();
            var position = stream.Position;
            StreamType = (byte)stream.ReadByte();
            switch (StreamType)
            {
                case 0x01:
                case 0x03:
                    break;
                case 0x02:
                case 0x04:
                    RefToSubPathID = (byte)stream.ReadByte();
                    RefToSubClipID = (byte)stream.ReadByte();
                    break;
                default:
                    Console.WriteLine($"Unknow StreamType type: {StreamType:X}");
                    break;
            }
            RefToStreamPID = (ushort)stream.BEInt16();
            stream.Skip(Length - (stream.Position - position));
        }
    }

    public class StreamAttributes
    {
        public byte Length;
        public byte StreamCodingType;
        private readonly byte _videoInfo;

        public byte VideoFormat => (byte)(_videoInfo >> 4);

        public byte FrameRate => (byte)(_videoInfo & 0xf);

        private readonly byte _audioInfo;

        public byte AudioFormat => (byte)(_audioInfo >> 4);

        public byte SampleRate => (byte)(_audioInfo & 0xf);

        public byte CharacterCode;
        public string LanguageCode; // 3

        public StreamAttributes(Stream stream)
        {
            Length = (byte)stream.ReadByte();
            var position = stream.Position;
            StreamCodingType = (byte)stream.ReadByte();
            switch (StreamCodingType)
            {
                case 0x01:
                case 0x02:
                case 0x1B:
                case 0xEA:
                case 0x24:
                    _videoInfo = (byte)stream.ReadByte();
                    break;
                case 0x03:
                case 0x04:
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                case 0x84:
                case 0x85:
                case 0x86:
                case 0xA1:
                case 0xA2:
                    _audioInfo = (byte)stream.ReadByte();
                    LanguageCode = Encoding.ASCII.GetString(stream.ReadBytes(3));
                    break;
                case 0x90:
                case 0x91:
                    LanguageCode = Encoding.ASCII.GetString(stream.ReadBytes(3));
                    break;
                case 0x92:
                    CharacterCode = (byte)stream.ReadByte();
                    LanguageCode = Encoding.ASCII.GetString(stream.ReadBytes(3));
                    break;
                default:
                    Console.WriteLine($"Unknow StreamCodingType type: {StreamCodingType:X}");
                    break;
            }
            stream.Skip(Length - (stream.Position - position));
        }
    }

    public class Mark
    {
        // 1byte reserved
        public byte MarkType;
        public ushort RefToPlayItemID;
        public uint MarkTimeStamp;
        public ushort EntryESPID;
        public uint Duration;

        public Mark(Stream stream)
        {
            stream.Skip(1);
            MarkType = (byte)stream.ReadByte();
            RefToPlayItemID = (ushort)stream.BEInt16();
            MarkTimeStamp = stream.BEInt32();
            EntryESPID = (ushort)stream.BEInt16();
            Duration = stream.BEInt32();
        }
    }

    internal class PlayListMark
    {
        public uint Length;
        public ushort NumberOfPlayListMarks;
        public Mark[] Marks;

        public PlayListMark(Stream stream)
        {
            Length = stream.BEInt32();
            var position = stream.Position;
            NumberOfPlayListMarks = (ushort)stream.BEInt16();
            Marks = new Mark[NumberOfPlayListMarks];
            for (var i = 0; i < NumberOfPlayListMarks; ++i)
            {
                Marks[i] = new Mark(stream);
            }
            stream.Skip(Length - (stream.Position - position));
        }
    }

    internal class ExtensionData
    {
        public uint Length;
        public uint DataBlockStartAddress;

        // 3bytes reserved
        public byte NumberOfExtDataEntries;
        public ExtDataEntry[] ExtDataEntries;

        public ExtensionData(Stream stream)
        {
            Length = stream.BEInt32();
            if (Length == 0) return;
            DataBlockStartAddress = stream.BEInt32();
            stream.Skip(3);
            NumberOfExtDataEntries = (byte)stream.ReadByte();
            ExtDataEntries = new ExtDataEntry[NumberOfExtDataEntries];
            for (var i = 0; i < NumberOfExtDataEntries; ++i)
            {
                ExtDataEntries[i] = new ExtDataEntry(stream);
            }
        }
    }

    internal class ExtDataEntry
    {
        public ushort ExtDataType;
        public ushort ExtDataVersion;
        public uint ExtDataStartAddres;
        public uint ExtDataLength;

        public ExtDataEntry(Stream stream)
        {
            ExtDataType = (ushort)stream.BEInt16();
            ExtDataVersion = (ushort)stream.BEInt16();
            ExtDataStartAddres = stream.BEInt32();
            ExtDataLength = stream.BEInt32();
        }
    }

    internal static class StreamAttribution
    {
        public static event Action<string> OnLog;

        public static void LogStreamAttributes(BasicStreamEntry stream, ClipName clipName)
        {
            var streamCodingType = stream.StreamAttributes.StreamCodingType;
            var result = StreamCoding.TryGetValue(streamCodingType, out string streamCoding);
            if (!result) streamCoding = "und";
            OnLog?.Invoke($"Stream[{clipName}] Type: {streamCoding}");
            if (0x01 != streamCodingType && 0x02 != streamCodingType &&
                0x1b != streamCodingType && 0xea != streamCodingType &&
                0x24 != streamCodingType)
            {
                var isAudio = !(0x90 == streamCodingType || 0x91 == streamCodingType);
                if (0x92 == streamCodingType)
                {
                    OnLog?.Invoke($"Stream[{clipName}] CharacterCode: {CharacterCode[stream.StreamAttributes.CharacterCode]}");
                }
                var language = stream.StreamAttributes.LanguageCode;
                if (language == null || language[0] == '\0') language = "und";
                OnLog?.Invoke($"Stream[{clipName}] Language: {language}");
                if (isAudio)
                {
                    OnLog?.Invoke($"Stream[{clipName}] Channel: {Channel[stream.StreamAttributes.AudioFormat]}");
                    OnLog?.Invoke($"Stream[{clipName}] SampleRate: {SampleRate[stream.StreamAttributes.SampleRate]}");
                }
                return;
            }
            OnLog?.Invoke($"Stream[{clipName}] Resolution: {Resolution[stream.StreamAttributes.VideoFormat]}");
            OnLog?.Invoke($"Stream[{clipName}] FrameRate: {FrameRate[stream.StreamAttributes.FrameRate]}");
        }

        private static readonly Dictionary<int, string> StreamCoding = new Dictionary<int, string>
        {
            [0x01] = "MPEG-1 Video Stream",
            [0x02] = "MPEG-2 Video Stream",
            [0x03] = "MPEG-1 Audio Stream",
            [0x04] = "MPEG-2 Audio Stream",
            [0x1B] = "MPEG-4 AVC Video Stream",
            [0x24] = "HEVC Video Stream",
            [0xEA] = "SMPTE VC-1 Video Stream",
            [0x80] = "HDMV LPCM audio stream",
            [0x81] = "Dolby Digital (AC-3) audio stream",
            [0x82] = "DTS audio stream",
            [0x83] = "Dolby Digital TrueHD audio stream",
            [0x84] = "Dolby Digital Plus audio stream",
            [0x85] = "DTS-HD High Resolution Audio audio stream",
            [0x86] = "DTS-HD Master Audio audio stream",
            [0xA1] = "Dolby Digital Plus audio stream",
            [0xA2] = "DTS-HD audio stream",
            [0x90] = "Presentation Graphics Stream",
            [0x91] = "Interactive Graphics Stream",
            [0x92] = "Text Subtitle stream",
        };

        private static readonly Dictionary<int, string> Resolution = new Dictionary<int, string>
        {
            [0x00] = "res.",
            [0x01] = "720*480i",
            [0x02] = "720*576i",
            [0x03] = "720*480p",
            [0x04] = "1920*1080i",
            [0x05] = "1280*720p",
            [0x06] = "1920*1080p",
            [0x07] = "720*576p",
            [0x08] = "3840*2160p",
        };

        private static readonly Dictionary<int, string> FrameRate = new Dictionary<int, string>
        {
            [0x00] = "res.",
            [0x01] = "24000/1001 FPS",
            [0x02] = "24 FPS",
            [0x03] = "25 FPS",
            [0x04] = "30000/1001 FPS",
            [0x05] = "res.",
            [0x06] = "50 FPS",
            [0x07] = "60000/1001 FPS",
        };

        private static readonly Dictionary<int, string> Channel = new Dictionary<int, string>
        {
            [0x00] = "res.",
            [0x01] = "mono",
            [0x03] = "stereo",
            [0x06] = "multichannel",
            [0x0C] = "stereo and multichannel",
        };

        private static readonly Dictionary<int, string> SampleRate = new Dictionary<int, string>
        {
            [0x00] = "res.",
            [0x01] = "48 KHz",
            [0x04] = "96 KHz",
            [0x05] = "192 KHz",
            [0x0C] = "48 & 192 KHz",
            [0x0E] = "48 & 96 KHz",
        };

        private static readonly Dictionary<int, string> CharacterCode = new Dictionary<int, string>
        {
            [0x00] = "res.",
            [0x01] = "UTF-8",
            [0x02] = "UTF-16BE",
            [0x03] = "Shift-JIS",
            [0x04] = "EUC KR",
            [0x05] = "GB18030-2000",
            [0x06] = "GB2312",
            [0x07] = "BIG5",
        };
    }
}
