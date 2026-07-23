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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
        using var appInfoSection = MplsBoundedStream.CreateToAddress(stream, playListStartAddress, "app-info section");
        var appInfoPlayList = MplsAppInfoPlayList.Read(appInfoSection);
        appInfoSection.Complete("app-info section");

        MplsParseLimits.SeekToAddress(stream, playListStartAddress, "playlist");
        using var playlistSection = MplsBoundedStream.CreateToAddress(stream, playListMarkStartAddress, "playlist section");
        var playList = MplsPlayList.Read(playlistSection);
        playlistSection.Complete("playlist section");

        MplsParseLimits.SeekToAddress(stream, playListMarkStartAddress, "playlist mark");
        var markSectionEnd = extensionDataStartAddress == 0 ? stream.Length : extensionDataStartAddress;
        using var markSection = MplsBoundedStream.CreateToAddress(stream, markSectionEnd, "playlist mark section");
        var playListMark = MplsPlayListMark.Read(markSection);
        markSection.Complete("playlist mark section");

        MplsExtensionData? extensionData = null;
        if (extensionDataStartAddress != 0)
        {
            MplsParseLimits.SeekToAddress(stream, extensionDataStartAddress, "extension data");
            using var extensionSection = MplsBoundedStream.CreateToAddress(stream, stream.Length, "extension data section");
            extensionData = MplsExtensionData.Read(extensionSection);
            extensionSection.Complete("extension data section");
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
    /// <summary>
    /// Gets the RandomAccessFlag value.
    /// </summary>
    public bool RandomAccessFlag => ((FlagField >> 15) & 1) == 1;

    /// <summary>
    /// Gets the AudioMixFlag value.
    /// </summary>
    public bool AudioMixFlag => ((FlagField >> 14) & 1) == 1;

    /// <summary>
    /// Gets the LosslessBypassFlag value.
    /// </summary>
    public bool LosslessBypassFlag => ((FlagField >> 13) & 1) == 1;

    /// <summary>
    /// Gets the MVCBaseViewRFlag value.
    /// </summary>
    public bool MVCBaseViewRFlag => ((FlagField >> 12) & 1) == 1;

    /// <summary>
    /// Gets the SDRConversionNotificationFlag value.
    /// </summary>
    public bool SDRConversionNotificationFlag => ((FlagField >> 11) & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsAppInfoPlayList Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 14, MplsParseLimits.MaximumAppInfoLength, "app-info");
        container.SkipBytes(1);
        var playbackType = container.ReadByteChecked();
        var playbackCount = container.ReadUInt16BigEndian();
        var uoMaskTable = MplsUOMaskTable.Read(container);
        var flagField = container.ReadUInt16BigEndian();
        container.Complete("app-info");
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsPlayList Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 6, MplsParseLimits.MaximumPlaylistLength, "playlist");
        container.SkipBytes(2);
        var numberOfPlayItems = container.ReadUInt16BigEndian();
        var numberOfSubPaths = container.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCount(numberOfPlayItems, MplsParseLimits.MaximumPlayItems, "play item");
        MplsParseLimits.ValidateCount(numberOfSubPaths, MplsParseLimits.MaximumSubPaths, "subpath");
        MplsParseLimits.ValidateCountByBudget(numberOfPlayItems, 2, container.Remaining, "play item");
        MplsParseLimits.ValidateCountByBudget(numberOfSubPaths, 4, container.Remaining, "subpath");
        var playItems = new List<MplsPlayItem>(numberOfPlayItems);
        for (var i = 0; i < numberOfPlayItems; i++)
        {
            playItems.Add(MplsPlayItem.Read(container));
        }

        var subPaths = new List<MplsSubPath>(numberOfSubPaths);
        for (var i = 0; i < numberOfSubPaths; i++)
        {
            subPaths.Add(MplsSubPath.Read(container));
        }

        container.Complete("playlist");
        return new MplsPlayList(length, numberOfPlayItems, numberOfSubPaths, playItems, subPaths);
    }
}

internal sealed record MplsClipName(string ClipInformationFileName, string ClipCodecIdentifier)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsClipName Read(Stream stream) =>
        new(stream.ReadAscii(5), stream.ReadAscii(4));

    /// <inheritdoc />
    public override string ToString() => $"{ClipInformationFileName}.{ClipCodecIdentifier}";
}

internal sealed record MplsClipNameWithRef(MplsClipName ClipName, byte RefToSTCID)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
    /// <summary>
    /// Gets the IsMultiAngle value.
    /// </summary>
    public bool IsMultiAngle => ((FlagField >> 4) & 1) == 1;

    /// <summary>
    /// Gets the ConnectionCondition value.
    /// </summary>
    public byte ConnectionCondition => (byte)(FlagField & 0x0f);

    /// <summary>
    /// Gets the PlayItemRandomAccessFlag value.
    /// </summary>
    public bool PlayItemRandomAccessFlag => PlayItemFlagField >> 7 == 1;

    /// <summary>
    /// Gets the FullName value.
    /// </summary>
    public string FullName => IsMultiAngle
        ? string.Join('&', new[] { ClipName.ClipInformationFileName }.Concat(MultiAngle?.Angles.Select(angle => angle.ClipName.ClipInformationFileName) ?? []))
        : ClipName.ClipInformationFileName;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsPlayItem Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        using var container = stream.CreateMplsContainer(length, 34, MplsParseLimits.MaximumPlayItemLength, "play item");
        var clipName = MplsClipName.Read(container);
        var flagField = container.ReadUInt16BigEndian();
        var refToSTCID = container.ReadByteChecked();
        var inTime = container.ReadUInt32BigEndian();
        var outTime = container.ReadUInt32BigEndian();
        var uoMaskTable = MplsUOMaskTable.Read(container);
        var playItemFlagField = container.ReadByteChecked();
        var stillMode = container.ReadByteChecked();
        var stillTime = container.ReadUInt16BigEndian();
        var isMultiAngle = ((flagField >> 4) & 1) == 1;
        var multiAngle = isMultiAngle ? MplsMultiAngle.Read(container) : null;
        var stnTable = MplsSTNTable.Read(container);
        container.Complete("play item");
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
    /// <summary>
    /// Gets the MenuCall value.
    /// </summary>
    public bool MenuCall => Bit(0);
    /// <summary>
    /// Gets the TitleSearch value.
    /// </summary>
    public bool TitleSearch => Bit(1);
    /// <summary>
    /// Gets the ChapterSearch value.
    /// </summary>
    public bool ChapterSearch => Bit(2);
    /// <summary>
    /// Gets the TimeSearch value.
    /// </summary>
    public bool TimeSearch => Bit(3);
    /// <summary>
    /// Gets the SkipToNextPoint value.
    /// </summary>
    public bool SkipToNextPoint => Bit(4);
    /// <summary>
    /// Gets the SkipToPrevPoint value.
    /// </summary>
    public bool SkipToPrevPoint => Bit(5);
    /// <summary>
    /// Gets the Stop value.
    /// </summary>
    public bool Stop => Bit(7);
    /// <summary>
    /// Gets the PauseOn value.
    /// </summary>
    public bool PauseOn => Bit(8);
    /// <summary>
    /// Gets the StillOff value.
    /// </summary>
    public bool StillOff => Bit(10);
    /// <summary>
    /// Gets the ForwardPlay value.
    /// </summary>
    public bool ForwardPlay => Bit(11);
    /// <summary>
    /// Gets the BackwardPlay value.
    /// </summary>
    public bool BackwardPlay => Bit(12);
    /// <summary>
    /// Gets the Resume value.
    /// </summary>
    public bool Resume => Bit(13);
    /// <summary>
    /// Gets the MoveUpSelectedButton value.
    /// </summary>
    public bool MoveUpSelectedButton => Bit(14);
    /// <summary>
    /// Gets the MoveDownSelectedButton value.
    /// </summary>
    public bool MoveDownSelectedButton => Bit(15);
    /// <summary>
    /// Gets the MoveLeftSelectedButton value.
    /// </summary>
    public bool MoveLeftSelectedButton => Bit(16);
    /// <summary>
    /// Gets the MoveRightSelectedButton value.
    /// </summary>
    public bool MoveRightSelectedButton => Bit(17);
    /// <summary>
    /// Gets the SelectButton value.
    /// </summary>
    public bool SelectButton => Bit(18);
    /// <summary>
    /// Gets the ActivateButton value.
    /// </summary>
    public bool ActivateButton => Bit(19);
    /// <summary>
    /// Gets the SelectAndActivateButton value.
    /// </summary>
    public bool SelectAndActivateButton => Bit(20);
    /// <summary>
    /// Gets the PrimaryAudioStreamNumberChange value.
    /// </summary>
    public bool PrimaryAudioStreamNumberChange => Bit(21);
    /// <summary>
    /// Gets the AngleNumberChange value.
    /// </summary>
    public bool AngleNumberChange => Bit(23);
    /// <summary>
    /// Gets the PopupOn value.
    /// </summary>
    public bool PopupOn => Bit(24);
    /// <summary>
    /// Gets the PopupOff value.
    /// </summary>
    public bool PopupOff => Bit(25);
    /// <summary>
    /// Gets the PrimaryPGEnableDisable value.
    /// </summary>
    public bool PrimaryPGEnableDisable => Bit(26);
    /// <summary>
    /// Gets the PrimaryPGStreamNumberChange value.
    /// </summary>
    public bool PrimaryPGStreamNumberChange => Bit(27);
    /// <summary>
    /// Gets the SecondaryVideoEnableDisable value.
    /// </summary>
    public bool SecondaryVideoEnableDisable => Bit(28);
    /// <summary>
    /// Gets the SecondaryVideoStreamNumberChange value.
    /// </summary>
    public bool SecondaryVideoStreamNumberChange => Bit(29);
    /// <summary>
    /// Gets the SecondaryAudioEnableDisable value.
    /// </summary>
    public bool SecondaryAudioEnableDisable => Bit(30);
    /// <summary>
    /// Gets the SecondaryAudioStreamNumberChange value.
    /// </summary>
    public bool SecondaryAudioStreamNumberChange => Bit(31);
    /// <summary>
    /// Gets the SecondaryPGStreamNumberChange value.
    /// </summary>
    public bool SecondaryPGStreamNumberChange => Bit(33);

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
    /// <summary>
    /// Gets the IsDifferentAudios value.
    /// </summary>
    public bool IsDifferentAudios => ((FlagField >> 1) & 1) == 1;

    /// <summary>
    /// Gets the IsSeamlessAngleChange value.
    /// </summary>
    public bool IsSeamlessAngleChange => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsMultiAngle Read(Stream stream)
    {
        var numberOfAngles = stream.ReadByteChecked();
        var flagField = stream.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfAngles, MplsParseLimits.MaximumMultiAngleEntries, "multi-angle entry");
        MplsParseLimits.ValidateCountByBudget(numberOfAngles - 1, 10, stream.Length - stream.Position, "multi-angle entry");
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
    /// <summary>
    /// Gets the IsRepeatSubPath value.
    /// </summary>
    public bool IsRepeatSubPath => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSubPath Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 6, MplsParseLimits.MaximumSubPathLength, "subpath");
        container.SkipBytes(1);
        var subPathType = container.ReadByteChecked();
        var flagField = container.ReadUInt16BigEndian();
        container.SkipBytes(1);
        var numberOfSubPlayItems = container.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfSubPlayItems, MplsParseLimits.MaximumSubPlayItems, "subplay item");
        MplsParseLimits.ValidateCountByBudget(numberOfSubPlayItems, 2, container.Remaining, "subplay item");
        var subPlayItems = new List<MplsSubPlayItem>(numberOfSubPlayItems);
        for (var i = 0; i < numberOfSubPlayItems; i++)
        {
            subPlayItems.Add(MplsSubPlayItem.Read(container));
        }

        container.Complete("subpath");
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
    /// <summary>
    /// Gets the ConnectionCondition value.
    /// </summary>
    public byte ConnectionCondition => (byte)((FlagField >> 1) & 0x0f);

    /// <summary>
    /// Gets the IsMultiClipEntries value.
    /// </summary>
    public bool IsMultiClipEntries => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSubPlayItem Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        using var container = stream.CreateMplsContainer(length, 28, MplsParseLimits.MaximumSubPlayItemLength, "subplay item");
        var clipName = MplsClipName.Read(container);
        container.SkipBytes(3);
        var flagField = container.ReadByteChecked();
        var refToSTCID = container.ReadByteChecked();
        var inTime = container.ReadUInt32BigEndian();
        var outTime = container.ReadUInt32BigEndian();
        var syncPlayItemId = container.ReadUInt16BigEndian();
        var syncStartPts = container.ReadUInt32BigEndian();
        var numberOfMultiClipEntries = (byte)0;
        var multiClipEntries = new List<MplsClipNameWithRef>();
        if ((flagField & 1) == 1)
        {
            numberOfMultiClipEntries = container.ReadByteChecked();
            MplsParseLimits.ValidateCount(numberOfMultiClipEntries, MplsParseLimits.MaximumMultiClipEntries, "multi-clip entry");
            MplsParseLimits.ValidateCountByBudget(numberOfMultiClipEntries, 10, container.Remaining - 1, "multi-clip entry");
            container.SkipBytes(1);
            for (var i = 0; i < numberOfMultiClipEntries; i++)
            {
                multiClipEntries.Add(MplsClipNameWithRef.Read(container));
            }
        }

        container.Complete("subplay item");
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
    /// <summary>
    /// Gets the SubPathStreamEntries value.
    /// </summary>
    public IReadOnlyList<MplsBasicStreamEntry> SubPathStreamEntries => PIPPGStreamEntries.Concat(DVStreamEntries).ToList();

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSTNTable Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        using var container = stream.CreateMplsContainer(length, 14, MplsParseLimits.MaximumStnTableLength, "stream table");
        container.SkipBytes(2);
        var primaryVideo = container.ReadByteChecked();
        var primaryAudio = container.ReadByteChecked();
        var primaryPg = container.ReadByteChecked();
        var primaryIg = container.ReadByteChecked();
        var secondaryAudio = container.ReadByteChecked();
        var secondaryVideo = container.ReadByteChecked();
        var pipPg = container.ReadByteChecked();
        var dv = container.ReadByteChecked();
        container.SkipBytes(4);

        var primaryVideoEntries = ReadEntries(container, primaryVideo, "primary video stream");
        var primaryAudioEntries = ReadEntries(container, primaryAudio, "primary audio stream");
        var primaryPgEntries = ReadEntries(container, primaryPg, "primary presentation graphics stream");
        var pipPgEntries = ReadEntries(container, pipPg, "picture-in-picture graphics stream");
        var primaryIgEntries = ReadEntries(container, primaryIg, "primary interactive graphics stream");
        var secondaryAudioEntries = ReadEntries(container, secondaryAudio, "secondary audio stream");
        var secondaryVideoEntries = ReadEntries(container, secondaryVideo, "secondary video stream");
        var dvEntries = ReadEntries(container, dv, "Dolby Vision stream");

        container.Complete("stream table");
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

    private static List<MplsBasicStreamEntry> ReadEntries(Stream stream, int count, string entryName)
    {
        MplsParseLimits.ValidateCount(count, MplsParseLimits.MaximumStreamEntriesPerCategory, entryName);
        MplsParseLimits.ValidateCountByBudget(count, 3, stream.Length - stream.Position, entryName);
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsStreamEntry Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        using var container = stream.CreateMplsContainer(length, 0, byte.MaxValue, "stream entry");
        if (length == 0)
        {
            container.Complete("stream entry");
            return new MplsStreamEntry(length, 0, null, null, 0);
        }

        var streamType = container.ReadByteChecked();
        byte? refToSubPathId = null;
        byte? refToSubClipId = null;
        ushort refToStreamPid;
        switch (streamType)
        {
            case 0x01:
                refToStreamPid = container.ReadUInt16BigEndian();
                break;
            case 0x02:
                refToSubPathId = container.ReadByteChecked();
                refToSubClipId = container.ReadByteChecked();
                refToStreamPid = container.ReadUInt16BigEndian();
                break;
            case 0x03:
            case 0x04:
                refToSubPathId = container.ReadByteChecked();
                refToStreamPid = container.ReadUInt16BigEndian();
                break;
            default:
                refToStreamPid = 0;
                break;
        }

        container.Complete("stream entry");
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsStreamAttributes Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        using var container = stream.CreateMplsContainer(length, 0, byte.MaxValue, "stream attributes");
        if (length == 0)
        {
            container.Complete("stream attributes");
            return new MplsStreamAttributes(length, 0, null, null, null, null, null, null, null, null, null, null);
        }

        var streamCodingType = container.ReadByteChecked();
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
                ReadVideoInfo(container, out videoFormat, out frameRate);
                break;
            case 0x24:
                ReadVideoInfo(container, out videoFormat, out frameRate);
                var dynamicRangeAndColor = container.ReadByteChecked();
                dynamicRangeType = (byte)(dynamicRangeAndColor >> 4);
                colorSpace = (byte)(dynamicRangeAndColor & 0x0f);
                var hdrFlags = container.ReadByteChecked();
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
                ReadAudioInfo(container, out audioFormat, out sampleRate);
                languageCode = container.ReadAscii(3);
                break;
            case 0x90:
            case 0x91:
                languageCode = container.ReadAscii(3);
                break;
            case 0x92:
                characterCode = container.ReadByteChecked();
                languageCode = container.ReadAscii(3);
                break;
            case 0xA1:
            case 0xA2:
                ReadAudioInfo(container, out audioFormat, out sampleRate);
                languageCode = container.ReadAscii(3);
                break;
        }

        container.Complete("stream attributes");
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsPlayListMark Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 2, MplsParseLimits.MaximumMarkTableLength, "playlist mark table");
        var numberOfPlayListMarks = container.ReadUInt16BigEndian();
        MplsParseLimits.ValidateCount(numberOfPlayListMarks, MplsParseLimits.MaximumPlayListMarks, "playlist mark");
        if (2L + numberOfPlayListMarks * 14L > length)
        {
            throw new InvalidDataException("MPLS playlist mark table length cannot contain its declared marks.");
        }
        MplsParseLimits.ValidateCountByBudget(numberOfPlayListMarks, 14, container.Remaining, "playlist mark");
        var marks = new List<MplsMark>(numberOfPlayListMarks);
        for (var i = 0; i < numberOfPlayListMarks; i++)
        {
            marks.Add(MplsMark.Read(container));
        }

        container.Complete("playlist mark table");
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsExtensionData Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new MplsExtensionData(length, 0, 0, [], []);
        }

        using var container = stream.CreateMplsContainer(length, 8, MplsParseLimits.MaximumExtensionDataLength, "extension data");
        var dataBlockStartAddress = container.ReadUInt32BigEndian();
        container.SkipBytes(3);
        var numberOfExtDataEntries = container.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfExtDataEntries, MplsParseLimits.MaximumExtensionEntries, "extension data entry");
        if (8L + numberOfExtDataEntries * 12L > length)
        {
            throw new InvalidDataException("MPLS extension data length cannot contain its declared entries.");
        }
        MplsParseLimits.ValidateCountByBudget(numberOfExtDataEntries, 12, container.Remaining, "extension data entry");
        var entries = new List<MplsExtDataEntry>(numberOfExtDataEntries);
        for (var i = 0; i < numberOfExtDataEntries; i++)
        {
            entries.Add(MplsExtDataEntry.Read(container));
        }

        if (dataBlockStartAddress > length || dataBlockStartAddress < 8L + numberOfExtDataEntries * 12L)
        {
            throw new InvalidDataException("MPLS extension data block start address exceeds extension length.");
        }

        var dataBlockLength = length - dataBlockStartAddress;
        container.Position = dataBlockStartAddress;
        var dataBlock = container.ReadExactBytes((int)dataBlockLength);
        container.Complete("extension data");
        return new MplsExtensionData(length, dataBlockStartAddress, numberOfExtDataEntries, entries, dataBlock);
    }
}

internal sealed record MplsExtDataEntry(
    ushort ExtDataType,
    ushort ExtDataVersion,
    uint ExtDataStartAddress,
    uint ExtDataLength)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsExtDataEntry Read(Stream stream) =>
        new(
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt16BigEndian(),
            stream.ReadUInt32BigEndian(),
            stream.ReadUInt32BigEndian());
}

internal static class MplsStreamReadExtensions
{
    /// <summary>
    /// Executes the ReadByteChecked operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
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
