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
        var projection = MplsPlaylistProjection.Read(path, discRoot);
        var playlist = projection.Playlist;
        var playlistName = Path.GetFileName(path);
        var info = projection.ToChapterSet(title ?? string.Empty, playlistName);
        return new MplsAggregateProjection(playlistName, playlist, info, projection.ReferencedMediaFiles, projection.HasChapterMarks);
    }
}
