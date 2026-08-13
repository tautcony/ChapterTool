namespace ChapterTool.Core.Importing.Disc.Clpi;

#pragma warning disable SA1503

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

    internal ClpiPacketLookupResult? LookupPacket(byte stcId, uint timestamp)
    {
        var stc = SequenceInfo?.FindSTCSequence(stcId);
        if (stc == null || CPI == null || CPI.StreamEntries.Count == 0 || CPI.EPMaps.Count == 0) return null;
        var stream = CPI.StreamEntries[0];
        var map = CPI.EPMaps[0];
        if (map.FineEntries.Count == 0)
        {
            return new ClpiPacketLookupResult(stcId, stream.StreamPID, timestamp, 0, 0, -1, -1);
        }

        ClpiPacketLookupResult? selected = null;
        for (var coarseIndex = 0; coarseIndex < map.CoarseEntries.Count; coarseIndex++)
        {
            var coarse = map.CoarseEntries[coarseIndex];
            var start = checked((int)coarse.RefToEPFineID);
            var end = coarseIndex + 1 < map.CoarseEntries.Count
                ? checked((int)map.CoarseEntries[coarseIndex + 1].RefToEPFineID)
                : map.FineEntries.Count;
            if (start < 0 || start > end || end > map.FineEntries.Count) continue;
            for (var fineIndex = start; fineIndex < end; fineIndex++)
            {
                var fine = map.FineEntries[fineIndex];
                var entryTimestamp = unchecked(((uint)(coarse.PTSEPCoarse & 0xfffe) << 18) + ((uint)fine.PTSEPFine << 8));
                var packet = (coarse.SPNEPCoarse & ~0x1ffffU) + fine.SPNEPFine;
                if (packet < stc.SPNSTCStart || entryTimestamp > timestamp) continue;
                selected = new ClpiPacketLookupResult(
                    stcId,
                    stream.StreamPID,
                    timestamp,
                    entryTimestamp,
                    packet,
                    coarseIndex,
                    fineIndex);
            }
        }

        return selected ?? new ClpiPacketLookupResult(stcId, stream.StreamPID, timestamp, 0, 0, -1, -1);
    }
}

internal sealed record ClpiPacketLookupResult(
    byte STCId,
    ushort StreamPID,
    uint RequestedTimestamp,
    uint EntryTimestamp,
    uint SourcePacketNumber,
    int CoarseEntryIndex,
    int FineEntryIndex);
