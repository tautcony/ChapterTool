using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

public sealed class MplsChapterImporter : IChapterImporter
{
    private static readonly double[] FrameRates =
    [
        0,
        24000d / 1001d,
        24,
        25,
        30000d / 1001d,
        0,
        50,
        60000d / 1001d
    ];

    public string Id => "mpls";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mpls"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.Content ?? File.OpenRead(request.Path);
        try
        {
            var parsed = MplsPlaylistFile.Read(stream);
            var options = parsed.PlayList.PlayItems.Select((playItem, index) => ToOption(request.Path, playItem, parsed.PlayListMark.Marks, index)).ToList();
            return new ChapterImportResult(true, [new ChapterInfoGroup(request.Path, options)], []);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidMpls", exception.Message));
        }
    }

    public static TimeSpan PtsToTime(uint pts)
    {
        var total = pts / 45000M;
        var seconds = Math.Floor(total);
        var milliseconds = Math.Round((total - seconds) * 1000M, MidpointRounding.AwayFromZero);
        return new TimeSpan(0, 0, 0, (int)seconds, (int)milliseconds);
    }

    public static ChapterInfo ReadPlaylistInfo(
        string path,
        string title = "",
        string? sourceName = null,
        int sourceIndex = 0,
        string sourceType = "MPLS",
        TimeSpan? duration = null)
    {
        using var stream = File.OpenRead(path);
        var parsed = MplsPlaylistFile.Read(stream);
        var playItems = parsed.PlayList.PlayItems;
        var chapters = PlaylistChapters(playItems, parsed.PlayListMark.Marks);
        var frameRateCode = playItems
            .SelectMany(static item => item.STNTable.PrimaryVideoStreamEntries)
            .Select(static entry => entry.StreamAttributes.FrameRate ?? 0)
            .FirstOrDefault();
        var totalPts = playItems.Aggregate(0UL, static (sum, item) => sum + item.OUTTime - item.INTime);

        return new ChapterInfo(
            title,
            sourceName ?? string.Join("+", playItems.Select(static item => item.FullName)),
            sourceIndex,
            sourceType,
            frameRateCode < FrameRates.Length ? FrameRates[frameRateCode] : 0,
            duration ?? PtsToTime(checked((uint)Math.Min(totalPts, uint.MaxValue))),
            chapters,
            Tag: path,
            TagType: sourceType);
    }

    private static ChapterSourceOption ToOption(string path, MplsPlayItem playItem, IReadOnlyList<MplsMark> marks, int playItemIndex)
    {
        var matchingMarks = marks
            .Where(mark => mark.MarkType == 0x01 && mark.RefToPlayItemID == playItemIndex)
            .ToList();
        var offset = matchingMarks.Count == 0 ? playItem.INTime : matchingMarks[0].MarkTimeStamp;
        if (playItem.INTime < offset)
        {
            offset = playItem.INTime;
        }

        var chapters = matchingMarks.Count == 0
            ? [new Chapter(1, TimeSpan.Zero, "Chapter 1")]
            : matchingMarks
                .Select((mark, index) => new Chapter(index + 1, PtsToTime(mark.MarkTimeStamp - offset), $"Chapter {index + 1:D2}"))
                .ToList();
        var frameRateCode = playItem.STNTable.PrimaryVideoStreamEntries.FirstOrDefault()?.StreamAttributes.FrameRate ?? 0;
        var info = new ChapterInfo(
            string.Empty,
            playItem.FullName,
            playItemIndex,
            "MPLS",
            frameRateCode < FrameRates.Length ? FrameRates[frameRateCode] : 0,
            PtsToTime(playItem.OUTTime - playItem.INTime),
            chapters,
            Tag: path,
            TagType: "MPLS");
        var refs = playItem.FullName
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(clip => new SourceMediaReference($"{clip}.m2ts", Path.Combine("..", "STREAM", $"{clip}.m2ts")))
            .ToList();
        return new ChapterSourceOption($"clip-{playItemIndex}", $"{playItem.FullName}__{chapters.Count}", info, CanCombine: true, MediaReferences: refs);
    }

    private static List<Chapter> PlaylistChapters(IReadOnlyList<MplsPlayItem> playItems, IReadOnlyList<MplsMark> marks)
    {
        if (playItems.Count == 0)
        {
            return [];
        }

        var playItemStartPts = new ulong[playItems.Count];
        var cursor = 0UL;
        for (var i = 0; i < playItems.Count; i++)
        {
            playItemStartPts[i] = cursor;
            cursor += playItems[i].OUTTime - playItems[i].INTime;
        }

        var chapters = marks
            .Where(static mark => mark.MarkType == 0x01)
            .Where(mark => mark.RefToPlayItemID < playItems.Count)
            .Select(mark =>
            {
                var playItem = playItems[mark.RefToPlayItemID];
                var relativePts = mark.MarkTimeStamp > playItem.INTime ? mark.MarkTimeStamp - playItem.INTime : 0;
                return playItemStartPts[mark.RefToPlayItemID] + relativePts;
            })
            .Distinct()
            .Order()
            .Select((pts, index) => new Chapter(
                index + 1,
                PtsToTime(checked((uint)Math.Min(pts, uint.MaxValue))),
                $"Chapter {index + 1:D2}"))
            .ToList();

        return chapters.Count == 0
            ? [new Chapter(1, TimeSpan.Zero, "Chapter 01")]
            : chapters;
    }
}
