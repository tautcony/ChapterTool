namespace ChapterTool.Core.Importing.Disc;

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
    /// Gets a value indicating whether gets the IsMultiAngle value.
    /// </summary>
    public bool IsMultiAngle => ((FlagField >> 4) & 1) == 1;

    /// <summary>
    /// Gets the ConnectionCondition value.
    /// </summary>
    public byte ConnectionCondition => (byte)(FlagField & 0x0f);

    /// <summary>
    /// Gets a value indicating whether gets the PlayItemRandomAccessFlag value.
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
