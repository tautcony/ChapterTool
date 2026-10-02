using System.Buffers.Binary;

namespace ChapterTool.Core.Importing.Disc;

/// <summary>Reads presentation times from title VOB navigation packs.</summary>
internal sealed class DvdNavTimingReader : IDisposable
{
    private const int SectorSize = 2048;
    private const int MaximumNavPacks = 200_000;
    private readonly List<(FileStream Stream, long Start, long End)> files = [];
    private readonly Dictionary<(uint First, uint LastVobu, uint Last, ushort Vob, byte Cell), long?> cells = [];
    private readonly CancellationToken cancellationToken;
    private readonly byte[] sectorBuffer = new byte[SectorSize];
    private int packsRead;

    private DvdNavTimingReader(CancellationToken cancellationToken) => this.cancellationToken = cancellationToken;

    public static DvdNavTimingReader? Open(string ifoPath, CancellationToken cancellationToken)
    {
        var stem = Path.GetFileNameWithoutExtension(ifoPath);
        if (!stem.StartsWith("VTS_", StringComparison.OrdinalIgnoreCase) || !stem.EndsWith("_0", StringComparison.Ordinal))
        {
            return null;
        }

        var reader = new DvdNavTimingReader(cancellationToken);
        try
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(ifoPath))!;
            long end = 0;
            var missing = false;
            for (var number = 1; number <= 9; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(root, $"{stem[..^1]}{number}.VOB");
                if (!File.Exists(path))
                {
                    missing = true;
                    continue;
                }

                if (missing)
                {
                    reader.Dispose();
                    return null;
                }

                var stream = File.OpenRead(path);
                reader.files.Add((stream, end, end + stream.Length / SectorSize));
                if (stream.Length == 0 || stream.Length % SectorSize != 0)
                {
                    reader.Dispose();
                    return null;
                }

                end += stream.Length / SectorSize;
            }

            if (reader.files.Count > 0)
            {
                return reader;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Optional VOB access must not prevent an IFO-only import.
        }
        catch
        {
            reader.Dispose();
            throw;
        }

        reader.Dispose();
        return null;
    }

    public bool TryGetCellDuration(uint first, uint lastVobu, uint last, ushort vob, byte cell, out long ptsDuration)
    {
        var key = (first, lastVobu, last, vob, cell);
        if (!cells.TryGetValue(key, out var cached))
        {
            cached = ReadCell(first, lastVobu, last, vob, cell);
            cells.Add(key, cached);
        }

        ptsDuration = cached ?? 0;
        return cached.HasValue;
    }

    private long? ReadCell(uint first, uint lastVobu, uint last, ushort vob, byte cell)
    {
        if (first > lastVobu || lastVobu > last || vob == 0 || cell == 0)
        {
            return null;
        }

        try
        {
            long duration = 0;
            uint? previousEnd = null;
            long address = first;
            while (address <= lastVobu)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++packsRead > MaximumNavPacks || !ReadSector(address))
                {
                    return null;
                }

                var data = sectorBuffer.AsSpan();

                // MPEG-2 pack, system header, PCI private_stream_2, and DSI private_stream_2.
                if (Read32(data, 0) != 0x000001BA || (data[4] & 0xC0) != 0x40 ||
                    (data[13] & 7) != 0 || Read32(data, 14) != 0x000001BB ||
                    Read16(data, 18) != 18 || Read32(data, 38) != 0x000001BF ||
                    Read16(data, 42) != 980 || data[44] != 0 ||
                    Read32(data, 1024) != 0x000001BF ||
                    Read16(data, 1028) != 1018 || data[1030] != 1 ||
                    Read32(data, 45) != address || Read32(data, 1035) != address ||
                    Read16(data, 1055) != vob || data[1058] != cell)
                {
                    return null;
                }

                var start = Read32(data, 57);
                var end = Read32(data, 61);
                var elapsed = unchecked(end - start); // NAV PTM wraps at 32 bits.
                var endAddress = address + Read32(data, 1039);
                if (elapsed is 0 or > 900_000 || endAddress > last ||
                    (previousEnd.HasValue && previousEnd.Value != start))
                {
                    return null;
                }

                duration += elapsed;
                if (address == lastVobu)
                {
                    return endAddress == last && (Read32(data, 1345) & 0x7FFF_FFFF) == 0x3FFF_FFFF ? duration : null;
                }

                previousEnd = end;
                var next = Read32(data, 1345) & 0x7FFF_FFFF;
                if (next == 0 || next == 0x3FFF_FFFF || address + next <= endAddress || address + next > lastVobu)
                {
                    return null;
                }

                address += next;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A partial or unreadable VOB falls back as one complete PGC.
        }

        return null;
    }

    private bool ReadSector(long address)
    {
        foreach (var file in files)
        {
            if (address < file.Start || address >= file.End)
            {
                continue;
            }

            file.Stream.Position = (address - file.Start) * SectorSize;
            file.Stream.ReadExactly(sectorBuffer);
            return true;
        }

        return false;
    }

    private static ushort Read16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint Read32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);

    public void Dispose()
    {
        foreach (var file in files)
        {
            file.Stream.Dispose();
        }
    }
}
