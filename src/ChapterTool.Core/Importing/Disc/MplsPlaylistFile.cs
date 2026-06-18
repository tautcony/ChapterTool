namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsPlaylistFile(
    string TypeIndicator,
    string VersionNumber,
    uint PlayListStartAddress,
    uint PlayListMarkStartAddress,
    uint ExtensionDataStartAddress,
    MplsAppInfoPlayList AppInfoPlayList,
    MplsPlayList PlayList,
    MplsPlayListMark PlayListMark,
    MplsExtensionData? ExtensionData)
{
    public static MplsPlaylistFile Read(Stream stream)
    {
        var typeIndicator = stream.ReadAscii(4);
        if (typeIndicator != "MPLS")
        {
            throw new InvalidDataException("Invalid MPLS header.");
        }

        var versionNumber = stream.ReadAscii(4);
        if (versionNumber is not ("0100" or "0200" or "0300"))
        {
            throw new InvalidDataException($"Unsupported MPLS version: {versionNumber}.");
        }

        var playListStartAddress = stream.ReadUInt32BigEndian();
        var playListMarkStartAddress = stream.ReadUInt32BigEndian();
        var extensionDataStartAddress = stream.ReadUInt32BigEndian();
        stream.SkipBytes(20);
        var appInfoPlayList = MplsAppInfoPlayList.Read(stream);

        stream.Position = playListStartAddress;
        var playList = MplsPlayList.Read(stream);

        stream.Position = playListMarkStartAddress;
        var playListMark = MplsPlayListMark.Read(stream);

        MplsExtensionData? extensionData = null;
        if (extensionDataStartAddress != 0)
        {
            stream.Position = extensionDataStartAddress;
            extensionData = MplsExtensionData.Read(stream);
        }

        return new MplsPlaylistFile(
            typeIndicator,
            versionNumber,
            playListStartAddress,
            playListMarkStartAddress,
            extensionDataStartAddress,
            appInfoPlayList,
            playList,
            playListMark,
            extensionData);
    }
}

internal sealed record MplsAppInfoPlayList(
    uint Length,
    byte PlaybackType,
    ushort PlaybackCount,
    MplsUOMaskTable UOMaskTable,
    ushort FlagField)
{
    public bool RandomAccessFlag => ((FlagField >> 15) & 1) == 1;

    public bool AudioMixFlag => ((FlagField >> 14) & 1) == 1;

    public bool LosslessBypassFlag => ((FlagField >> 13) & 1) == 1;

    public bool MVCBaseViewRFlag => ((FlagField >> 12) & 1) == 1;

    public bool SDRConversionNotificationFlag => ((FlagField >> 11) & 1) == 1;

    public static MplsAppInfoPlayList Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        var position = stream.Position;
        stream.SkipBytes(1);
        var playbackType = stream.ReadByteChecked();
        var playbackCount = stream.ReadUInt16BigEndian();
        var uoMaskTable = MplsUOMaskTable.Read(stream);
        var flagField = stream.ReadUInt16BigEndian();
        stream.SkipBytes(length - (stream.Position - position));
        return new MplsAppInfoPlayList(length, playbackType, playbackCount, uoMaskTable, flagField);
    }
}

internal sealed record MplsPlayList(
    uint Length,
    ushort NumberOfPlayItems,
    ushort NumberOfSubPaths,
    IReadOnlyList<MplsPlayItem> PlayItems,
    IReadOnlyList<MplsSubPath> SubPaths)
{
    public static MplsPlayList Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        var position = stream.Position;
        stream.SkipBytes(2);
        var numberOfPlayItems = stream.ReadUInt16BigEndian();
        var numberOfSubPaths = stream.ReadUInt16BigEndian();
        var playItems = new List<MplsPlayItem>(numberOfPlayItems);
        for (var i = 0; i < numberOfPlayItems; i++)
        {
            playItems.Add(MplsPlayItem.Read(stream));
        }

        var subPaths = new List<MplsSubPath>(numberOfSubPaths);
        for (var i = 0; i < numberOfSubPaths; i++)
        {
            subPaths.Add(MplsSubPath.Read(stream));
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsPlayList(length, numberOfPlayItems, numberOfSubPaths, playItems, subPaths);
    }
}

internal sealed record MplsClipName(string ClipInformationFileName, string ClipCodecIdentifier)
{
    public static MplsClipName Read(Stream stream) =>
        new(stream.ReadAscii(5), stream.ReadAscii(4));

    public override string ToString() => $"{ClipInformationFileName}.{ClipCodecIdentifier}";
}

internal sealed record MplsClipNameWithRef(MplsClipName ClipName, byte RefToSTCID)
{
    public static MplsClipNameWithRef Read(Stream stream) =>
        new(MplsClipName.Read(stream), stream.ReadByteChecked());
}

internal sealed record MplsPlayItem(
    ushort Length,
    MplsClipName ClipName,
    ushort FlagField,
    byte RefToSTCID,
    uint INTime,
    uint OUTTime,
    MplsUOMaskTable UOMaskTable,
    byte PlayItemFlagField,
    byte StillMode,
    ushort StillTime,
    MplsMultiAngle? MultiAngle,
    MplsSTNTable STNTable)
{
    public bool IsMultiAngle => ((FlagField >> 4) & 1) == 1;

    public byte ConnectionCondition => (byte)(FlagField & 0x0f);

    public bool PlayItemRandomAccessFlag => (PlayItemFlagField >> 7) == 1;

    public string FullName => IsMultiAngle
        ? string.Join('&', new[] { ClipName.ClipInformationFileName }.Concat(MultiAngle?.Angles.Select(angle => angle.ClipName.ClipInformationFileName) ?? []))
        : ClipName.ClipInformationFileName;

    public static MplsPlayItem Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        var position = stream.Position;
        var clipName = MplsClipName.Read(stream);
        var flagField = stream.ReadUInt16BigEndian();
        var refToSTCID = stream.ReadByteChecked();
        var inTime = stream.ReadUInt32BigEndian();
        var outTime = stream.ReadUInt32BigEndian();
        var uoMaskTable = MplsUOMaskTable.Read(stream);
        var playItemFlagField = stream.ReadByteChecked();
        var stillMode = stream.ReadByteChecked();
        var stillTime = stream.ReadUInt16BigEndian();
        var isMultiAngle = ((flagField >> 4) & 1) == 1;
        var multiAngle = isMultiAngle ? MplsMultiAngle.Read(stream) : null;
        var stnTable = MplsSTNTable.Read(stream);
        stream.SkipBytes(length - (stream.Position - position));
        return new MplsPlayItem(
            length,
            clipName,
            flagField,
            refToSTCID,
            inTime,
            outTime,
            uoMaskTable,
            playItemFlagField,
            stillMode,
            stillTime,
            multiAngle,
            stnTable);
    }
}

internal sealed record MplsUOMaskTable(byte[] FlagField)
{
    public bool MenuCall => Bit(0);
    public bool TitleSearch => Bit(1);
    public bool ChapterSearch => Bit(2);
    public bool TimeSearch => Bit(3);
    public bool SkipToNextPoint => Bit(4);
    public bool SkipToPrevPoint => Bit(5);
    public bool Stop => Bit(7);
    public bool PauseOn => Bit(8);
    public bool StillOff => Bit(10);
    public bool ForwardPlay => Bit(11);
    public bool BackwardPlay => Bit(12);
    public bool Resume => Bit(13);
    public bool MoveUpSelectedButton => Bit(14);
    public bool MoveDownSelectedButton => Bit(15);
    public bool MoveLeftSelectedButton => Bit(16);
    public bool MoveRightSelectedButton => Bit(17);
    public bool SelectButton => Bit(18);
    public bool ActivateButton => Bit(19);
    public bool SelectAndActivateButton => Bit(20);
    public bool PrimaryAudioStreamNumberChange => Bit(21);
    public bool AngleNumberChange => Bit(23);
    public bool PopupOn => Bit(24);
    public bool PopupOff => Bit(25);
    public bool PrimaryPGEnableDisable => Bit(26);
    public bool PrimaryPGStreamNumberChange => Bit(27);
    public bool SecondaryVideoEnableDisable => Bit(28);
    public bool SecondaryVideoStreamNumberChange => Bit(29);
    public bool SecondaryAudioEnableDisable => Bit(30);
    public bool SecondaryAudioStreamNumberChange => Bit(31);
    public bool SecondaryPGStreamNumberChange => Bit(33);

    public static MplsUOMaskTable Read(Stream stream) =>
        new(stream.ReadExactBytes(8));

    private bool Bit(int index) =>
        (FlagField[index / 8] & (0x80 >> (index % 8))) != 0;
}

internal sealed record MplsMultiAngle(
    byte NumberOfAngles,
    byte FlagField,
    IReadOnlyList<MplsClipNameWithRef> Angles)
{
    public bool IsDifferentAudios => ((FlagField >> 1) & 1) == 1;

    public bool IsSeamlessAngleChange => (FlagField & 1) == 1;

    public static MplsMultiAngle Read(Stream stream)
    {
        var numberOfAngles = stream.ReadByteChecked();
        var flagField = stream.ReadByteChecked();
        var angles = new List<MplsClipNameWithRef>(Math.Max(0, numberOfAngles - 1));
        for (var i = 0; i < numberOfAngles - 1; i++)
        {
            angles.Add(MplsClipNameWithRef.Read(stream));
        }

        return new MplsMultiAngle(numberOfAngles, flagField, angles);
    }
}

internal sealed record MplsSubPath(
    uint Length,
    byte SubPathType,
    ushort FlagField,
    byte NumberOfSubPlayItems,
    IReadOnlyList<MplsSubPlayItem> SubPlayItems)
{
    public bool IsRepeatSubPath => (FlagField & 1) == 1;

    public static MplsSubPath Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        var position = stream.Position;
        stream.SkipBytes(1);
        var subPathType = stream.ReadByteChecked();
        var flagField = stream.ReadUInt16BigEndian();
        stream.SkipBytes(1);
        var numberOfSubPlayItems = stream.ReadByteChecked();
        var subPlayItems = new List<MplsSubPlayItem>(numberOfSubPlayItems);
        for (var i = 0; i < numberOfSubPlayItems; i++)
        {
            subPlayItems.Add(MplsSubPlayItem.Read(stream));
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsSubPath(length, subPathType, flagField, numberOfSubPlayItems, subPlayItems);
    }
}

internal sealed record MplsSubPlayItem(
    ushort Length,
    MplsClipName ClipName,
    byte FlagField,
    byte RefToSTCID,
    uint INTime,
    uint OUTTime,
    ushort SyncPlayItemID,
    uint SyncStartPTS,
    byte NumberOfMultiClipEntries,
    IReadOnlyList<MplsClipNameWithRef> MultiClipEntries)
{
    public byte ConnectionCondition => (byte)((FlagField >> 1) & 0x0f);

    public bool IsMultiClipEntries => (FlagField & 1) == 1;

    public static MplsSubPlayItem Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        var position = stream.Position;
        var clipName = MplsClipName.Read(stream);
        stream.SkipBytes(3);
        var flagField = stream.ReadByteChecked();
        var refToSTCID = stream.ReadByteChecked();
        var inTime = stream.ReadUInt32BigEndian();
        var outTime = stream.ReadUInt32BigEndian();
        var syncPlayItemId = stream.ReadUInt16BigEndian();
        var syncStartPts = stream.ReadUInt32BigEndian();
        var numberOfMultiClipEntries = (byte)0;
        var multiClipEntries = new List<MplsClipNameWithRef>();
        if ((flagField & 1) == 1)
        {
            numberOfMultiClipEntries = stream.ReadByteChecked();
            stream.SkipBytes(1);
            for (var i = 0; i < numberOfMultiClipEntries; i++)
            {
                multiClipEntries.Add(MplsClipNameWithRef.Read(stream));
            }
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsSubPlayItem(
            length,
            clipName,
            flagField,
            refToSTCID,
            inTime,
            outTime,
            syncPlayItemId,
            syncStartPts,
            numberOfMultiClipEntries,
            multiClipEntries);
    }
}

internal sealed record MplsSTNTable(
    ushort Length,
    byte NumberOfPrimaryVideoStreamEntries,
    byte NumberOfPrimaryAudioStreamEntries,
    byte NumberOfPrimaryPGStreamEntries,
    byte NumberOfPrimaryIGStreamEntries,
    byte NumberOfSecondaryAudioStreamEntries,
    byte NumberOfSecondaryVideoStreamEntries,
    byte NumberOfPIPPGStreamEntries,
    byte NumberOfDVStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryVideoStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryAudioStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryPGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryIGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> SecondaryAudioStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> SecondaryVideoStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PIPPGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> DVStreamEntries)
{
    public IReadOnlyList<MplsBasicStreamEntry> SubPathStreamEntries => PIPPGStreamEntries.Concat(DVStreamEntries).ToList();

    public static MplsSTNTable Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        var position = stream.Position;
        stream.SkipBytes(2);
        var primaryVideo = stream.ReadByteChecked();
        var primaryAudio = stream.ReadByteChecked();
        var primaryPg = stream.ReadByteChecked();
        var primaryIg = stream.ReadByteChecked();
        var secondaryAudio = stream.ReadByteChecked();
        var secondaryVideo = stream.ReadByteChecked();
        var pipPg = stream.ReadByteChecked();
        var dv = stream.ReadByteChecked();
        stream.SkipBytes(4);

        var primaryVideoEntries = ReadEntries(stream, primaryVideo);
        var primaryAudioEntries = ReadEntries(stream, primaryAudio);
        var primaryPgEntries = ReadEntries(stream, primaryPg);
        var pipPgEntries = ReadEntries(stream, pipPg);
        var primaryIgEntries = ReadEntries(stream, primaryIg);
        var secondaryAudioEntries = ReadEntries(stream, secondaryAudio);
        var secondaryVideoEntries = ReadEntries(stream, secondaryVideo);
        var dvEntries = ReadEntries(stream, dv);

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsSTNTable(
            length,
            primaryVideo,
            primaryAudio,
            primaryPg,
            primaryIg,
            secondaryAudio,
            secondaryVideo,
            pipPg,
            dv,
            primaryVideoEntries,
            primaryAudioEntries,
            primaryPgEntries,
            primaryIgEntries,
            secondaryAudioEntries,
            secondaryVideoEntries,
            pipPgEntries,
            dvEntries);
    }

    private static List<MplsBasicStreamEntry> ReadEntries(Stream stream, int count)
    {
        var entries = new List<MplsBasicStreamEntry>(count);
        for (var i = 0; i < count; i++)
        {
            entries.Add(MplsBasicStreamEntry.Read(stream));
        }

        return entries;
    }
}

internal sealed record MplsBasicStreamEntry(MplsStreamEntry StreamEntry, MplsStreamAttributes StreamAttributes)
{
    public static MplsBasicStreamEntry Read(Stream stream) =>
        new(MplsStreamEntry.Read(stream), MplsStreamAttributes.Read(stream));
}

internal sealed record MplsStreamEntry(
    byte Length,
    byte StreamType,
    byte? RefToSubPathID,
    byte? RefToSubClipID,
    ushort RefToStreamPID)
{
    public static MplsStreamEntry Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        var position = stream.Position;
        var streamType = stream.ReadByteChecked();
        byte? refToSubPathId = null;
        byte? refToSubClipId = null;
        ushort refToStreamPid;
        switch (streamType)
        {
            case 0x01:
                refToStreamPid = stream.ReadUInt16BigEndian();
                break;
            case 0x02:
                refToSubPathId = stream.ReadByteChecked();
                refToSubClipId = stream.ReadByteChecked();
                refToStreamPid = stream.ReadUInt16BigEndian();
                break;
            case 0x03:
            case 0x04:
                refToSubPathId = stream.ReadByteChecked();
                refToStreamPid = stream.ReadUInt16BigEndian();
                break;
            default:
                refToStreamPid = 0;
                break;
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsStreamEntry(length, streamType, refToSubPathId, refToSubClipId, refToStreamPid);
    }
}

internal sealed record MplsStreamAttributes(
    byte Length,
    byte StreamCodingType,
    byte? VideoFormat,
    byte? FrameRate,
    byte? DynamicRangeType,
    byte? ColorSpace,
    bool? CRFlag,
    bool? HDRPlusFlag,
    byte? AudioFormat,
    byte? SampleRate,
    byte? CharacterCode,
    string? LanguageCode)
{
    public static MplsStreamAttributes Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        var position = stream.Position;
        var streamCodingType = stream.ReadByteChecked();
        byte? videoFormat = null;
        byte? frameRate = null;
        byte? dynamicRangeType = null;
        byte? colorSpace = null;
        bool? crFlag = null;
        bool? hdrPlusFlag = null;
        byte? audioFormat = null;
        byte? sampleRate = null;
        byte? characterCode = null;
        string? languageCode = null;

        switch (streamCodingType)
        {
            case 0x01:
            case 0x02:
            case 0x1B:
            case 0x20:
            case 0xEA:
                ReadVideoInfo(stream, out videoFormat, out frameRate);
                break;
            case 0x24:
                ReadVideoInfo(stream, out videoFormat, out frameRate);
                var dynamicRangeAndColor = stream.ReadByteChecked();
                dynamicRangeType = (byte)(dynamicRangeAndColor >> 4);
                colorSpace = (byte)(dynamicRangeAndColor & 0x0f);
                var hdrFlags = stream.ReadByteChecked();
                crFlag = ((hdrFlags >> 7) & 1) == 1;
                hdrPlusFlag = ((hdrFlags >> 6) & 1) == 1;
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
                ReadAudioInfo(stream, out audioFormat, out sampleRate);
                languageCode = stream.ReadAscii(3);
                break;
            case 0x90:
            case 0x91:
                languageCode = stream.ReadAscii(3);
                break;
            case 0x92:
                characterCode = stream.ReadByteChecked();
                languageCode = stream.ReadAscii(3);
                break;
            case 0xA1:
            case 0xA2:
                ReadAudioInfo(stream, out audioFormat, out sampleRate);
                languageCode = stream.ReadAscii(3);
                break;
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsStreamAttributes(
            length,
            streamCodingType,
            videoFormat,
            frameRate,
            dynamicRangeType,
            colorSpace,
            crFlag,
            hdrPlusFlag,
            audioFormat,
            sampleRate,
            characterCode,
            languageCode);
    }

    private static void ReadVideoInfo(Stream stream, out byte? videoFormat, out byte? frameRate)
    {
        var videoInfo = stream.ReadByteChecked();
        videoFormat = (byte)(videoInfo >> 4);
        frameRate = (byte)(videoInfo & 0x0f);
    }

    private static void ReadAudioInfo(Stream stream, out byte? audioFormat, out byte? sampleRate)
    {
        var audioInfo = stream.ReadByteChecked();
        audioFormat = (byte)(audioInfo >> 4);
        sampleRate = (byte)(audioInfo & 0x0f);
    }
}

internal sealed record MplsPlayListMark(
    uint Length,
    ushort NumberOfPlayListMarks,
    IReadOnlyList<MplsMark> Marks)
{
    public static MplsPlayListMark Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        var position = stream.Position;
        var numberOfPlayListMarks = stream.ReadUInt16BigEndian();
        var marks = new List<MplsMark>(numberOfPlayListMarks);
        for (var i = 0; i < numberOfPlayListMarks; i++)
        {
            marks.Add(MplsMark.Read(stream));
        }

        stream.SkipBytes(length - (stream.Position - position));
        return new MplsPlayListMark(length, numberOfPlayListMarks, marks);
    }
}

internal sealed record MplsMark(
    byte MarkType,
    ushort RefToPlayItemID,
    uint MarkTimeStamp,
    ushort EntryESPID,
    uint Duration)
{
    public static MplsMark Read(Stream stream)
    {
        stream.SkipBytes(1);
        var markType = stream.ReadByteChecked();
        var refToPlayItemId = stream.ReadUInt16BigEndian();
        var markTimeStamp = stream.ReadUInt32BigEndian();
        var entryEspid = stream.ReadUInt16BigEndian();
        var duration = stream.ReadUInt32BigEndian();
        return new MplsMark(markType, refToPlayItemId, markTimeStamp, entryEspid, duration);
    }
}

internal sealed record MplsExtensionData(
    uint Length,
    uint DataBlockStartAddress,
    byte NumberOfExtDataEntries,
    IReadOnlyList<MplsExtDataEntry> ExtDataEntries,
    byte[] DataBlock)
{
    public static MplsExtensionData Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new MplsExtensionData(length, 0, 0, [], []);
        }

        var basePosition = stream.Position;
        var dataBlockStartAddress = stream.ReadUInt32BigEndian();
        stream.SkipBytes(3);
        var numberOfExtDataEntries = stream.ReadByteChecked();
        var entries = new List<MplsExtDataEntry>(numberOfExtDataEntries);
        for (var i = 0; i < numberOfExtDataEntries; i++)
        {
            entries.Add(MplsExtDataEntry.Read(stream));
        }

        var dataBlockLength = length - dataBlockStartAddress;
        stream.Position = basePosition + dataBlockStartAddress;
        var dataBlock = stream.ReadExactBytes(checked((int)dataBlockLength));
        return new MplsExtensionData(length, dataBlockStartAddress, numberOfExtDataEntries, entries, dataBlock);
    }
}

internal sealed record MplsExtDataEntry(
    ushort ExtDataType,
    ushort ExtDataVersion,
    uint ExtDataStartAddress,
    uint ExtDataLength)
{
    public static MplsExtDataEntry Read(Stream stream) =>
        new(
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt32BigEndian(),
            stream.ReadUInt32BigEndian());
}

internal static class MplsStreamReadExtensions
{
    public static byte ReadByteChecked(this Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException();
        }

        return (byte)value;
    }
}
