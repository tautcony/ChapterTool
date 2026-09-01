using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc;

#pragma warning disable SA1503

namespace ChapterTool.Infrastructure.Importing.Bdmv;

internal sealed record BdmvPlaylistCandidate(
    string Name,
    string Path,
    MplsAggregateProjection Projection,
    IReadOnlyList<string> Evidence);

internal static class BdmvPlaylistScanner
{
    internal const int MaximumPlaylists = 4096;
    internal const int MaximumRepeatedSegments = 2;

    internal static IReadOnlyList<BdmvPlaylistCandidate> Scan(
        BdmvSourceLayout layout,
        List<ChapterDiagnostic> diagnostics)
    {
        var directory = Directory.Exists(layout.PrimaryPlaylistDirectory)
            ? layout.PrimaryPlaylistDirectory
            : layout.BackupPlaylistDirectory;
        if (!Directory.Exists(directory)) return [];

        var candidates = new List<BdmvPlaylistCandidate>();
        var skipped = new List<object?>();
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<string> paths;
        try
        {
            paths =
            [
                .. Directory.EnumerateFiles(directory, "*.mpls", SearchOption.TopDirectoryOnly)
                    .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumPlaylists)
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.BdmvScanRejected, $"Unable to enumerate BDMV playlists: {exception.Message}");
            return [];
        }

        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            try
            {
                var projection = MplsAggregateProjection.Read(path, discRoot: layout.DiscRoot);
                var repeated = projection.Playlist.PlayList.PlayItems
                    .GroupBy(static item => $"{item.FullName}:{item.INTime}:{item.OUTTime}", StringComparer.Ordinal)
                    .Any(group => group.Count() > MaximumRepeatedSegments);
                if (repeated)
                {
                    skipped.Add(SkippedPlaylist(name, path, "repeated-segments"));
                    continue;
                }

                var signature = StructuralSignature(projection);
                if (!signatures.Add(signature))
                {
                    skipped.Add(SkippedPlaylist(name, path, "structural-duplicate"));
                    continue;
                }

                candidates.Add(new BdmvPlaylistCandidate(name, path, projection, ["playlist-scan"]));
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.BdmvScanRejected, $"Rejected BDMV playlist {name}: {exception.Message}", path);
            }
        }

        if (paths.Count > 0)
        {
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.BdmvScanCandidate,
                $"Playlist scan retained {candidates.Count} of {paths.Count} discovered files and skipped {skipped.Count} duplicates or repeated-segment playlists.",
                directory,
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["discoveredCount"] = paths.Count,
                    ["retainedCount"] = candidates.Count,
                    ["skippedCount"] = skipped.Count,
                    ["candidates"] = candidates.Select(static candidate => new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["name"] = candidate.Name,
                        ["path"] = candidate.Path,
                        ["hasChapterMarks"] = candidate.Projection.HasChapterMarks,
                        ["chapters"] = candidate.Projection.ChapterSet.Chapters.Count,
                        ["duration"] = candidate.Projection.ChapterSet.Duration,
                        ["playItems"] = candidate.Projection.Playlist.PlayList.PlayItems.Count
                    }).Cast<object?>().ToList(),
                    ["skipped"] = skipped
                }));
        }

        return candidates;
    }

    private static Dictionary<string, object?> SkippedPlaylist(string name, string path, string reason) =>
        new(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["path"] = path,
            ["reason"] = reason
        };

    private static string StructuralSignature(MplsAggregateProjection projection) =>
        string.Join("|", projection.Playlist.PlayList.PlayItems.Select(static item =>
            $"{item.FullName}:{item.INTime}:{item.OUTTime}:{item.RefToSTCID}:{item.STNTable.PrimaryVideoStreamEntries.Count}")) +
        ";" + string.Join("|", projection.Playlist.PlayListMark.Marks.Select(static mark =>
            $"{mark.MarkType}:{mark.RefToPlayItemID}:{mark.MarkTimeStamp}"));
}

internal static class BdmvDiagnosticExtensions
{
    internal static void Add(this List<ChapterDiagnostic> diagnostics, DiagnosticSeverity severity, ChapterDiagnosticCode code, string message, string? location = null) =>
        diagnostics.Add(new ChapterDiagnostic(severity, code, message, location));
}
