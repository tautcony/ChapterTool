using ChapterTool.Core.Models;

 #pragma warning disable SA1503

namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsAggregateProjection(
    string PlaylistName,
    MplsPlaylistFile Playlist,
    ChapterSet ChapterSet,
    IReadOnlyList<ReferencedMediaFile> ReferencedMediaFiles,
    bool HasChapterMarks)
{
    internal static MplsAggregateProjection Read(string path, string? title = null, string? discRoot = null)
    {
        using var stream = File.OpenRead(path);
        var playlist = MplsPlaylistFile.Read(stream);
        var playItems = playlist.PlayList.PlayItems;
        var marks = playlist.PlayListMark.Marks
            .Where(static mark => mark.MarkType == 0x01 && mark.RefToPlayItemID < 4096)
            .Where(mark => mark.RefToPlayItemID < playItems.Count)
            .ToList();
        var starts = new ulong[playItems.Count];
        var cursor = 0UL;
        for (var i = 0; i < playItems.Count; i++)
        {
            starts[i] = cursor;
            cursor += playItems[i].OUTTime - playItems[i].INTime;
        }

        var chapters = marks
            .Select(mark => starts[mark.RefToPlayItemID] + (mark.MarkTimeStamp > playItems[mark.RefToPlayItemID].INTime
                ? mark.MarkTimeStamp - playItems[mark.RefToPlayItemID].INTime
                : 0))
            .Distinct()
            .Order()
            .Select((pts, index) => new Chapter(index + 1, MplsChapterImporter.PtsToTime(checked((uint)Math.Min(pts, uint.MaxValue))), $"Chapter {index + 1:D2}"))
            .ToList();
        var frameRateCode = playItems
            .SelectMany(static item => item.STNTable.PrimaryVideoStreamEntries)
            .Select(static entry => entry.StreamAttributes.FrameRate ?? 0)
            .FirstOrDefault();
        var fps = frameRateCode switch
        {
            1 => 23.976,
            2 => 24,
            3 => 25,
            4 => 29.97,
            5 => 30,
            6 => 50,
            7 => 59.94,
            8 => 60,
            _ => 0
        };
        var playlistName = Path.GetFileName(path);
        var references = new List<ReferencedMediaFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in playItems.SelectMany(static item => item.FullName.Split('&', StringSplitOptions.RemoveEmptyEntries)))
        {
            if (!seen.Add(clip)) continue;
            var relative = Path.Combine("..", "STREAM", $"{clip}.m2ts");
            var absolute = discRoot == null ? null : Path.Combine(discRoot, "BDMV", "STREAM", $"{clip}.m2ts");
            references.Add(new ReferencedMediaFile($"{clip}.m2ts", relative, absolute));
        }

        var duration = MplsChapterImporter.PtsToTime(checked((uint)Math.Min(cursor, uint.MaxValue)));
        var info = new ChapterSet(
            title ?? string.Empty,
            playlistName,
            ChapterImportFormat.Mpls,
            fps,
            duration,
            chapters);
        return new MplsAggregateProjection(playlistName, playlist, info, references, marks.Count > 0);
    }
}
