namespace ChapterTool.Core.Importing.Disc;

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
