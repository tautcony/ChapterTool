namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiEPStreamEntry(
    ushort StreamPID,
    byte EPStreamType,
    ushort NumberOfEPCoarseEntries,
    uint NumberOfEPFineEntries,
    uint EPMapForOneStreamPIDStartAddress);

internal sealed record ClpiEPCoarseEntry(
    uint RefToEPFineID,
    ushort PTSEPCoarse,
    uint SPNEPCoarse);

internal sealed record ClpiEPFineEntry(
    byte ReservedEPFine,
    byte IEndPositionOffset,
    ushort PTSEPFine,
    uint SPNEPFine);

internal sealed record ClpiEPMap(
    IReadOnlyList<ClpiEPCoarseEntry> CoarseEntries,
    IReadOnlyList<ClpiEPFineEntry> FineEntries);

internal sealed record ClpiCPI(
    uint Length,
    byte CPIType,
    IReadOnlyList<ClpiEPStreamEntry> StreamEntries,
    IReadOnlyList<ClpiEPMap> EPMaps)
{
    public static ClpiCPI Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        if (length == 0)
        {
            return new ClpiCPI(length, 0, [], []);
        }

        using var container = stream.CreateMplsContainer(length, 8, ClpiParseLimits.MaximumCPILength, "CPI");
        container.SkipBytes(1);
        var cpiTypeAndReserved = container.ReadByteChecked();
        var cpiType = (byte)(cpiTypeAndReserved & 0x0f);
        container.SkipBytes(1);
        var numberOfStreamPIDEntries = container.ReadByteChecked();
        ClpiParseLimits.ValidateCount(numberOfStreamPIDEntries, ClpiParseLimits.MaximumStreamPIDEntries, "EP stream PID");
        ClpiParseLimits.ValidateCountByBudget(numberOfStreamPIDEntries, 12, container.Remaining, "EP stream PID");

        var streamEntries = new List<ClpiEPStreamEntry>(numberOfStreamPIDEntries);
        var streamEntryReader = new ClpiBitReader(container);
        for (var i = 0; i < numberOfStreamPIDEntries; i++)
        {
            var streamPID = (ushort)streamEntryReader.ReadBits(16);
            streamEntryReader.ReadBits(10);
            var epStreamType = (byte)streamEntryReader.ReadBits(4);
            var numberOfEPCoarseEntries = (ushort)streamEntryReader.ReadBits(16);
            var numberOfEPFineEntries = streamEntryReader.ReadBits(18);
            var epMapStartAddress = streamEntryReader.ReadBits(32);
            streamEntries.Add(new ClpiEPStreamEntry(
                streamPID,
                epStreamType,
                numberOfEPCoarseEntries,
                numberOfEPFineEntries,
                epMapStartAddress));
        }

        var epMaps = new List<ClpiEPMap>(numberOfStreamPIDEntries);
        for (var i = 0; i < numberOfStreamPIDEntries; i++)
        {
            var entry = streamEntries[i];
            container.Position = 2 + entry.EPMapForOneStreamPIDStartAddress;

            var epFineTableStartAddress = container.ReadUInt32BigEndian();

            ClpiParseLimits.ValidateCountByBudget(entry.NumberOfEPCoarseEntries, 8, container.Remaining, "EP coarse entry");

            var coarseEntries = new List<ClpiEPCoarseEntry>(Math.Min(entry.NumberOfEPCoarseEntries, ClpiParseLimits.MaximumEPCoarseEntries));
            var coarseReader = new ClpiBitReader(container);
            for (var j = 0; j < entry.NumberOfEPCoarseEntries; j++)
            {
                var refToEPFineID = coarseReader.ReadBits(18);
                if (entry.NumberOfEPFineEntries > 0 && refToEPFineID >= entry.NumberOfEPFineEntries)
                {
                    refToEPFineID = entry.NumberOfEPFineEntries - 1;
                }

                var ptsEPCoarse = (ushort)coarseReader.ReadBits(14);
                var spnEPCoarse = coarseReader.ReadBits(32);
                coarseEntries.Add(new ClpiEPCoarseEntry(refToEPFineID, ptsEPCoarse, spnEPCoarse));
            }

            container.Position = 2 + entry.EPMapForOneStreamPIDStartAddress + epFineTableStartAddress;

            ClpiParseLimits.ValidateCountByBudget(entry.NumberOfEPFineEntries, 4, container.Remaining, "EP fine entry");

            var fineEntries = new List<ClpiEPFineEntry>((int)Math.Min(entry.NumberOfEPFineEntries, ClpiParseLimits.MaximumEPFineEntries));
            var fineReader = new ClpiBitReader(container);
            for (var j = 0; j < entry.NumberOfEPFineEntries; j++)
            {
                var reservedEPFine = (byte)fineReader.ReadBits(1);
                var iEndPositionOffset = (byte)fineReader.ReadBits(3);
                var ptsEPFineLow = (ushort)fineReader.ReadBits(11);
                var spnEPFineLow = fineReader.ReadBits(17);
                fineEntries.Add(new ClpiEPFineEntry(reservedEPFine, iEndPositionOffset, ptsEPFineLow, spnEPFineLow));
            }

            epMaps.Add(new ClpiEPMap(coarseEntries, fineEntries));
        }

        container.Complete("CPI");
        return new ClpiCPI(length, cpiType, streamEntries, epMaps);
    }

    private sealed class ClpiBitReader(Stream stream)
    {
        private int bitsRemaining;
        private uint buffer;

        public uint ReadBits(int count)
        {
            if (count is < 1 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            uint value = 0;
            while (count > 0)
            {
                if (bitsRemaining == 0)
                {
                    buffer = stream.ReadByteChecked();
                    bitsRemaining = 8;
                }

                var take = Math.Min(count, bitsRemaining);
                value = (value << take) | ((buffer >> (bitsRemaining - take)) & ((1u << take) - 1));
                bitsRemaining -= take;
                count -= take;
            }

            return value;
        }
    }
}
