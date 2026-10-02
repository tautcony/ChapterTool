using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

/// <summary>
/// Imports DVD chapter data from IFO files.
/// </summary>
public sealed class IfoChapterImporter : IChapterImporter
{
    private const int DvdSectorSize = 2048;
    private const int PgcTableEntrySize = 8;
    private const int PgcHeaderSize = 0xEC;
    private const int CellPlaybackEntrySize = 0x18;

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "dvd-ifo";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".ifo",
        ".bup"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        Stream? ownedStream = null;
        try
        {
            var resolvedPath = ResolveDiscInputPath(request.Path);
            var resolvedRequest = string.Equals(resolvedPath, request.Path, StringComparison.Ordinal)
                ? request
                : request with { Path = resolvedPath };
            var stream = await OpenImportStreamAsync(resolvedRequest, cancellationToken);
            if (stream is null)
            {
                return ChapterImportResult.Failed(PortableInputReader.TooLargeDiagnostic());
            }

            ownedStream = ReferenceEquals(stream, request.Content) ? null : stream;
            var signature = System.Text.Encoding.ASCII.GetString(ReadBlock(stream, 0, 12));
            List<ChapterDiagnostic> diagnostics = [];
            using var navTiming = request.Content is null && signature == "DVDVIDEO-VTS"
                ? DvdNavTimingReader.Open(resolvedPath, cancellationToken)
                : null;
            var entries = string.Equals(signature, "DVDVIDEO-VMG", StringComparison.Ordinal)
                ? await GetVmgEntriesAsync(resolvedRequest, stream, cancellationToken, diagnostics)
                : GetStreams(request.Path, stream, navTiming, diagnostics, cancellationToken)
                    .Select((info, index) => CreateImportEntry(
                        request.Path,
                        info,
                        index + 1,
                        info.Title,
                        request.Content is null ? DvdIfoMetadataReader.ReadTitleSetTracks(request.Path) : [],
                        FindVobFiles(request.Path, info.SourceName)))
                    .ToList();
            if (entries.Count == 0)
            {
                return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.NoChaptersFound, "No DVD chapters were parsed."));
            }

            return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], diagnostics);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or EndOfStreamException or UnauthorizedAccessException)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.InvalidIfo, exception.Message));
        }
        finally
        {
            if (ownedStream is not null)
            {
                await ownedStream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Executes the GetStreams operation.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <returns>The operation result.</returns>
    public static IReadOnlyList<ChapterSet> GetStreams(string path)
    {
        using var stream = File.OpenRead(path);
        using var navTiming = DvdNavTimingReader.Open(path, CancellationToken.None);
        return GetStreams(path, stream, navTiming);
    }

    private static IReadOnlyList<ChapterSet> GetStreams(string path, Stream stream, DvdNavTimingReader? navTiming = null,
        List<ChapterDiagnostic>? diagnostics = null, CancellationToken cancellationToken = default)
    {
        var signature = System.Text.Encoding.ASCII.GetString(ReadBlock(stream, 0, 12));
        if (!string.Equals(signature, "DVDVIDEO-VTS", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Only DVD Video Title Set IFO files (VTS_XX_0.IFO) are supported.");
        }

        var pttTable = GetPttTable(stream);
        if (pttTable.Titles.Count == 0)
        {
            return [];
        }

        var pgcTable = GetPgcTable(stream);
        var pgcInfo = new Dictionary<int, PgcPlaybackInfo>();
        var streams = new List<ChapterSet>();
        for (var titleIndex = 0; titleIndex < pttTable.Titles.Count; titleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var title = pttTable.Titles[titleIndex];
            var chapterSets = new List<(PttEntry Entry, PgcPlaybackInfo Pgc)>();
            foreach (var entry in title.Chapters)
            {
                if (entry.PgcNumber < 1 || entry.PgcNumber > pgcTable.Count)
                {
                    throw new InvalidDataException("VTS PTT references an unknown PGC.");
                }

                if (!pgcInfo.TryGetValue(entry.PgcNumber, out var playbackInfo))
                {
                    playbackInfo = GetPgcPlaybackInfo(stream, pgcTable, entry.PgcNumber, navTiming);
                    pgcInfo.Add(entry.PgcNumber, playbackInfo);
                }

                if (entry.ProgramNumber < 1 || entry.ProgramNumber > playbackInfo.ProgramStartFrames.Count)
                {
                    throw new InvalidDataException("VTS PTT references an unknown program.");
                }

                chapterSets.Add((entry, playbackInfo));
            }

            var set = CreateTitleChapterSet(path, titleIndex + 1, chapterSets);
            streams.Add(FinalizeTiming(path, set, chapterSets.All(chapter => chapter.Pgc.UsesNavTiming), diagnostics));
        }

        return streams;
    }

    private static async ValueTask<List<ChapterImportEntry>> GetVmgEntriesAsync(
        ChapterImportRequest request,
        Stream stream,
        CancellationToken cancellationToken,
        List<ChapterDiagnostic> diagnostics)
    {
        if (request.Content is not null)
        {
            throw new InvalidDataException("Importing VIDEO_TS.IFO requires access to its sibling VTS IFO files; byte-stream imports cannot provide that disc context.");
        }

        var titles = GetVmgTitleTable(stream);
        var root = Path.GetDirectoryName(Path.GetFullPath(request.Path)) ?? Directory.GetCurrentDirectory();
        var byTitleSet = new Dictionary<int, IReadOnlyList<ChapterSet>>();
        var tracksByTitleSet = new Dictionary<int, IReadOnlyList<ChapterImportMediaTrack>>();
        var entries = new List<ChapterImportEntry>(titles.Count);
        for (var titleIndex = 0; titleIndex < titles.Count; titleIndex++)
        {
            var title = titles[titleIndex];
            cancellationToken.ThrowIfCancellationRequested();
            request.ProgressReporter?.Report(new ChapterImportProgress(
                ChapterImportProgressPhase.ParsingChapters,
                0.1 + ((titleIndex + 1) * 0.85 / titles.Count),
                $"Title {title.DiscTitleNumber}",
                titleIndex + 1,
                titles.Count));
            if (!byTitleSet.TryGetValue(title.TitleSetNumber, out var titleSet))
            {
                var vtsPath = ResolveTitleSetInfoPath(root, title.TitleSetNumber);
                if (!File.Exists(vtsPath))
                {
                    throw new InvalidDataException($"VMG title references missing title-set IFO '{Path.GetFileName(vtsPath)}'.");
                }

                if (!PortableInputPolicy.IsWithinLimit(new FileInfo(vtsPath).Length))
                {
                    throw new InvalidDataException($"Referenced title-set IFO '{Path.GetFileName(vtsPath)}' exceeds the supported input size.");
                }

                using var vtsStream = File.OpenRead(vtsPath);
                using var navTiming = DvdNavTimingReader.Open(vtsPath, cancellationToken);
                try
                {
                    titleSet = GetStreams(vtsPath, vtsStream, navTiming, diagnostics, cancellationToken);
                }
                catch (InvalidDataException exception) when (
                    exception.Message is "Invalid VTS PTT chapter list." or "Invalid VTS PTT chapter reference."
                        or "VTS PTT references an unknown PGC." or "VTS PTT references an unknown program.")
                {
                    titleSet = [];
                }

                byTitleSet.Add(title.TitleSetNumber, titleSet);
                tracksByTitleSet.Add(title.TitleSetNumber, DvdIfoMetadataReader.ReadTitleSetTracks(vtsPath));
            }

            ChapterSet? set = title.TitleWithinTitleSet >= 1 && title.TitleWithinTitleSet <= titleSet.Count
                ? titleSet[title.TitleWithinTitleSet - 1]
                : null;
            if (set is null)
            {
                var vtsPath = ResolveTitleSetInfoPath(root, title.TitleSetNumber);
                set = TryCreateProgramChapterFallback(vtsPath, title, diagnostics, cancellationToken, allowOrdinalPgcRecovery: true)
                    ?? throw new InvalidDataException($"The VTS PTT table for disc title {title.DiscTitleNumber} is invalid, and no matching PGC program map can recover its {title.PartCount} chapters.");
            }
            else if (set.Chapters.Count != title.PartCount)
            {
                var vtsPath = ResolveTitleSetInfoPath(root, title.TitleSetNumber);
                set = TryCreateProgramChapterFallback(vtsPath, title, diagnostics, cancellationToken) ?? set;
            }

            set = set with { Title = $"Title {title.DiscTitleNumber}" };
            var titleSetIfoPath = ResolveTitleSetInfoPath(root, title.TitleSetNumber);
            var mediaFiles = FindVobFiles(titleSetIfoPath, set.SourceName);
            entries.Add(CreateImportEntry(request.Path, set, title.DiscTitleNumber, set.Title, tracksByTitleSet[title.TitleSetNumber], mediaFiles, title.DiscTitleNumber));
        }

        diagnostics.Add(DvdIfoMetadataReader.CreateDiscLoadedDiagnostic(request.Path, entries));
        return entries;
    }

    private static ChapterImportEntry CreateImportEntry(
        string path,
        ChapterSet info,
        int entryNumber,
        string? name,
        IReadOnlyList<ChapterImportMediaTrack>? mediaTracks = null,
        IReadOnlyList<ReferencedMediaFile>? mediaFiles = null,
        int? discTitleNumber = null) =>
        new(
            $"title-{entryNumber}",
            ChapterImportDisplay.FormatDvdOption(name ?? $"Title {entryNumber}", info, mediaFiles),
            info,
            CanCombine: true,
            ReferencedMediaFiles: mediaFiles ?? [],
            MediaTracks: mediaTracks ?? [],
            ImportDisplayName: ImportDisplayName(path, info, mediaFiles),
            DiscTitleNumber: discTitleNumber);

    private static IReadOnlyList<ReferencedMediaFile> FindVobFiles(string ifoPath, string? titleSourceName)
    {
        if (string.IsNullOrWhiteSpace(titleSourceName))
        {
            return [];
        }

        var fileStem = Path.GetFileNameWithoutExtension(titleSourceName);
        var lastSeparator = fileStem.LastIndexOf('_');
        if (lastSeparator < 0)
        {
            return [];
        }

        var vtsName = fileStem[..lastSeparator];
        var directory = Path.GetDirectoryName(Path.GetFullPath(ifoPath)) ?? Directory.GetCurrentDirectory();
        var files = Enumerable.Range(1, 9)
            .Select(number => Path.Combine(directory, $"{vtsName}_{number}.VOB"))
            .Where(File.Exists)
            .Select(path => new ReferencedMediaFile(Path.GetFileName(path), Path.GetFileName(path), path))
            .ToArray();
        return files.Length > 0
            ? files
            : [new ReferencedMediaFile($"{vtsName}_1.VOB", $"{vtsName}_1.VOB")];
    }

    private static IReadOnlyList<VmgTitle> GetVmgTitleTable(Stream stream)
    {
        var sectorOffset = ToInt32(ReadBlock(stream, 0xC4, 4));
        if (sectorOffset == 0)
        {
            throw new InvalidDataException("The VMG IFO does not contain a title search pointer table.");
        }

        var tableOffset = checked((long)sectorOffset * DvdSectorSize);
        var header = ReadBlock(stream, tableOffset, 8);
        var count = ToUInt16(header);
        var lastByte = ToInt32(header.AsSpan(4, 4).ToArray());
        var tableEnd = checked(tableOffset + lastByte + 1L);
        if (count is 0 or > 99 || tableEnd > stream.Length || 8L + count * 12L > lastByte + 1L)
        {
            throw new InvalidDataException("Invalid VMG title search pointer table bounds.");
        }

        var titles = new List<VmgTitle>(count);
        for (var index = 0; index < count; index++)
        {
            var entry = ReadBlock(stream, tableOffset + 8L + index * 12L, 12);
            var titleSet = entry[6];
            var titleWithinSet = entry[7];
            var angleCount = entry[1];
            var partCount = ToUInt16(entry.AsSpan(2, 2).ToArray());
            if (titleSet is 0 or > 99 || titleWithinSet == 0 || angleCount is 0 or > 9 || partCount == 0)
            {
                throw new InvalidDataException("Invalid VMG title-to-title-set reference.");
            }

            titles.Add(new VmgTitle(index + 1, titleSet, titleWithinSet, partCount));
        }

        return titles;
    }

    private static ChapterSet? TryCreateProgramChapterFallback(string path, VmgTitle title,
        List<ChapterDiagnostic> diagnostics, CancellationToken cancellationToken, bool allowOrdinalPgcRecovery = false)
    {
        using var stream = File.OpenRead(path);
        using var navTiming = DvdNavTimingReader.Open(path, cancellationToken);
        var signature = System.Text.Encoding.ASCII.GetString(ReadBlock(stream, 0, 12));
        if (!string.Equals(signature, "DVDVIDEO-VTS", StringComparison.Ordinal))
        {
            return null;
        }

        var (firstPtt, pttTitleCount) = GetFirstPttEntry(stream, title.TitleWithinTitleSet);
        var pgcTable = GetPgcTable(stream);
        if (firstPtt.PgcNumber >= 1 && firstPtt.PgcNumber <= pgcTable.Count)
        {
            try
            {
                var playback = GetPgcPlaybackInfo(stream, pgcTable, firstPtt.PgcNumber, navTiming);
                if (playback.ProgramStartFrames.Count == title.PartCount && firstPtt.ProgramNumber is 0 or 1)
                {
                    return FinalizeTiming(path, CreateProgramChapterSet(path, title, playback), playback.UsesNavTiming, diagnostics);
                }
            }
            catch (InvalidDataException)
            {
                // A malformed PTT reference must not prevent the bounded ordinal-PGC recovery below.
            }
        }

        if (!allowOrdinalPgcRecovery || pttTitleCount != pgcTable.Count || title.TitleWithinTitleSet > pgcTable.Count)
        {
            return null;
        }

        var ordinalPlayback = GetPgcPlaybackInfo(stream, pgcTable, title.TitleWithinTitleSet, navTiming);
        return ordinalPlayback.ProgramStartFrames.Count == title.PartCount
            ? FinalizeTiming(path, CreateProgramChapterSet(path, title, ordinalPlayback), ordinalPlayback.UsesNavTiming, diagnostics)
            : null;
    }

    private static ChapterSet CreateProgramChapterSet(string path, VmgTitle title, PgcPlaybackInfo playback)
    {
        TimeSpan Convert(long value) => playback.UsesNavTiming ? FromPts(value) : FromDvdFrames(value, playback.IsNtsc);
        var chapters = (playback.NavProgramStartPts ?? playback.ProgramStartFrames)
            .Select((start, index) => new Chapter(index + 1, Convert(start), $"Chapter {index + 1:D2}"))
            .ToArray();
        var sourceName = GetTitleSourceName(path, title.TitleWithinTitleSet);
        return new ChapterSet(
            sourceName,
            sourceName,
            ChapterImportFormat.DvdIfo,
            playback.IsNtsc ? 30000d / 1001d : 25d,
            Convert(playback.NavDurationPts ?? playback.DurationFrames),
            chapters);
    }

    private static (PttEntry Entry, int TitleCount) GetFirstPttEntry(Stream stream, int titleNumber)
    {
        var layout = GetPttLayout(stream);
        if (layout is null)
        {
            throw new InvalidDataException("The VTS IFO does not contain a PTT table for chapter recovery.");
        }

        var titleCount = layout.TitleOffsets.Length;
        if (titleNumber < 1 || titleNumber > titleCount)
        {
            throw new InvalidDataException("The VMG title references an unknown VTS PTT title.");
        }

        var titleOffset = layout.TitleOffsets[titleNumber - 1];
        var titleStart = checked(layout.Offset + titleOffset);
        if (titleStart + 4 > layout.GetTitleEnd(titleOffset))
        {
            throw new InvalidDataException("The VTS PTT title has no chapters for chapter recovery.");
        }

        var record = ReadBlock(stream, titleStart, 4);
        var pgcNumber = ToUInt16(record.AsSpan(0, 2).ToArray());
        var programNumber = ToUInt16(record.AsSpan(2, 2).ToArray());
        return (new PttEntry(pgcNumber, programNumber), titleCount);
    }

    /// <summary>
    /// Executes the BcdToInt operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    public static int BcdToInt(byte value) => (0xFF & (value >> 4)) * 10 + (value & 0x0F);

    /// <summary>
    /// Converts the playback duration recorded in the IFO.
    /// </summary>
    /// <param name="hour">The BCD-encoded DVD playback hour.</param>
    /// <param name="minute">The BCD-encoded DVD playback minute.</param>
    /// <param name="second">The BCD-encoded DVD playback second.</param>
    /// <param name="frameByte">The DVD frame byte containing frame count and frame-rate flags.</param>
    /// <param name="isNtsc">Returns whether the playback time uses the NTSC frame-rate flag.</param>
    /// <returns>The duration using the legacy nominal-frame interpretation of IFO timecodes.</returns>
    public static TimeSpan ConvertDvdPlaybackTime(byte hour, byte minute, byte second, byte frameByte, out bool isNtsc)
    {
        var frames = ReadDvdFrameCount(hour, minute, second, frameByte, out isNtsc);
        return FromDvdFrames(frames, isNtsc);
    }

    private static long ReadDvdFrameCount(byte hour, byte minute, byte second, byte frameByte, out bool isNtsc)
    {
        var frameRateCode = frameByte >> 6;
        isNtsc = frameRateCode == 0x03;
        var nominalRate = frameRateCode switch
        {
            0x01 => 25,
            0x03 => 30,
            _ => throw new InvalidDataException("Invalid DVD playback frame-rate code.")
        };
        var hours = ReadBcd(hour, "hour");
        var minutes = ReadBcd(minute, "minute");
        var seconds = ReadBcd(second, "second");
        var frames = ReadBcd((byte)(frameByte & 0x3F), "frame");
        if (minutes > 59 || seconds > 59)
        {
            throw new InvalidDataException("Invalid DVD playback time.");
        }

        if (frames >= nominalRate)
        {
            throw new InvalidDataException("Invalid DVD playback frame count.");
        }

        var wholeSeconds = hours * 3600 + minutes * 60 + seconds;
        return (long)wholeSeconds * nominalRate + frames;
    }

    private static int ReadBcd(byte value, string field)
    {
        var high = value >> 4;
        var low = value & 0x0F;
        if (high > 9 || low > 9)
        {
            throw new InvalidDataException($"Invalid DVD playback {field} value.");
        }

        return high * 10 + low;
    }

    private static ChapterSet CreateTitleChapterSet(
        string path,
        int titleNumber,
        IReadOnlyList<(PttEntry Entry, PgcPlaybackInfo Pgc)> titleChapters)
    {
        if (titleChapters.Count == 0)
        {
            throw new InvalidDataException("VTS title has no PTT chapters.");
        }

        if (titleChapters.Any(chapter => chapter.Pgc.IsNtsc != titleChapters[0].Pgc.IsNtsc))
        {
            throw new InvalidDataException("A VTS title contains mixed DVD playback frame rates.");
        }

        var useNav = titleChapters.All(chapter => chapter.Pgc.UsesNavTiming);
        long Start((PttEntry Entry, PgcPlaybackInfo Pgc) chapter) =>
            (useNav ? chapter.Pgc.NavProgramStartPts! : chapter.Pgc.ProgramStartFrames)[chapter.Entry.ProgramNumber - 1];
        long Duration(PgcPlaybackInfo pgc) => useNav ? pgc.NavDurationPts!.Value : pgc.DurationFrames;
        TimeSpan Convert(long value) => useNav ? FromPts(value) : FromDvdFrames(value, titleChapters[0].Pgc.IsNtsc);
        var starts = new long[titleChapters.Count];
        for (var index = 1; index < titleChapters.Count; index++)
        {
            var previous = titleChapters[index - 1];
            var current = titleChapters[index];
            var previousStart = Start(previous);
            var currentStart = Start(current);
            var elapsed = previous.Entry.PgcNumber == current.Entry.PgcNumber
                ? currentStart - previousStart
                : Duration(previous.Pgc) - previousStart + currentStart;
            if (elapsed < 0)
            {
                throw new InvalidDataException("VTS PTT chapters are not in playback order.");
            }

            starts[index] = starts[index - 1] + elapsed;
        }

        var last = titleChapters[^1];
        var lastStart = Start(last);
        var duration = starts[^1] + Duration(last.Pgc) - lastStart;
        if (duration <= 0)
        {
            throw new InvalidDataException("VTS title has no positive playback duration.");
        }

        var chapters = starts.Select((start, index) => new Chapter(index + 1, Convert(start), $"Chapter {index + 1:D2}")).ToArray();
        var sourceName = GetTitleSourceName(path, titleNumber);
        return new ChapterSet(
            sourceName,
            sourceName,
            ChapterImportFormat.DvdIfo,
            titleChapters[0].Pgc.IsNtsc ? 30000d / 1001d : 25d,
            Convert(duration),
            chapters);
    }

    private static string GetTitleSourceName(string path, int titleNumber)
    {
        var sourceName = Path.GetFileNameWithoutExtension(path);
        if (sourceName.Count(static ch => ch == '_') == 2)
        {
            var last = sourceName.LastIndexOf('_');
            return $"{sourceName[..last]}_{titleNumber}";
        }

        return $"{sourceName}_{titleNumber}";
    }

    private static string ImportDisplayName(
        string sourcePath,
        ChapterSet info,
        IReadOnlyList<ReferencedMediaFile>? mediaFiles)
    {
        var values = new List<string>
        {
            Path.GetFileName(sourcePath.Replace('\\', Path.DirectorySeparatorChar)),
            info.SourceName ?? string.Empty,
            info.Duration.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
        };
        var resolvedMediaFiles = mediaFiles?.Where(static file => file.AbsolutePath is not null).ToArray();
        if (resolvedMediaFiles is { Length: > 0 })
        {
            values.Add(string.Join("+", resolvedMediaFiles.Select(static file => file.DisplayName)));
        }

        return string.Join(", ", values.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string ResolveDiscInputPath(string inputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            return inputPath;
        }

        var fullPath = Path.GetFullPath(inputPath);
        foreach (var extension in new[] { ".IFO", ".BUP" })
        {
            var directPath = Path.Combine(fullPath, $"VIDEO_TS{extension}");
            if (File.Exists(directPath))
            {
                return directPath;
            }
        }

        foreach (var extension in new[] { ".IFO", ".BUP" })
        {
            var nestedPath = Path.Combine(fullPath, "VIDEO_TS", $"VIDEO_TS{extension}");
            if (File.Exists(nestedPath))
            {
                return nestedPath;
            }
        }

        return inputPath;
    }

    private static string ResolveTitleSetInfoPath(string root, int titleSetNumber)
    {
        var baseName = $"VTS_{titleSetNumber:D2}_0";
        var primaryPath = Path.Combine(root, $"{baseName}.IFO");
        return File.Exists(primaryPath) ? primaryPath : Path.Combine(root, $"{baseName}.BUP");
    }

    private static PgcPlaybackInfo GetPgcPlaybackInfo(Stream stream, PgcTable table, int programChain, DvdNavTimingReader? navTiming = null)
    {
        var chainOffset = GetChainOffset(stream, table, programChain);
        var pgcStart = checked(table.Offset + chainOffset);
        var header = ReadBlock(stream, pgcStart, PgcHeaderSize);
        var programCount = header[2];
        var cellCount = header[3];
        if (programCount == 0 || cellCount == 0 || programCount > cellCount)
        {
            throw new InvalidDataException("Invalid IFO program or cell count.");
        }

        var playbackMode = header[0xA3];
        if (playbackMode != 0 && (playbackMode & 0x7F) != programCount)
        {
            throw new InvalidDataException("The DVD PGC random or shuffle program count does not match its program map.");
        }

        var programMapOffset = ToUInt16(ReadBlock(stream, pgcStart + 230, 2));
        var cellTableOffset = ToUInt16(ReadBlock(stream, pgcStart + 0xE8, 2));
        var pgcEnd = table.GetPgcEnd(chainOffset);
        if (programMapOffset < PgcHeaderSize || cellTableOffset < PgcHeaderSize ||
            pgcStart + programMapOffset + programCount > pgcEnd ||
            pgcStart + cellTableOffset + cellCount * CellPlaybackEntrySize > pgcEnd)
        {
            throw new InvalidDataException("Invalid IFO program map or cell playback table.");
        }

        var programMap = ReadBlock(stream, pgcStart + programMapOffset, programCount)
            .Select(static cell => (int)cell)
            .ToArray();
        if (programMap.Any(cell => cell < 1 || cell > cellCount) ||
            programMap.Zip(programMap.Skip(1), static (current, next) => current >= next).Any(static invalid => invalid))
        {
            throw new InvalidDataException("Invalid IFO program map cell references.");
        }

        var programStarts = new long[programCount];
        var navStarts = new long[programCount];
        long navDuration = 0;
        var usesNavTiming = navTiming is not null;
        var positionOffset = ToUInt16(header.AsSpan(0xEA, 2).ToArray());
        usesNavTiming &= positionOffset >= PgcHeaderSize && pgcStart + positionOffset + cellCount * 4 <= pgcEnd;
        long duration = 0;
        bool? isNtsc = null;
        for (var currentProgram = 0; currentProgram < programCount; currentProgram++)
        {
            programStarts[currentProgram] = duration;
            navStarts[currentProgram] = navDuration;
            var entryCell = programMap[currentProgram];
            var exitCell = currentProgram + 1 < programCount ? programMap[currentProgram + 1] - 1 : cellCount;

            long programDuration = 0;
            for (var currentCell = entryCell; currentCell <= exitCell; currentCell++)
            {
                var cellStart = cellTableOffset + (currentCell - 1) * CellPlaybackEntrySize;
                var typeBytes = ReadBlock(stream, pgcStart + cellStart, 4);
                var cellType = typeBytes[0] >> 6;
                if (cellType is 0x00 or 0x01)
                {
                    var timeBytes = ReadBlock(stream, pgcStart + cellStart + 4, 4);
                    programDuration += ReadDvdFrameCount(timeBytes[0], timeBytes[1], timeBytes[2], timeBytes[3], out var cellIsNtsc);
                    if (isNtsc.HasValue && isNtsc.Value != cellIsNtsc)
                    {
                        throw new InvalidDataException("A PGC contains mixed DVD playback frame rates.");
                    }

                    isNtsc = cellIsNtsc;
                    if (usesNavTiming)
                    {
                        var cell = ReadBlock(stream, pgcStart + cellStart, CellPlaybackEntrySize);
                        var position = ReadBlock(stream, pgcStart + positionOffset + (currentCell - 1) * 4, 4);

                        // Interleaved angles need DVD navigation selection; never scan other angles as this one.
                        if ((cell[0] & 0x30) != 0 || !navTiming!.TryGetCellDuration(
                            ToInt32(cell.AsSpan(8, 4).ToArray()), ToInt32(cell.AsSpan(16, 4).ToArray()),
                            ToInt32(cell.AsSpan(20, 4).ToArray()), (ushort)ToUInt16(position), position[3], out var cellPts))
                        {
                            usesNavTiming = false;
                        }
                        else
                        {
                            navDuration += cellPts;
                        }
                    }
                }
            }

            duration += programDuration;
        }

        if (duration <= 0 || !isNtsc.HasValue)
        {
            throw new InvalidDataException("DVD PGC has no usable playback cells.");
        }

        return usesNavTiming
            ? new PgcPlaybackInfo(programStarts, duration, isNtsc.Value, navStarts, navDuration)
            : new PgcPlaybackInfo(programStarts, duration, isNtsc.Value);
    }

    private static PgcTable GetPgcTable(Stream stream)
    {
        var sectorOffset = ToInt32(ReadBlock(stream, 0xCC, 4));
        if (sectorOffset == 0)
        {
            throw new InvalidDataException("The VTS IFO does not contain a PGC table.");
        }

        var tableOffset = checked((long)sectorOffset * DvdSectorSize);
        var tableHeader = ReadBlock(stream, tableOffset, 8);
        var count = ToUInt16(tableHeader);
        var lastByte = ToInt32(tableHeader.AsSpan(4, 4).ToArray());
        var tableEnd = checked(tableOffset + lastByte + 1L);
        if (count is 0 or > 999 || tableEnd > stream.Length ||
            8L + count * PgcTableEntrySize > lastByte + 1L)
        {
            throw new InvalidDataException("Invalid VTS PGC table bounds.");
        }

        var pgcOffsets = new uint[count];
        for (var index = 0; index < count; index++)
        {
            var entry = ReadBlock(stream, tableOffset + 8L + index * PgcTableEntrySize, PgcTableEntrySize);
            pgcOffsets[index] = ToInt32(entry.AsSpan(4, 4).ToArray());
            if (pgcOffsets[index] < 8L + count * PgcTableEntrySize ||
                tableOffset + pgcOffsets[index] + PgcHeaderSize > tableEnd)
            {
                throw new InvalidDataException("Invalid VTS PGC offset.");
            }
        }

        if (pgcOffsets.Distinct().Count() != count)
        {
            throw new InvalidDataException("Duplicate VTS PGC offsets.");
        }

        return new PgcTable(tableOffset, tableEnd, pgcOffsets);
    }

    private static PttTable GetPttTable(Stream stream)
    {
        var layout = GetPttLayout(stream);
        if (layout is null)
        {
            return new PttTable([]);
        }

        var titles = new List<PttTitle>(layout.TitleOffsets.Length);
        foreach (var titleOffset in layout.TitleOffsets)
        {
            var titleStart = checked(layout.Offset + titleOffset);
            var byteCount = layout.GetTitleEnd(titleOffset) - titleStart;

            // Each on-disc PTT record is a PGC/program pair. There is no count prefix.
            var pttCount = byteCount / 4;
            if (pttCount == 0 || pttCount > 999 || byteCount % 4 != 0)
            {
                throw new InvalidDataException("Invalid VTS PTT chapter list.");
            }

            var chapters = new List<PttEntry>((int)pttCount);
            for (var index = 0; index < pttCount; index++)
            {
                var record = ReadBlock(stream, titleStart + index * 4L, 4);
                var pgcNumber = ToUInt16(record.AsSpan(0, 2).ToArray());
                var programNumber = ToUInt16(record.AsSpan(2, 2).ToArray());
                if (pgcNumber == 0 || programNumber == 0)
                {
                    throw new InvalidDataException("Invalid VTS PTT chapter reference.");
                }

                chapters.Add(new PttEntry(pgcNumber, programNumber));
            }

            titles.Add(new PttTitle(chapters));
        }

        return new PttTable(titles);
    }

    private static PttLayout? GetPttLayout(Stream stream)
    {
        var sectorOffset = ToInt32(ReadBlock(stream, 0xC8, 4));
        if (sectorOffset == 0)
        {
            return null;
        }

        var tableOffset = checked((long)sectorOffset * DvdSectorSize);
        var header = ReadBlock(stream, tableOffset, 8);
        var titleCount = ToUInt16(header);
        var lastByte = ToInt32(header.AsSpan(4, 4).ToArray());
        var tableEnd = checked(tableOffset + lastByte + 1L);
        if (titleCount > 99 || tableEnd > stream.Length || 8L + titleCount * 4 > lastByte + 1L)
        {
            throw new InvalidDataException("Invalid VTS PTT table bounds.");
        }

        var titleOffsets = new uint[titleCount];
        for (var index = 0; index < titleCount; index++)
        {
            titleOffsets[index] = ToInt32(ReadBlock(stream, tableOffset + 8L + index * 4, 4));
            if (titleOffsets[index] < 8L + titleCount * 4 || tableOffset + titleOffsets[index] > tableEnd)
            {
                throw new InvalidDataException("Invalid VTS PTT title offset.");
            }
        }

        if (titleOffsets.Distinct().Count() != titleCount)
        {
            throw new InvalidDataException("Duplicate VTS PTT title offsets.");
        }

        return new PttLayout(tableOffset, tableEnd, titleOffsets);
    }

    private static async ValueTask<Stream?> OpenImportStreamAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return File.OpenRead(request.Path);
        }

        if (request.Content.CanSeek)
        {
            try
            {
                if (!PortableInputPolicy.IsWithinLimit(request.Content.Length))
                {
                    return null;
                }
            }
            catch (NotSupportedException)
            {
            }

            request.Content.Position = 0;
            return request.Content;
        }

        var copy = await PortableInputPolicy.CopyToBoundedMemoryAsync(request.Content, cancellationToken);
        return copy.Exceeded ? null : copy.Stream;
    }

    private static uint GetChainOffset(Stream stream, PgcTable table, int programChain) =>
        table.PgcOffsets[programChain - 1];

    private static byte[] ReadBlock(Stream stream, long position, int count)
    {
        if (position < 0 || count < 0 || position > stream.Length - count)
        {
            throw new InvalidDataException("Invalid IFO file structure.");
        }

        stream.Position = position;
        return stream.ReadExactBytes(count);
    }

    internal static int ToUInt16(byte[] bytes) => (bytes[0] << 8) | bytes[1];

    private static uint ToInt32(byte[] bytes) => (uint)((bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3]);

    private sealed record PgcTable(long Offset, long End, uint[] PgcOffsets)
    {
        public int Count => PgcOffsets.Length;

        public long GetPgcEnd(uint offset) => PgcOffsets
            .Where(candidate => candidate > offset)
            .Select(candidate => checked(Offset + candidate))
            .Append(End)
            .Min();
    }

    private sealed record PttTable(IReadOnlyList<PttTitle> Titles);

    private sealed record PttLayout(long Offset, long End, uint[] TitleOffsets)
    {
        public long GetTitleEnd(uint offset) => TitleOffsets
            .Where(candidate => candidate > offset)
            .Select(candidate => checked(Offset + candidate))
            .Append(End)
            .Min();
    }

    private sealed record PttTitle(IReadOnlyList<PttEntry> Chapters);

    private sealed record PttEntry(int PgcNumber, int ProgramNumber);

    private sealed record VmgTitle(int DiscTitleNumber, int TitleSetNumber, int TitleWithinTitleSet, int PartCount);

    private sealed record PgcPlaybackInfo(IReadOnlyList<long> ProgramStartFrames, long DurationFrames, bool IsNtsc,
        IReadOnlyList<long>? NavProgramStartPts = null, long? NavDurationPts = null)
    {
        public bool UsesNavTiming => NavProgramStartPts is not null;
    }

    private static TimeSpan FromDvdFrames(long frames, bool isNtsc) => TimeSpan.FromTicks((long)decimal.Round(
        isNtsc ? frames * (decimal)TimeSpan.TicksPerSecond * 1001 / 30000 : frames * (decimal)TimeSpan.TicksPerSecond / 25));

    private static TimeSpan FromPts(long pts) => TimeSpan.FromTicks((long)decimal.Round(pts * (decimal)TimeSpan.TicksPerSecond / 90_000));

    private static ChapterSet FinalizeTiming(string path, ChapterSet set, bool usesNavTiming, List<ChapterDiagnostic>? diagnostics)
    {
        var source = $"{Path.GetFileName(path)} ({set.SourceName})";
        var code = usesNavTiming ? ChapterDiagnosticCode.IfoTimingFromNav : ChapterDiagnosticCode.IfoTimingEstimated;
        if (diagnostics is not null && !diagnostics.Any(item => item.Code == code && Equals(item.Arguments?["source"], source)))
        {
            diagnostics.Add(new ChapterDiagnostic(usesNavTiming ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning, code,
                usesNavTiming
                    ? $"DVD chapter times for {source} were read from VOB navigation timestamps."
                    : $"DVD chapter times for {source} use IFO duration estimates; VOB navigation timestamps could not be verified. Original times were preserved.",
                Arguments: new Dictionary<string, object?> { ["source"] = source }));
        }

        return set;
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

}
