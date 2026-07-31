using System.Xml.Linq;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Disc.Bdjo;
using ChapterTool.Core.Importing.Disc.Index;
using ChapterTool.Core.Importing.Disc.MovieObject;
using ChapterTool.Core.Models;

#pragma warning disable SA1503

namespace ChapterTool.Infrastructure.Importing.Bdmv;

/// <summary>Imports complete chapter-bearing Blu-ray playlists with managed parsers.</summary>
public sealed class NativeBdmvImporter : IChapterImporter
{
    private readonly BdmvPlaylistScanner scanner = new();

    public string Id => "bdmv-native";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BDMV"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        var layout = BdmvSourceLayout.TryResolve(request.Path, out var layoutError);
        if (layout == null)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(
                DiagnosticSeverity.Error,
                ChapterDiagnosticCode.BdmvInputRejected,
                layoutError ?? "Invalid BDMV input."));
        }

        diagnostics.Add(new ChapterDiagnostic(
            DiagnosticSeverity.Info,
            ChapterDiagnosticCode.BdmvInputLayout,
            $"Normalized BDMV input to disc root '{layout.DiscRoot}'.",
            layout.OriginalInputPath));

        Report(request.ProgressReporter, ChapterImportProgressPhase.DiscoveringTitles, 0.05, layout.OriginalInputPath);
        var discTitle = ReadDiscTitle(layout.MetadataDirectory);
        var scanCandidates = scanner.Scan(layout, diagnostics).ToDictionary(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase);
        var evidence = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var evidenceOrder = new List<string>();
        var index = TryReadIndex(layout, diagnostics);
        if (index != null)
        {
            ResolveNavigation(index, layout, evidence, evidenceOrder, diagnostics);
        }

        var candidates = scanCandidates.Values
            .Select(candidate => candidate with { Evidence = evidence.TryGetValue(candidate.Name, out var values) ? values : candidate.Evidence })
            .OrderBy(static candidate => EvidencePriority(candidate.Evidence))

            // eac3to keeps the first discovered navigation title first. It then sorts the remaining
            // candidates by complete duration and uses the descending playlist name as a stable tie-breaker.
            .ThenBy(candidate => FirstEvidenceOrder(candidate.Name, evidenceOrder))
            .ThenByDescending(static candidate => candidate.Projection.ChapterSet.Duration)
            .ThenByDescending(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in candidates)
        {
            _ = BdmvPathHelper.DiscoverClpiFiles(
                layout.DiscRoot,
                candidate.Projection.Playlist.PlayList.PlayItems.SelectMany(static item => item.FullName.Split('&', StringSplitOptions.RemoveEmptyEntries)),
                diagnostics);
            if (candidate.Projection.HasChapterMarks) continue;
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.BdmvScanCandidate,
                $"Retained no-chapter playlist candidate {candidate.Name} for parity diagnostics.",
                candidate.Path));
        }

        var entries = new List<ChapterImportEntry>();
        for (var indexValue = 0; indexValue < candidates.Count; indexValue++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[indexValue];
            Report(
                request.ProgressReporter,
                ChapterImportProgressPhase.ParsingChapters,
                0.10 + (indexValue + 1) * 0.85 / Math.Max(candidates.Count, 1),
                candidate.Name,
                indexValue + 1,
                candidates.Count);
            if (!candidate.Projection.HasChapterMarks) continue;

            var chapterSet = candidate.Projection.ChapterSet with
            {
                Title = string.IsNullOrWhiteSpace(discTitle) ? candidate.Projection.ChapterSet.Title : discTitle
            };
            entries.Add(new ChapterImportEntry(
                candidate.Name,
                candidate.Name,
                chapterSet,
                CanCombine: true,
                ReferencedMediaFiles: candidate.Projection.ReferencedMediaFiles));
        }

        if (entries.Count == 0)
        {
            var errors = diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length == 0)
            {
                diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.NoChaptersFound, "No BDMV playlists with chapters were found."));
            }

            return new ChapterImportResult(false, [], diagnostics);
        }

        return new ChapterImportResult(true, [new ChapterImportSource(layout.OriginalInputPath, entries)], diagnostics);
    }

    private static IndexFile? TryReadIndex(BdmvSourceLayout layout, List<ChapterDiagnostic> diagnostics)
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

    private static void ResolveNavigation(
        IndexFile index,
        BdmvSourceLayout layout,
        Dictionary<string, List<string>> evidence,
        List<string> evidenceOrder,
        List<ChapterDiagnostic> diagnostics)
    {
        var titleObjects = index.Indexes.MovieAndBdJTitles
            .Select(static title => title.ObjectReference)
            .OfType<IndexHdmvObjectReference>()
            .Select(static reference => reference.ObjectId)
            .ToList();
        var movieObject = MovieObjectFile.TryReadPrimaryOrBackup(
            layout.PrimaryMovieObjectPath,
            layout.BackupMovieObjectPath,
            out var movieObjectPath,
            out var movieObjectError);

        foreach (var title in index.Indexes.MovieAndBdJTitles)
        {
            if (title.ObjectReference is IndexHdmvObjectReference hdmv)
            {
                if (movieObject == null)
                {
                    diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.MovieObjectParseFailed,
                        $"MovieObject navigation was unavailable for object {hdmv.ObjectId}: {movieObjectError}."));
                    continue;
                }

                var titleNumber = index.Indexes.MovieAndBdJTitles.ToList().IndexOf(title) + 1;
                var result = new HdmvNavigationResolver().Resolve(movieObject, hdmv.ObjectId, titleObjects, titleNumber: titleNumber);
                diagnostics.AddRange(result.Diagnostics);
                diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
                    $"Resolved HDMV object {hdmv.ObjectId} from {(movieObjectPath == layout.BackupMovieObjectPath ? "backup" : "primary")} MovieObject."));
                foreach (var playback in result.Events)
                {
                    AddEvidence(evidence, evidenceOrder, $"{playback.PlaylistId:D5}.mpls", $"HDMV:{hdmv.ObjectId}:{playback.InstructionType}");
                    diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
                        $"Index title references playlist through HDMV navigation: {playback.PlaylistId:D5}.mpls."));
                }
            }
            else if (title.ObjectReference is IndexBdJObjectReference bdj)
            {
                ResolveBdjo(bdj, layout, evidence, evidenceOrder, diagnostics);
            }
        }
    }

    private static void ResolveBdjo(
        IndexBdJObjectReference reference,
        BdmvSourceLayout layout,
        Dictionary<string, List<string>> evidence,
        List<string> evidenceOrder,
        List<ChapterDiagnostic> diagnostics)
    {
        var name = reference.Name;
        var primaryPath = Path.Combine(layout.PrimaryBdjoDirectory, $"{name}.bdjo");
        var backupPath = Path.Combine(layout.BackupBdjoDirectory, $"{name}.bdjo");
        var bdjo = BdjoFile.TryRead(primaryPath, out var primaryError);
        var selectedPath = primaryPath;
        if (bdjo == null)
        {
            bdjo = BdjoFile.TryRead(backupPath, out var backupError);
            selectedPath = backupPath;
            if (bdjo == null)
            {
                diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation,
                    $"BD-J object {name} could not be parsed. Primary: {primaryError}; Backup: {backupError}."));
                return;
            }
        }

        diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
            $"Loaded {(selectedPath == backupPath ? "backup" : "primary")} BDJO {name}.", selectedPath));
        foreach (var playlist in bdjo.AccessiblePlaylists.Names)
        {
            AddEvidence(evidence, evidenceOrder, $"{playlist}.mpls", bdjo.AccessiblePlaylists.AutostartFirstPlaylist && playlist == bdjo.AccessiblePlaylists.Names[0]
                ? $"BDJO-autostart:{name}"
                : $"BDJO-accessible:{name}");
        }

        if (bdjo.AccessiblePlaylists.AccessToAll || bdjo.AccessiblePlaylists.Names.Count == 0)
        {
            diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation,
                $"BD-J object {name} may select playlists dynamically. JAR and Xlet execution is not supported; bounded playlist scan is used as fallback.", selectedPath));
        }
    }

    private static void AddEvidence(Dictionary<string, List<string>> evidence, List<string> evidenceOrder, string name, string source)
    {
        if (!evidence.TryGetValue(name, out var values))
        {
            evidence[name] = values = [];
            evidenceOrder.Add(name);
        }

        if (!values.Contains(source, StringComparer.Ordinal)) values.Add(source);
    }

    private static int FirstEvidenceOrder(string name, IReadOnlyList<string> evidenceOrder) =>
        evidenceOrder.Count == 0 ? 1 : evidenceOrder[0].Equals(name, StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static int EvidencePriority(IReadOnlyList<string> evidence) =>
        evidence.Any(static item => item.StartsWith("HDMV:", StringComparison.Ordinal)) ? 0 :
        evidence.Any(static item => item.StartsWith("BDJO-", StringComparison.Ordinal)) ? 1 : 2;

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

    private static string ReadDiscTitle(string metadataDirectory)
    {
        try
        {
            var file = Directory.Exists(metadataDirectory)
                ? Directory.EnumerateFiles(metadataDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                : null;
            if (file == null) return string.Empty;
            var document = XDocument.Load(file, LoadOptions.None);
            return document.Descendants().FirstOrDefault(static element => element.Name.LocalName == "name")?.Value.Trim() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static void Report(
        IChapterImportProgressReporter? progress,
        ChapterImportProgressPhase phase,
        double fraction,
        string? sourceName,
        int? current = null,
        int? total = null) =>
        progress?.Report(new ChapterImportProgress(phase, fraction, sourceName, current, total));
}
