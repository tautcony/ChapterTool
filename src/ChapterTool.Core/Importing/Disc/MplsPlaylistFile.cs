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
        if (versionNumber is not ("0100" or "0200" or "0240" or "0300"))
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
