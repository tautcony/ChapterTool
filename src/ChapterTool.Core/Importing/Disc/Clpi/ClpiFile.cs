namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiFile(
    string TypeIndicator,
    string VersionNumber,
    uint SequenceInfoStartAddress,
    uint ProgramInfoStartAddress,
    uint CPIStartAddress,
    uint ClipMarkStartAddress,
    uint ExtensionDataStartAddress,
    ClpiClipInfo ClipInfo,
    ClpiSequenceInfo? SequenceInfo,
    ClpiProgramInfo? ProgramInfo,
    ClpiCPI? CPI,
    ClpiExtensionData? ExtensionData)
{
    public static ClpiFile Read(Stream stream)
    {
        var typeIndicator = stream.ReadAscii(4);
        if (typeIndicator != "HDMV")
        {
            throw new InvalidDataException("Invalid CLPI header.");
        }

        var versionNumber = stream.ReadAscii(4);
        if (versionNumber is not ("0100" or "0200" or "0240" or "0300"))
        {
            throw new InvalidDataException($"Unsupported CLPI version: {versionNumber}.");
        }

        var sequenceInfoStartAddress = stream.ReadUInt32BigEndian();
        var programInfoStartAddress = stream.ReadUInt32BigEndian();
        var cpiStartAddress = stream.ReadUInt32BigEndian();
        var clipMarkStartAddress = stream.ReadUInt32BigEndian();
        var extensionDataStartAddress = stream.ReadUInt32BigEndian();
        stream.SkipBytes(12);

        using var clipInfoSection = MplsBoundedStream.CreateToAddress(stream, sequenceInfoStartAddress, "clip info section");
        var clipInfo = ClpiClipInfo.Read(clipInfoSection);
        clipInfoSection.Complete("clip info section");

        ClpiSequenceInfo? sequenceInfo = null;
        if (sequenceInfoStartAddress > 0 && sequenceInfoStartAddress < stream.Length)
        {
            MplsParseLimits.SeekToAddress(stream, sequenceInfoStartAddress, "sequence info");
            using var seqSection = MplsBoundedStream.CreateToAddress(stream, programInfoStartAddress, "sequence info section");
            sequenceInfo = ClpiSequenceInfo.Read(seqSection);
            seqSection.Complete("sequence info section");
        }

        ClpiProgramInfo? programInfo = null;
        if (programInfoStartAddress > 0 && programInfoStartAddress < stream.Length)
        {
            MplsParseLimits.SeekToAddress(stream, programInfoStartAddress, "program info");
            using var progSection = MplsBoundedStream.CreateToAddress(stream, cpiStartAddress, "program info section");
            programInfo = ClpiProgramInfo.Read(progSection);
            progSection.Complete("program info section");
        }

        ClpiCPI? cpi = null;
        if (cpiStartAddress > 0 && cpiStartAddress < stream.Length)
        {
            MplsParseLimits.SeekToAddress(stream, cpiStartAddress, "CPI");
            var cpiSectionEnd = extensionDataStartAddress == 0 ? stream.Length : Math.Min(extensionDataStartAddress, (uint)stream.Length);
            using var cpiSection = MplsBoundedStream.CreateToAddress(stream, cpiSectionEnd, "CPI section");
            cpi = ClpiCPI.Read(cpiSection);
            cpiSection.Complete("CPI section");
        }

        ClpiExtensionData? extensionData = null;
        if (extensionDataStartAddress > 0 && extensionDataStartAddress < stream.Length)
        {
            MplsParseLimits.SeekToAddress(stream, extensionDataStartAddress, "extension data");
            using var extensionSection = MplsBoundedStream.CreateToAddress(stream, stream.Length, "extension data section");
            extensionData = ClpiExtensionData.Read(extensionSection);
            extensionSection.Complete("extension data section");
        }

        return new ClpiFile(
            typeIndicator,
            versionNumber,
            sequenceInfoStartAddress,
            programInfoStartAddress,
            cpiStartAddress,
            clipMarkStartAddress,
            extensionDataStartAddress,
            clipInfo,
            sequenceInfo,
            programInfo,
            cpi,
            extensionData);
    }

    public static ClpiFile? TryRead(string path)
    {
        return TryRead(path, out _);
    }

    public static ClpiFile? TryRead(string path, out string? error)
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
