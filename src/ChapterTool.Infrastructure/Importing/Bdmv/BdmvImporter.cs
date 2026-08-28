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
public sealed class BdmvImporter : IChapterImporter
{
    public string Id => "bdmv";

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
        var discTitle = BdmvMetadataReader.ReadDiscTitle(layout.MetadataDirectory);
        var scanCandidates = BdmvPlaylistScanner.Scan(layout, diagnostics).ToDictionary(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase);
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

            // Keep the first navigation title first. Sort the remaining candidates by complete
            // duration and use the descending playlist name as a stable tie-breaker.
            .ThenBy(candidate => FirstEvidenceOrder(candidate.Name, evidenceOrder))
            .ThenByDescending(static candidate => candidate.Projection.ChapterSet.Duration)
            .ThenByDescending(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var clpiByClip = BdmvPathHelper.DiscoverClpiFiles(
            layout.DiscRoot,
            candidates.SelectMany(static candidate => candidate.Projection.Playlist.PlayList.PlayItems)
                .SelectMany(static item => item.FullName.Split('&', StringSplitOptions.RemoveEmptyEntries)),
            diagnostics);

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
                DisplayName(candidate),
                chapterSet,
                CanCombine: true,
                ReferencedMediaFiles: candidate.Projection.ReferencedMediaFiles,
                MediaTracks: MplsMediaTrackProjection.ForPlayItems(candidate.Projection.Playlist.PlayList.PlayItems, clpiByClip)));
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
        var numberedTitles = index.Indexes.Titles.Select(static (title, index) => (Title: title, Number: (uint)index + 1)).ToList();
        var titleObjects = numberedTitles
            .Where(static item => !item.Title.IsAccessProhibited && item.Title.ObjectReference is IndexHdmvObjectReference)
            .ToDictionary(
                static item => item.Number,
                static item => ((IndexHdmvObjectReference)item.Title.ObjectReference).ObjectId);
        var movieObject = MovieObjectFile.TryReadPrimaryOrBackup(
            layout.PrimaryMovieObjectPath,
            layout.BackupMovieObjectPath,
            out var movieObjectPath,
            out var movieObjectError);
        var navigationDetails = new List<object?>();
        var unavailableLogged = false;

        foreach (var item in numberedTitles.Where(static item => item.Title.IsMoviePlayback && (item.Title.IsMovieObject || item.Title.IsBDJObject)))
        {
            var title = item.Title;
            if (title.IsAccessProhibited)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.NavigationSource,
                    $"Skipped prohibited INDEX title {item.Number}{(title.IsHidden ? " (hidden)" : string.Empty)}."));
                continue;
            }

            if (title.IsHidden)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.NavigationSource,
                    $"INDEX title {item.Number} is hidden."));
            }

            switch (title.ObjectReference)
            {
                case IndexHdmvObjectReference when movieObject == null:
                {
                    if (!unavailableLogged)
                    {
                        diagnostics.Add(new ChapterDiagnostic(
                            DiagnosticSeverity.Info,
                            ChapterDiagnosticCode.MovieObjectParseFailed,
                            $"MovieObject navigation was unavailable for {titleObjects.Count} HDMV title objects: {movieObjectError}."));
                        unavailableLogged = true;
                    }

                    continue;
                }
                case IndexHdmvObjectReference hdmv:
                {
                    var titleNumber = checked((int)item.Number);
                    var result = new HdmvNavigationResolver().ResolveProfileVariants(movieObject, hdmv.ObjectId, titleObjects, titleNumber);
                    diagnostics.AddRange(result.Diagnostics);
                    var playlists = new List<object?>();
                    foreach (var playback in result.Events)
                    {
                        AddEvidence(evidence, evidenceOrder, $"{playback.PlaylistId:D5}.mpls", $"HDMV:{hdmv.ObjectId}:{playback.InstructionType}");
                        playlists.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["playlist"] = $"{playback.PlaylistId:D5}.mpls",
                            ["instruction"] = playback.InstructionType
                        });
                    }

                    navigationDetails.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["title"] = titleNumber,
                        ["objectId"] = hdmv.ObjectId,
                        ["playlists"] = playlists
                    });
                    break;
                }
                case IndexBdJObjectReference bdj:
                    ResolveBdjo(bdj, layout, evidence, evidenceOrder, diagnostics);
                    break;
            }
        }

        if (navigationDetails.Count > 0)
        {
            var playlistCount = navigationDetails
                .Cast<IReadOnlyDictionary<string, object?>>()
                .Sum(static detail => ((IReadOnlyList<object?>)detail["playlists"]!).Count);
            var source = movieObjectPath == layout.BackupMovieObjectPath ? "backup" : "primary";
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.NavigationSource,
                $"Resolved {navigationDetails.Count} HDMV title objects and {playlistCount} playlist references from the {source} MovieObject.",
                movieObjectPath,
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = source,
                    ["objectCount"] = navigationDetails.Count,
                    ["playlistCount"] = playlistCount,
                    ["objects"] = navigationDetails
                }));
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
        if (!TryReadBdjo(layout, name, out var bdjo, out var source, out var selectedPath, diagnostics))
        {
            return;
        }

        diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.NavigationSource,
            $"Loaded {source} BDJO {name}.", selectedPath));
        ReportAccessiblePlaylists(bdjo!, name, evidence, evidenceOrder, selectedPath, diagnostics);
    }

    private static bool TryReadBdjo(
        BdmvSourceLayout layout,
        string name,
        out BdjoFile? bdjo,
        out string source,
        out string selectedPath,
        List<ChapterDiagnostic> diagnostics)
    {
        var primaryPath = Path.Combine(layout.PrimaryBdjoDirectory, $"{name}.bdjo");
        var backupPath = Path.Combine(layout.BackupBdjoDirectory, $"{name}.bdjo");

        bdjo = BdjoFile.TryRead(primaryPath, out var primaryError);
        selectedPath = primaryPath;
        source = "primary";
        if (bdjo is not null)
        {
            return true;
        }

        bdjo = BdjoFile.TryRead(backupPath, out var backupError);
        selectedPath = backupPath;
        source = "backup";
        if (bdjo is not null)
        {
            return true;
        }

        diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation,
            $"BD-J object {name} could not be parsed. Primary: {primaryError}; Backup: {backupError}."));
        return false;
    }

    private static void ReportAccessiblePlaylists(
        BdjoFile bdjo,
        string name,
        Dictionary<string, List<string>> evidence,
        List<string> evidenceOrder,
        string selectedPath,
        List<ChapterDiagnostic> diagnostics)
    {
        var playlists = bdjo.AccessiblePlaylists;
        ReportPlaylistEvidence(playlists, name, evidence, evidenceOrder);
        ReportDynamicSelectionWarning(playlists, name, selectedPath, diagnostics);
    }

    private static void ReportPlaylistEvidence(
        BdjoAccessiblePlaylists playlists,
        string name,
        Dictionary<string, List<string>> evidence,
        List<string> evidenceOrder)
    {
        foreach (var playlist in playlists.Names)
        {
            AddEvidence(evidence, evidenceOrder, $"{playlist}.mpls", PlaylistEvidenceSource(playlists, name, playlist));
        }
    }

    private static string PlaylistEvidenceSource(BdjoAccessiblePlaylists playlists, string name, string playlist) =>
        playlists.AutostartFirstPlaylist && playlist == playlists.Names[0]
            ? $"BDJO-autostart:{name}"
            : $"BDJO-accessible:{name}";

    private static void ReportDynamicSelectionWarning(
        BdjoAccessiblePlaylists playlists,
        string name,
        string selectedPath,
        List<ChapterDiagnostic> diagnostics)
    {
        if (playlists.AccessToAll || playlists.Names.Count == 0)
        {
            diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation,
                $"BD-J object {name} may select playlists dynamically. JAR and Xlet execution is not supported; bounded playlist scan is used as fallback.", selectedPath));
        }
    }

    /// <summary>
    /// Builds the BDMV display name: playlist name, complete duration, and the m2ts
    /// combination. Angle clips and multiple PlayItems merge into one bracket group,
    /// for example <c>00041.mpls (2:00:22) [00112+00127+00115].m2ts</c>.
    /// </summary>
    private static string DisplayName(BdmvPlaylistCandidate candidate)
        => MplsPlaylistProjection.PlaylistDisplayName(
            candidate.Name,
            candidate.Projection.ChapterSet.Duration,
            candidate.Projection.Playlist.PlayList.PlayItems.SelectMany(MplsPlaylistProjection.ClipNames));

    /// <summary>
    /// Renders a clip list in the BDMV display convention: a single clip keeps its plain name,
    /// while multiple clips (angles or PlayItems) merge into one <c>[a+b+c].m2ts</c> bracket group.
    /// </summary>
    internal static string ClipListDisplay(IEnumerable<string> clips)
        => MplsPlaylistProjection.ClipListDisplay(clips);

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

    private static void Report(
        IChapterImportProgressReporter? progress,
        ChapterImportProgressPhase phase,
        double fraction,
        string? sourceName,
        int? current = null,
        int? total = null) =>
        progress?.Report(new ChapterImportProgress(phase, fraction, sourceName, current, total));
}
