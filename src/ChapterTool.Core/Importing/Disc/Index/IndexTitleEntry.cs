namespace ChapterTool.Core.Importing.Disc.Index;

internal sealed record IndexTitleEntry(
    byte ObjectType,
    byte AccessType,
    ushort PlaybackType,
    IndexObjectReference ObjectReference)
{
    private const byte HdmvObjectType = 1;
    private const byte BdJObjectType = 2;
    private const ushort HdmvMoviePlaybackType = 0;
    private const ushort HdmvInteractivePlaybackType = 1;
    private const ushort BdJMoviePlaybackType = 2;
    private const ushort BdJInteractivePlaybackType = 3;

    internal const int SerializedLength = 12;

    public bool IsMovieObject => ObjectType == HdmvObjectType;

    public bool IsBDJObject => ObjectType == BdJObjectType;

    public bool IsMoviePlayback =>
        (IsMovieObject && PlaybackType == HdmvMoviePlaybackType)
        || (IsBDJObject && PlaybackType == BdJMoviePlaybackType);

    public bool IsInteractivePlayback =>
        (IsMovieObject && PlaybackType == HdmvInteractivePlaybackType)
        || (IsBDJObject && PlaybackType == BdJInteractivePlaybackType);

    public bool IsAccessProhibited => (AccessType & 0x01) != 0;

    public bool IsHidden => (AccessType & 0x02) != 0;

    public string ObjectData => ObjectReference switch
    {
        IndexHdmvObjectReference hdmv => hdmv.ObjectId.ToString("D5"),
        IndexBdJObjectReference bdj => bdj.Name,
        IndexUnknownObjectReference unknown => unknown.Data,
        _ => string.Empty
    };

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
            _ => new IndexUnknownObjectReference(stream.ReadAscii(6))
        };

        return new IndexTitleEntry(objectType, accessType, playbackType, objectData);
    }

    private static IndexObjectReference ReadHdmvObjectData(Stream stream)
    {
        var idReference = stream.ReadUInt16BigEndian();
        stream.SkipBytes(4);
        return new IndexHdmvObjectReference(idReference);
    }

    private static IndexObjectReference ReadBdJObjectData(Stream stream)
    {
        var objectData = stream.ReadAscii(5);
        stream.SkipBytes(1);
        return new IndexBdJObjectReference(objectData);
    }
}

internal abstract record IndexObjectReference;

internal sealed record IndexHdmvObjectReference(ushort ObjectId) : IndexObjectReference;

internal sealed record IndexBdJObjectReference(string Name) : IndexObjectReference;

internal sealed record IndexUnknownObjectReference(string Data) : IndexObjectReference;
