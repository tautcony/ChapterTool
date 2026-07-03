using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

public sealed partial class IfoChapterImporter : IChapterImporter
{
    public string Id => "dvd-ifo";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".ifo"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        try
        {
            var options = GetStreams(request.Path)
                .Select((info, index) => new ChapterSourceOption(
                    $"pgc-{index}",
                    $"{info.SourceName}__{info.Chapters.Count}",
                    info,
                    CanCombine: true,
                    MediaReferences: [new SourceMediaReference($"{info.SourceName}.VOB", $"{info.SourceName}.VOB")]))
                .ToList();
            if (options.Count == 0)
            {
                return ChapterImportResult.Failed(Error("NoChaptersFound", "No DVD chapters were parsed."));
            }

            return new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, options)], []);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or EndOfStreamException)
        {
            return ChapterImportResult.Failed(Error("InvalidIfo", exception.Message));
        }
    }

    public static IReadOnlyList<ChapterInfo> GetStreams(string path)
    {
        var count = GetPgcCount(path);
        var streams = new List<ChapterInfo>();
        for (var i = 1; i <= count; i++)
        {
            var info = GetChapterInfo(path, i);
            if (info is not null)
            {
                streams.Add(info);
            }
        }

        return streams;
    }

    public static int BcdToInt(byte value) => (0xFF & (value >> 4)) * 10 + (value & 0x0F);

    public static TimeSpan ConvertDvdPlaybackTime(byte hour, byte minute, byte second, byte frameByte, out bool isNtsc)
    {
        var fpsMask = frameByte >> 6;
        isNtsc = fpsMask == 0x03;
        var rawRate = isNtsc ? 30 : 25;
        var rate = isNtsc ? 30000d / 1001d : 25d;
        var frames = BcdToInt((byte)(frameByte & 0x3F));
        var totalFrames = frames + (BcdToInt(hour) * 3600 + BcdToInt(minute) * 60 + BcdToInt(second)) * rawRate;
        return TimeSpan.FromSeconds(totalFrames / rate);
    }

    private static ChapterInfo? GetChapterInfo(string path, int programChain)
    {
        var chapters = GetChapters(path, programChain, out var duration, out var isNtsc);
        if (duration.TotalSeconds < 10)
        {
            return null;
        }

        var sourceName = Path.GetFileNameWithoutExtension(path);
        if (sourceName.Count(static ch => ch == '_') == 2)
        {
            var last = sourceName.LastIndexOf('_');
            sourceName = $"{sourceName[..last]}_{programChain}";
        }

        return new ChapterInfo(
            sourceName,
            sourceName,
            programChain,
            "DVD",
            isNtsc ? 30000d / 1001d : 25d,
            duration,
            chapters);
    }

    private static List<Chapter> GetChapters(string path, int programChain, out TimeSpan duration, out bool isNtsc)
    {
        duration = TimeSpan.Zero;
        isNtsc = true;
        using var stream = File.OpenRead(path);
        var pcgit = GetPcgitPosition(stream);
        var chainOffset = GetChainOffset(stream, pcgit, programChain);
        var programCount = GetNumberOfPrograms(stream, pcgit, chainOffset);
        var chapters = new List<Chapter> { new(1, TimeSpan.Zero, "Chapter 01") };
        var programMapOffset = ToInt16(ReadBlock(stream, pcgit + chainOffset + 230, 2));
        var cellTableOffset = ToInt16(ReadBlock(stream, pcgit + chainOffset + 0xE8, 2));

        for (var currentProgram = 0; currentProgram < programCount; currentProgram++)
        {
            var entryCell = (int)ReadBlock(stream, pcgit + chainOffset + programMapOffset + currentProgram, 1)[0];
            var exitCell = entryCell;
            if (currentProgram < programCount - 1)
            {
                exitCell = ReadBlock(stream, pcgit + chainOffset + programMapOffset + currentProgram + 1, 1)[0] - 1;
            }

            var programDuration = TimeSpan.Zero;
            for (var currentCell = entryCell; currentCell <= exitCell; currentCell++)
            {
                var cellStart = cellTableOffset + (currentCell - 1) * 0x18;
                var typeBytes = ReadBlock(stream, pcgit + chainOffset + cellStart, 4);
                var cellType = typeBytes[0] >> 6;
                if (cellType is 0x00 or 0x01)
                {
                    var timeBytes = ReadBlock(stream, pcgit + chainOffset + cellStart + 4, 4);
                    programDuration += ConvertDvdPlaybackTime(timeBytes[0], timeBytes[1], timeBytes[2], timeBytes[3], out isNtsc);
                }
            }

            duration += programDuration;
            if (currentProgram + 1 < programCount)
            {
                chapters.Add(new Chapter(chapters.Count + 1, duration, $"Chapter {chapters.Count + 1:D2}"));
            }
        }

        return chapters;
    }

    private static int GetPgcCount(string path)
    {
        using var stream = File.OpenRead(path);
        var offset = ToInt32(ReadBlock(stream, 0xCC, 4));
        stream.Position = 2048 * offset + 0x01;
        return stream.ReadByte();
    }

    private static long GetPcgitPosition(Stream stream) => ToInt32(ReadBlock(stream, 0xCC, 4)) * 0x800L;

    private static uint GetChainOffset(Stream stream, long pcgit, int programChain) =>
        ToInt32(ReadBlock(stream, pcgit + 8 * programChain + 4, 4));

    private static int GetNumberOfPrograms(Stream stream, long pcgit, uint chainOffset) =>
        ReadBlock(stream, pcgit + chainOffset + 2, 1)[0];

    private static byte[] ReadBlock(Stream stream, long position, int count)
    {
        if (position < 0 || position + count > stream.Length)
        {
            throw new InvalidDataException("Invalid IFO file structure.");
        }

        stream.Position = position;
        return stream.ReadExactBytes(count);
    }

    private static short ToInt16(byte[] bytes) => (short)((bytes[0] << 8) + bytes[1]);

    private static uint ToInt32(byte[] bytes) => (uint)((bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3]);

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    [GeneratedRegex(@"^VTS_(?<Title>\d+)_0\.IFO$", RegexOptions.IgnoreCase)]
    private static partial Regex IfoNameRegex();
}
