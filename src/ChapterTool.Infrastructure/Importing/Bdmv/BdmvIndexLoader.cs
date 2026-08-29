using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc.Index;

namespace ChapterTool.Infrastructure.Importing.Bdmv;

internal static class BdmvIndexLoader
{
    public static IndexFile? TryRead(BdmvSourceLayout layout, List<ChapterDiagnostic> diagnostics)
    {
        var index = IndexFile.TryRead(layout.PrimaryIndexPath, out var primaryError);
        if (index != null)
        {
            diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
                $"Loaded index.bdmv v{index.VersionNumber}: titles={index.Indexes.Titles.Count}.",
                layout.PrimaryIndexPath,
                Arguments: IndexStructure(index)));
            return index;
        }

        var backup = IndexFile.TryRead(layout.BackupIndexPath, out var backupError);
        if (backup != null)
        {
            diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
                $"Loaded backup index.bdmv v{backup.VersionNumber}; primary was unavailable: {primaryError}.", layout.BackupIndexPath));
            return backup;
        }

        var message = File.Exists(layout.PrimaryIndexPath)
            ? $"Failed to parse index.bdmv: {primaryError}. Falling back to playlist scan. Backup: {backupError}."
            : $"index.bdmv not found; falling back to playlist scan. Backup: {backupError}.";
        diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource, message));
        return null;
    }

    private static IReadOnlyDictionary<string, object?> IndexStructure(IndexFile index) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["header"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["typeIndicator"] = index.TypeIndicator,
                ["version"] = index.VersionNumber
            },
            ["indexes"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["length"] = index.Indexes.Length,
                ["titleCount"] = index.Indexes.Titles.Count,
                ["titles"] = index.Indexes.Titles.Select(static title => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["objectType"] = title.ObjectType,
                    ["playbackType"] = title.PlaybackType,
                    ["objectData"] = title.ObjectData
                }).ToList()
            }
        };
}
