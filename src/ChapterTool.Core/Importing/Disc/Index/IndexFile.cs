namespace ChapterTool.Core.Importing.Disc.Index;

internal sealed record IndexFile(
    string TypeIndicator,
    string VersionNumber,
    IndexAppInfoBDMV AppInfoBDMV,
    IndexIndexes Indexes)
{
    public static IndexFile Read(Stream stream)
    {
        var typeIndicator = stream.ReadAscii(4);
        if (typeIndicator != "INDX")
        {
            throw new InvalidDataException("Invalid INDEX header.");
        }

        var versionNumber = stream.ReadAscii(4);
        if (versionNumber is not ("0100" or "0200" or "0240" or "0300"))
        {
            throw new InvalidDataException($"Unsupported INDEX version: {versionNumber}.");
        }

        var indexesStartAddress = stream.ReadUInt32BigEndian();
        var extensionDataStartAddress = stream.ReadUInt32BigEndian();
        stream.SkipBytes(24);

        using var appInfoSection = MplsBoundedStream.CreateToAddress(stream, indexesStartAddress, "app info BDMV section");
        var appInfoBDMV = IndexAppInfoBDMV.Read(appInfoSection);
        appInfoSection.Complete("app info BDMV section");

        MplsParseLimits.SeekToAddress(stream, indexesStartAddress, "indexes");
        var indexesEnd = extensionDataStartAddress == 0 ? stream.Length : Math.Min(extensionDataStartAddress, (uint)stream.Length);
        using var indexesSection = MplsBoundedStream.CreateToAddress(stream, indexesEnd, "indexes section");
        var indexes = IndexIndexes.Read(indexesSection);
        indexesSection.Complete("indexes section");

        return new IndexFile(typeIndicator, versionNumber, appInfoBDMV, indexes);
    }

    public static IndexFile? TryRead(string path)
    {
        return TryRead(path, out _);
    }

    public static IndexFile? TryRead(string path, out string? error)
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
}
