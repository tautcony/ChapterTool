using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc;

#pragma warning disable SA1503

namespace ChapterTool.Infrastructure.Importing.Bdmv;

internal sealed record BdmvPlaylistCandidate(
    string Name,
    string Path,
    MplsAggregateProjection Projection,
    IReadOnlyList<string> Evidence);

internal sealed class BdmvPlaylistScanner
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
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(directory, "*.mpls", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Take(MaximumPlaylists);
        }
        catch (IOException exception)
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
                    diagnostics.Add(DiagnosticSeverity.Info, ChapterDiagnosticCode.BdmvScanRejected, $"Skipped repeated-segment BDMV playlist {name}.", path);
                    continue;
                }

                var signature = StructuralSignature(projection);
                if (!signatures.Add(signature))
                {
                    diagnostics.Add(DiagnosticSeverity.Info, ChapterDiagnosticCode.BdmvScanRejected, $"Skipped structural duplicate BDMV playlist {name}.", path);
                    continue;
                }

                candidates.Add(new BdmvPlaylistCandidate(name, path, projection, ["playlist-scan"]));
                diagnostics.Add(DiagnosticSeverity.Info, ChapterDiagnosticCode.BdmvScanCandidate, $"Playlist scan found {name}.", path);
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
            {
                diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.BdmvScanRejected, $"Rejected BDMV playlist {name}: {exception.Message}", path);
            }
        }

        return candidates;
    }

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
