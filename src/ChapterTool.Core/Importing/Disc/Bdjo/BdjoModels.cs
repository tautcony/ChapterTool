namespace ChapterTool.Core.Importing.Disc.Bdjo;

internal sealed record BdjoFile(
    string TypeIndicator,
    string VersionNumber,
    BdjoAccessiblePlaylists AccessiblePlaylists)
{
    internal static BdjoFile Read(Stream stream)
    {
        if (!stream.CanSeek || stream.Length > BdjoParseLimits.MaximumFileLength)
        {
            throw new InvalidDataException("BDJO file is outside the supported bounds.");
        }

        stream.Position = 0;
        var type = stream.ReadAscii(4);
        var version = stream.ReadAscii(4);
        if (type != "BDJO" || version is not ("0100" or "0200" or "0240" or "0300"))
        {
            throw new InvalidDataException("Invalid BDJO header.");
        }

        SkipSection(stream, "terminal info");
        SkipSection(stream, "application cache info");
        var accessibleLength = stream.ReadUInt32BigEndian();
        if (accessibleLength > BdjoParseLimits.MaximumSectionLength || accessibleLength < 4 ||
            accessibleLength > stream.Length - stream.Position)
        {
            throw new InvalidDataException("BDJO accessible-playlists section is outside the supported bounds.");
        }

        using var accessible = MplsBoundedStream.Create(
            stream,
            accessibleLength,
            4,
            BdjoParseLimits.MaximumSectionLength,
            "BDJO accessible playlists");
        var flags = accessible.ReadUInt32BigEndian();
        var count = (ushort)(flags >> 21 & 0x07ff);
        var accessToAll = (flags & 0x00100000) != 0;
        var autostart = (flags & 0x00080000) != 0;
        if (count > BdjoParseLimits.MaximumPlaylists || count > accessible.Remaining / 6)
        {
            throw new InvalidDataException("BDJO playlist count exceeds the supported bounds.");
        }

        var playlists = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var name = accessible.ReadAscii(5);
            accessible.SkipBytes(1);
            if (name.Length != 5 || name.Any(static c => c is < '0' or > '9'))
            {
                throw new InvalidDataException($"BDJO playlist name '{name}' is invalid.");
            }

            playlists.Add(name);
        }

        accessible.Complete("BDJO accessible playlists");
        return new BdjoFile(type, version, new BdjoAccessiblePlaylists(count, accessToAll, autostart, playlists));
    }

    internal static BdjoFile? TryRead(string path, out string? error)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var result = Read(stream);
            error = null;
            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            error = exception.Message;
            return null;
        }
    }

    private static void SkipSection(Stream stream, string name)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length > BdjoParseLimits.MaximumSectionLength || length > stream.Length - stream.Position)
        {
            throw new InvalidDataException($"BDJO {name} section is outside the supported bounds.");
        }

        stream.SkipBytes(length);
    }
}

internal sealed record BdjoAccessiblePlaylists(
    ushort Count,
    bool AccessToAll,
    bool AutostartFirstPlaylist,
    IReadOnlyList<string> Names);
