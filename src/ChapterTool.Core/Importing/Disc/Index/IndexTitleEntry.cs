namespace ChapterTool.Core.Importing.Disc.Index;

internal sealed record IndexTitleEntry(
    byte ObjectType,
    byte AccessType,
    ushort PlaybackType,
    string ObjectData)
{
    internal const int SerializedLength = 12;

    public bool IsMovieObject => ObjectType == 1;

    public bool IsBDJObject => ObjectType == 2;

    public bool IsMoviePlayback => PlaybackType is 0 or 2;

    public bool IsInteractivePlayback => PlaybackType is 1 or 3;

    public static IndexTitleEntry Read(Stream stream)
    {
        var firstByte = stream.ReadByteChecked();
        var objectType = (byte)(firstByte >> 6);
        var accessType = (byte)((firstByte >> 4) & 0x03);
        stream.SkipBytes(3);
        var playbackTypeAndReserved = stream.ReadByteChecked();
        stream.SkipBytes(1);
        var playbackType = (ushort)((playbackTypeAndReserved >> 6) & 0x03);
        var objectData = objectType switch
        {
            1 => ReadHdmvObjectData(stream),
            2 => ReadBdJObjectData(stream),
            _ => stream.ReadAscii(6)
        };

        return new IndexTitleEntry(objectType, accessType, playbackType, objectData);
    }

    private static string ReadHdmvObjectData(Stream stream)
    {
        var idReference = stream.ReadUInt16BigEndian();
        stream.SkipBytes(4);
        return idReference.ToString("D5");
    }

    private static string ReadBdJObjectData(Stream stream)
    {
        var objectData = stream.ReadAscii(5);
        stream.SkipBytes(1);
        return objectData;
    }
}
