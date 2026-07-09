using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

/// <summary>
/// Imports Blu-ray chapter data from MPLS playlist files.
/// </summary>
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

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "mpls";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mpls"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.Content ?? File.OpenRead(request.Path);
        try
        {
            var parsed = MplsPlaylistFile.Read(stream);
            var entries = parsed.PlayList.PlayItems.Select((playItem, index) => ToOption(request.Path, playItem, parsed.PlayListMark.Marks, index)).ToList();
            return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], []);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidMpls", exception.Message));
        }
    }

    /// <summary>
    /// Executes the PtsToTime operation.
    /// </summary>
    /// <param name="pts">The Blu-ray presentation timestamp in 45 kHz PTS units.</param>
    /// <returns>The operation result.</returns>
    public static TimeSpan PtsToTime(uint pts)
    {
        var total = pts / 45000M;
        var seconds = Math.Floor(total);
        var milliseconds = Math.Round((total - seconds) * 1000M, MidpointRounding.AwayFromZero);
        return new TimeSpan(0, 0, 0, (int)seconds, (int)milliseconds);
    }

    /// <summary>
    /// Executes the ReadPlaylistInfo operation.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="title">The display title.</param>
    /// <param name="sourceName">The source display name.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>The operation result.</returns>
    public static ChapterSet ReadPlaylistInfo(
        string path,
        string title = "",
        string? sourceName = null,
        ChapterImportFormat sourceType = ChapterImportFormat.Mpls,
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

        return new ChapterSet(
            title,
            sourceName ?? string.Join("+", playItems.Select(static item => item.FullName)),
            sourceType,
            frameRateCode < FrameRates.Length ? FrameRates[frameRateCode] : 0,
            duration ?? PtsToTime(checked((uint)Math.Min(totalPts, uint.MaxValue))),
            chapters);
    }

    private static ChapterImportEntry ToOption(string path, MplsPlayItem playItem, IReadOnlyList<MplsMark> marks, int playItemIndex)
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
        var info = new ChapterSet(
            string.Empty,
            playItem.FullName,
            ChapterImportFormat.Mpls,
            frameRateCode < FrameRates.Length ? FrameRates[frameRateCode] : 0,
            PtsToTime(playItem.OUTTime - playItem.INTime),
            chapters);
        var refs = playItem.FullName
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(clip => new ReferencedMediaFile($"{clip}.m2ts", Path.Combine("..", "STREAM", $"{clip}.m2ts")))
            .ToList();
        return new ChapterImportEntry($"clip-{playItemIndex}", $"{playItem.FullName}__{chapters.Count}", info, CanCombine: true, ReferencedMediaFiles: refs);
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
