using System.Text;

namespace ChapterTool.Core.Importing.Disc.Bdjo;

#pragma warning disable SA1503, SA1516

internal sealed record BdjoFile(
    string TypeIndicator,
    string VersionNumber,
    BdjoTerminalInfo TerminalInfo,
    BdjoApplicationCacheInfo ApplicationCacheInfo,
    BdjoAccessiblePlaylists AccessiblePlaylists,
    BdjoApplicationManagementTable ApplicationManagementTable,
    BdjoKeyInterestTable KeyInterestTable,
    BdjoFileAccessInfo FileAccessInfo)
{
    internal static BdjoFile Read(Stream stream)
    {
        if (!stream.CanSeek || stream.Length > BdjoParseLimits.MaximumFileLength)
            throw new InvalidDataException("BDJO file is outside the supported bounds.");

        stream.Position = 0;
        var type = stream.ReadAscii(4);
        var version = stream.ReadAscii(4);
        if (type != "BDJO" || version is not ("0100" or "0200" or "0240" or "0300"))
            throw new InvalidDataException("Invalid BDJO header.");

        return new BdjoFile(
            type,
            version,
            ReadTerminalInfo(stream),
            ReadApplicationCacheInfo(stream),
            ReadAccessiblePlaylists(stream),
            ReadApplicationManagementTable(stream),
            ReadKeyInterestTable(stream),
            ReadFileAccessInfo(stream));
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

    private static BdjoTerminalInfo ReadTerminalInfo(Stream stream)
    {
        using var section = ReadSection(stream, "terminal info", 10);
        var font = section.ReadAscii(5);
        var flags = section.ReadByteChecked();
        section.Complete("BDJO terminal info");
        return new BdjoTerminalInfo(font, (byte)(flags >> 4), (flags & 0x08) != 0, (flags & 0x04) != 0);
    }

    private static BdjoApplicationCacheInfo ReadApplicationCacheInfo(Stream stream)
    {
        using var section = ReadSection(stream, "application cache info", 2);
        var count = section.ReadByteChecked();
        section.SkipBytes(1);
        ValidateCount(count, BdjoParseLimits.MaximumCacheItems, section.Remaining, 12, "application cache item");
        var items = new List<BdjoApplicationCacheItem>(count);
        for (var i = 0; i < count; i++)
        {
            var item = new BdjoApplicationCacheItem(section.ReadByteChecked(), section.ReadAscii(5), section.ReadAscii(3));
            section.SkipBytes(3);
            items.Add(item);
        }

        section.Complete("BDJO application cache info");
        return new BdjoApplicationCacheInfo(items);
    }

    private static BdjoAccessiblePlaylists ReadAccessiblePlaylists(Stream stream)
    {
        using var section = ReadSection(stream, "accessible playlists", 4);
        var flags = section.ReadUInt32BigEndian();
        var count = (ushort)(flags >> 21 & 0x07ff);
        ValidateCount(count, BdjoParseLimits.MaximumPlaylists, section.Remaining, 6, "playlist");
        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var name = section.ReadAscii(5);
            section.SkipBytes(1);
            if (name.Any(static c => c is < '0' or > '9')) throw new InvalidDataException($"BDJO playlist name '{name}' is invalid.");
            names.Add(name);
        }

        section.Complete("BDJO accessible playlists");
        return new BdjoAccessiblePlaylists(count, (flags & 0x00100000) != 0, (flags & 0x00080000) != 0, names);
    }

    private static BdjoApplicationManagementTable ReadApplicationManagementTable(Stream stream)
    {
        using var section = ReadSection(stream, "application management table", 2);
        var count = section.ReadByteChecked();
        section.SkipBytes(1);
        if (count > BdjoParseLimits.MaximumApplications) throw new InvalidDataException("BDJO application count exceeds the supported bounds.");
        var applications = new List<BdjoApplication>(count);
        for (var i = 0; i < count; i++) applications.Add(ReadApplication(section));
        section.Complete("BDJO application management table");
        return new BdjoApplicationManagementTable(applications);
    }

    private static BdjoApplication ReadApplication(Stream stream)
    {
        var controlCode = stream.ReadByteChecked();
        var type = (byte)(stream.ReadByteChecked() >> 4);
        var organizationId = stream.ReadUInt32BigEndian();
        var applicationId = stream.ReadUInt16BigEndian();
        stream.SkipBytes(10);
        var profileCount = (byte)(stream.ReadUInt16BigEndian() >> 12);
        if (profileCount > BdjoParseLimits.MaximumProfiles) throw new InvalidDataException("BDJO application profile count exceeds the supported bounds.");
        var profiles = new List<BdjoApplicationProfile>(profileCount);
        for (var i = 0; i < profileCount; i++)
        {
            profiles.Add(new BdjoApplicationProfile(stream.ReadUInt16BigEndian(), stream.ReadByteChecked(), stream.ReadByteChecked(), stream.ReadByteChecked()));
            stream.SkipBytes(1);
        }

        var priority = stream.ReadByteChecked();
        var flags = stream.ReadByteChecked();
        var names = ReadNames(stream);
        var iconLocator = ReadAlignedString(stream);
        var iconFlags = stream.ReadUInt16BigEndian();
        var baseDirectory = ReadAlignedString(stream);
        var classpathExtension = ReadAlignedString(stream);
        var initialClass = ReadAlignedString(stream);
        var parameters = ReadParameters(stream);
        return new BdjoApplication(controlCode, type, organizationId, applicationId, profiles, priority,
            (byte)(flags >> 6), (byte)(flags >> 4 & 0x03), names, iconFlags, iconLocator,
            baseDirectory, classpathExtension, initialClass, parameters);
    }

    private static IReadOnlyList<BdjoApplicationName> ReadNames(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        ValidateStringData(stream, length, "application names");
        var end = stream.Position + length;
        var names = new List<BdjoApplicationName>();
        while (stream.Position < end)
        {
            if (names.Count >= BdjoParseLimits.MaximumNames || end - stream.Position < 4) throw new InvalidDataException("BDJO application name data is malformed.");
            var language = stream.ReadAscii(3);
            var nameLength = stream.ReadByteChecked();
            if (nameLength > end - stream.Position) throw new InvalidDataException("BDJO application name is truncated.");
            names.Add(new BdjoApplicationName(language, ReadUtf8(stream, nameLength)));
        }

        if ((length & 1) != 0) stream.SkipBytes(1);
        return names;
    }

    private static IReadOnlyList<string> ReadParameters(Stream stream)
    {
        var length = stream.ReadByteChecked();
        ValidateStringData(stream, length, "application parameters");
        var end = stream.Position + length;
        var parameters = new List<string>();
        while (stream.Position < end)
        {
            if (parameters.Count >= BdjoParseLimits.MaximumParameters) throw new InvalidDataException("BDJO application parameter count exceeds the supported bounds.");
            var itemLength = stream.ReadByteChecked();
            if (itemLength > end - stream.Position) throw new InvalidDataException("BDJO application parameter is truncated.");
            parameters.Add(ReadUtf8(stream, itemLength));
        }

        if ((length & 1) == 0) stream.SkipBytes(1);
        return parameters;
    }

    private static string ReadAlignedString(Stream stream)
    {
        var length = stream.ReadByteChecked();
        if (length > BdjoParseLimits.MaximumStringLength || length > stream.Length - stream.Position)
            throw new InvalidDataException("BDJO application string exceeds the supported bounds.");
        var value = ReadUtf8(stream, length);
        if ((length & 1) == 0) stream.SkipBytes(1);
        return value;
    }

    private static BdjoKeyInterestTable ReadKeyInterestTable(Stream stream)
    {
        var bits = stream.ReadUInt32BigEndian();
        return new BdjoKeyInterestTable(
            (bits & 0x80000000) != 0, (bits & 0x40000000) != 0, (bits & 0x20000000) != 0,
            (bits & 0x10000000) != 0, (bits & 0x08000000) != 0, (bits & 0x04000000) != 0,
            (bits & 0x02000000) != 0, (bits & 0x01000000) != 0, (bits & 0x00800000) != 0,
            (bits & 0x00400000) != 0, (bits & 0x00200000) != 0);
    }

    private static BdjoFileAccessInfo ReadFileAccessInfo(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        ValidateStringData(stream, length, "file access information");
        return new BdjoFileAccessInfo(ReadUtf8(stream, length));
    }

    private static MplsBoundedStream ReadSection(Stream stream, string name, int minimumLength)
    {
        var length = stream.ReadUInt32BigEndian();
        return MplsBoundedStream.Create(stream, length, minimumLength, BdjoParseLimits.MaximumSectionLength, $"BDJO {name}");
    }

    private static string ReadUtf8(Stream stream, int length) => Encoding.UTF8.GetString(stream.ReadExactBytes(length));

    private static void ValidateStringData(Stream stream, int length, string name)
    {
        if (length > BdjoParseLimits.MaximumStringDataLength || length > stream.Length - stream.Position)
            throw new InvalidDataException($"BDJO {name} exceed the supported bounds.");
    }

    private static void ValidateCount(int count, int maximum, long remaining, int itemSize, string name)
    {
        if (count > maximum || count > remaining / itemSize) throw new InvalidDataException($"BDJO {name} count exceeds the supported bounds.");
    }
}

internal sealed record BdjoTerminalInfo(string DefaultFont, byte InitialHaviConfigId, bool MenuCallMask, bool TitleSearchMask);
internal sealed record BdjoApplicationCacheItem(byte Type, string ReferenceName, string LanguageCode);
internal sealed record BdjoApplicationCacheInfo(IReadOnlyList<BdjoApplicationCacheItem> Items);
internal sealed record BdjoAccessiblePlaylists(ushort Count, bool AccessToAll, bool AutostartFirstPlaylist, IReadOnlyList<string> Names);
internal sealed record BdjoApplicationProfile(ushort ProfileNumber, byte MajorVersion, byte MinorVersion, byte MicroVersion);
internal sealed record BdjoApplicationName(string LanguageCode, string Name);
internal sealed record BdjoApplication(
    byte ControlCode,
    byte Type,
    uint OrganizationId,
    ushort ApplicationId,
    IReadOnlyList<BdjoApplicationProfile> Profiles,
    byte Priority,
    byte Binding,
    byte Visibility,
    IReadOnlyList<BdjoApplicationName> Names,
    ushort IconFlags,
    string IconLocator,
    string BaseDirectory,
    string ClasspathExtension,
    string InitialClass,
    IReadOnlyList<string> Parameters);
internal sealed record BdjoApplicationManagementTable(IReadOnlyList<BdjoApplication> Applications);
internal sealed record BdjoKeyInterestTable(bool Play, bool Stop, bool FastForward, bool Rewind, bool TrackNext, bool TrackPrevious, bool Pause, bool StillOff, bool SecondaryAudio, bool SecondaryVideo, bool PgTextSubtitle);
internal sealed record BdjoFileAccessInfo(string Path);
