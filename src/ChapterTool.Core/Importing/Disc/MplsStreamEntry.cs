namespace ChapterTool.Core.Importing.Disc;

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
