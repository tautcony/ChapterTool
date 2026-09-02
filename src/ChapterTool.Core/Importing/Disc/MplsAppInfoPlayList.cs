namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsAppInfoPlayList(
    uint Length,
    byte PlaybackType,
    ushort PlaybackCount,
    MplsUOMaskTable UOMaskTable,
    ushort FlagField)
{
    /// <summary>
    /// Gets a value indicating whether gets the RandomAccessFlag value.
    /// </summary>
    public bool RandomAccessFlag => ((FlagField >> 15) & 1) == 1;

    /// <summary>
    /// Gets a value indicating whether gets the AudioMixFlag value.
    /// </summary>
    public bool AudioMixFlag => ((FlagField >> 14) & 1) == 1;

    /// <summary>
    /// Gets a value indicating whether gets the LosslessBypassFlag value.
    /// </summary>
    public bool LosslessBypassFlag => ((FlagField >> 13) & 1) == 1;

    /// <summary>
    /// Gets a value indicating whether gets the MVCBaseViewRFlag value.
    /// </summary>
    public bool MVCBaseViewRFlag => ((FlagField >> 12) & 1) == 1;

    /// <summary>
    /// Gets a value indicating whether gets the SDRConversionNotificationFlag value.
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
