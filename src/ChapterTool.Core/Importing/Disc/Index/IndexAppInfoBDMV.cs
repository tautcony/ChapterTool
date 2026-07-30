namespace ChapterTool.Core.Importing.Disc.Index;

internal sealed record IndexAppInfoBDMV(
    uint Length,
    bool InitialOutputModePreference,
    bool SSContentExistFlag,
    byte? InitialDynamicRangeType,
    byte VideoFormat,
    byte FrameRate,
    string UserData)
{
    public static IndexAppInfoBDMV Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 4, 64 * 1024, "app info BDMV");
        container.SkipBytes(1);
        var flags = container.ReadByteChecked();
        var initialOutputModePreference = (flags & 0x40) != 0;
        var ssContentExistFlag = (flags & 0x20) != 0;
        var initialDynamicRangeType = (byte)(flags & 0x0f);
        var videoFormat = (byte)(container.ReadByteChecked() >> 4);
        var frameRate = (byte)(container.ReadByteChecked() & 0x0f);
        var userData = container.ReadAscii((int)container.Remaining);
        container.Complete("app info BDMV");
        return new IndexAppInfoBDMV(
            length,
            initialOutputModePreference,
            ssContentExistFlag,
            initialDynamicRangeType,
            videoFormat,
            frameRate,
            userData);
    }
}
