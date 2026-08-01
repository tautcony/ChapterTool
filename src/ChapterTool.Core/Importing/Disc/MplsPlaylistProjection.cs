using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

#pragma warning disable SA1503

/// <summary>
/// Provides one semantic projection of an MPLS playlist for all import hosts.
/// </summary>
internal sealed record MplsPlaylistProjection(
    MplsPlaylistFile Playlist,
    IReadOnlyList<MplsMark> ChapterMarks,
    IReadOnlyList<ulong> PlayItemStartPts,
    IReadOnlyList<Chapter> Chapters,
    TimeSpan Duration,
    double FramesPerSecond,
    IReadOnlyList<ReferencedMediaFile> ReferencedMediaFiles,
    string? DiscRoot)
{
    internal bool HasChapterMarks => ChapterMarks.Count > 0;

    internal static MplsPlaylistProjection Read(string path, string? discRoot = null)
    {
        using var stream = File.OpenRead(path);
        return Create(MplsPlaylistFile.Read(stream), discRoot);
    }

    internal static MplsPlaylistProjection Create(MplsPlaylistFile playlist, string? discRoot = null)
    {
        var playItems = playlist.PlayList.PlayItems;
        var marks = playlist.PlayListMark.Marks
            .Where(static mark => mark.MarkType == 0x01)
            .Where(mark => mark.RefToPlayItemID < playItems.Count)
            .ToList();
        var starts = new ulong[playItems.Count];
        var cursor = 0UL;
        for (var index = 0; index < playItems.Count; index++)
        {
            starts[index] = cursor;
            cursor += playItems[index].OUTTime - playItems[index].INTime;
        }

        var chapters = BuildPlaylistChapters(playItems, marks, starts);
        var frameRateCode = playItems
            .SelectMany(static item => item.STNTable.PrimaryVideoStreamEntries)
            .Select(static entry => entry.StreamAttributes.FrameRate)
            .FirstOrDefault();
        var references = BuildReferences(playItems, discRoot);
        return new MplsPlaylistProjection(
            playlist,
            marks,
            starts,
            chapters,
            MplsChapterImporter.PtsToTime(checked((uint)Math.Min(cursor, uint.MaxValue))),
            MplsFrameRateCatalog.FromCode(frameRateCode),
            references,
            discRoot);
    }

    internal ChapterSet ToChapterSet(
        string title = "",
        string? sourceName = null,
        ChapterImportFormat sourceType = ChapterImportFormat.Mpls,
        TimeSpan? duration = null) =>
        new(
            title,
            sourceName ?? string.Join("+", Playlist.PlayList.PlayItems.Select(static item => item.FullName)),
            sourceType,
            FramesPerSecond,
            duration ?? Duration,
            Chapters);

    internal IReadOnlyList<Chapter> ChaptersForPlayItem(int playItemIndex)
    {
        if (playItemIndex < 0 || playItemIndex >= Playlist.PlayList.PlayItems.Count)
        {
            return [];
        }

        var playItem = Playlist.PlayList.PlayItems[playItemIndex];
        var marks = ChapterMarks
            .Where(mark => mark.RefToPlayItemID == playItemIndex)
            .ToList();
        if (marks.Count == 0)
        {
            return [new Chapter(1, TimeSpan.Zero, "Chapter 01")];
        }

        var offset = Math.Min(playItem.INTime, marks[0].MarkTimeStamp);
        return
        [
            .. marks
                .Select((mark, index) => new Chapter(
                    index + 1,
                    MplsChapterImporter.PtsToTime(mark.MarkTimeStamp - offset),
                    $"Chapter {index + 1:D2}"))
        ];
    }

    internal IReadOnlyList<string> ClipNamesForPlayItem(int playItemIndex) =>
        playItemIndex < 0 || playItemIndex >= Playlist.PlayList.PlayItems.Count
            ? []
            : ClipNames(Playlist.PlayList.PlayItems[playItemIndex]);

    internal IReadOnlyList<ReferencedMediaFile> ReferencesForPlayItem(int playItemIndex, string? discRoot = null)
    {
        if (playItemIndex < 0 || playItemIndex >= Playlist.PlayList.PlayItems.Count)
        {
            return [];
        }

        return BuildReferences([Playlist.PlayList.PlayItems[playItemIndex]], discRoot ?? DiscRoot);
    }

    internal string ClipDisplayNameForPlayItem(int playItemIndex) =>
        ClipListDisplay(ClipNamesForPlayItem(playItemIndex));

    internal static IReadOnlyList<string> ClipNames(MplsPlayItem playItem) =>
        playItem.FullName.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static string ClipListDisplay(IEnumerable<string> clips)
    {
        var names = clips.ToList();
        return names.Count switch
        {
            0 => string.Empty,
            1 => $"{names[0]}.m2ts",
            _ => $"[{string.Join("+", names)}].m2ts"
        };
    }

    internal static string PlaylistDisplayName(string playlistName, TimeSpan duration, IEnumerable<string> clips)
    {
        var clipText = ClipListDisplay(clips);
        return clipText.Length == 0
            ? $@"{playlistName} ({duration:h\:mm\:ss})"
            : $@"{playlistName} ({duration:h\:mm\:ss}) {clipText}";
    }

    private static List<Chapter> BuildPlaylistChapters(
        IReadOnlyList<MplsPlayItem> playItems,
        IReadOnlyList<MplsMark> marks,
        IReadOnlyList<ulong> starts)
    {
        var chapters = marks
            .Select(mark => starts[mark.RefToPlayItemID] + (mark.MarkTimeStamp > playItems[mark.RefToPlayItemID].INTime
                ? mark.MarkTimeStamp - playItems[mark.RefToPlayItemID].INTime
                : 0))
            .Distinct()
            .Order()
            .Select((pts, index) => new Chapter(
                index + 1,
                MplsChapterImporter.PtsToTime(checked((uint)Math.Min(pts, uint.MaxValue))),
                $"Chapter {index + 1:D2}"))
            .ToList();

        return chapters.Count == 0
            ? [new Chapter(1, TimeSpan.Zero, "Chapter 01")]
            : chapters;
    }

    private static IReadOnlyList<ReferencedMediaFile> BuildReferences(
        IEnumerable<MplsPlayItem> playItems,
        string? discRoot)
    {
        var references = new List<ReferencedMediaFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in playItems.SelectMany(ClipNames))
        {
            if (!seen.Add(clip))
            {
                continue;
            }

            var relative = Path.Combine("..", "STREAM", $"{clip}.m2ts");
            var absolute = discRoot is null
                ? null
                : Path.Combine(discRoot, "BDMV", "STREAM", $"{clip}.m2ts");
            references.Add(new ReferencedMediaFile($"{clip}.m2ts", relative, absolute));
        }

        return references;
    }
}

internal static class MplsFrameRateCatalog
{
    private static readonly double[] Values =
    [
        0,
        24000d / 1001d,
        24,
        25,
        30000d / 1001d,
        30,
        50,
        60000d / 1001d,
        60
    ];

    internal static double FromCode(byte? code) =>
        code is { } value && value < Values.Length ? Values[value] : 0;
}
