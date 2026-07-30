using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Disc.Index;
using ChapterTool.Core.Models;

namespace ChapterTool.Infrastructure.Importing.Bdmv;

/// <summary>
/// Native C# importer for Blu-ray BDMV directories that discovers
/// index.bdmv, playlists, and CLPI files without external tools.
/// </summary>
public sealed partial class NativeBdmvImporter : IChapterImporter
{
    private readonly MplsChapterImporter mplsImporter = new();

    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "bdmv-native";

    /// <summary>
    /// Gets the supported directory extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BDMV"
    };

    /// <summary>
    /// Imports chapters from a BDMV directory path.
    /// </summary>
    /// <param name="request">The import request with a BDMV root directory path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var playlistDirectory = Path.Combine(request.Path, "BDMV", "PLAYLIST");
        if (!Directory.Exists(playlistDirectory))
        {
            return ChapterImportResult.Failed(
                Error(ChapterDiagnosticCode.InvalidStructure, "Blu-ray BDMV/PLAYLIST directory was not found."));
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var discTitle = ReadDiscTitle(request.Path);

        var playlistCandidates = DiscoverPlaylistCandidates(request.Path, diagnostics);

        var entries = new List<ChapterImportEntry>();
        for (var candidateIndex = 0; candidateIndex < playlistCandidates.Count; candidateIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = playlistCandidates[candidateIndex];

            Report(
                request.ProgressReporter,
                ChapterImportProgressPhase.DiscoveringTitles,
                0.20 + candidateIndex * 0.75 / Math.Max(playlistCandidates.Count, 1),
                candidate,
                candidateIndex + 1,
                playlistCandidates.Count);

            try
            {
                var mplsPath = Path.Combine(playlistDirectory, candidate);
                if (!File.Exists(mplsPath))
                {
                    diagnostics.Add(Info(ChapterDiagnosticCode.InvalidMpls, $"Playlist file not found: {candidate}"));
                    continue;
                }

                var mplsResult = await mplsImporter.ImportAsync(
                    new ChapterImportRequest(mplsPath, ProgressReporter: request.ProgressReporter),
                    cancellationToken);

                diagnostics.AddRange(mplsResult.Diagnostics);
                if (!mplsResult.Success)
                {
                    continue;
                }

                foreach (var group in mplsResult.Groups)
                {
                    foreach (var entry in group.Entries)
                    {
                        var info = entry.ChapterSet with
                        {
                            Title = discTitle.Length > 0 ? discTitle : entry.ChapterSet.Title
                        };
                        entries.Add(new ChapterImportEntry(
                            entry.Id,
                            entry.DisplayName,
                            info,
                            CanCombine: true,
                            ReferencedMediaFiles: entry.ReferencedMediaFiles));
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
            {
                diagnostics.Add(Error(ChapterDiagnosticCode.InvalidMpls, $"Failed to parse {candidate}: {exception.Message}"));
            }
        }

        if (entries.Count == 0)
        {
            var errors = diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            return ChapterImportResult.Failed(
                errors.Length == 0
                    ? [Error(ChapterDiagnosticCode.NoChaptersFound, "No BDMV playlists with chapters were found.")]
                    : errors);
        }

        return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], diagnostics);
    }

    private static List<string> DiscoverPlaylistCandidates(string bdmvRoot, List<ChapterDiagnostic> diagnostics)
    {
        var indexPath = BdmvPathHelper.GetIndexPath(bdmvRoot);
        if (indexPath != null && File.Exists(indexPath))
        {
            var index = IndexFile.TryRead(indexPath, out var indexError);
            if (index != null)
            {
                diagnostics.Add(Info(
                    ChapterDiagnosticCode.ParseInfo,
                    $"Loaded index.bdmv v{index.VersionNumber}: " +
                    $"video_format={index.AppInfoBDMV.VideoFormat}, " +
                    $"frame_rate={index.AppInfoBDMV.FrameRate}, " +
                    $"initial_output={index.AppInfoBDMV.InitialOutputModePreference}, " +
                    $"titles={index.Indexes.Titles.Count}",
                    arguments: IndexStructure(index)));

                var movieTitles = index.Indexes.MovieTitles.ToList();
                if (movieTitles.Count > 0)
                {
                    diagnostics.Add(Info(
                        ChapterDiagnosticCode.ParseInfo,
                        $"Found {movieTitles.Count} movie title(s) in index.bdmv."));

                    var candidates = new List<string>();
                    foreach (var title in movieTitles)
                    {
                        var playlistName = ExtractPlaylistFromTitle(title);
                        if (playlistName != null)
                        {
                            diagnostics.Add(Info(
                                ChapterDiagnosticCode.ParseInfo,
                                $"Index title references playlist: {playlistName}"));
                            candidates.Add(playlistName);
                        }
                    }

                    if (candidates.Count > 0)
                    {
                        return candidates;
                    }

                    diagnostics.Add(Info(ChapterDiagnosticCode.ParseInfo,
                        "No playlists could be resolved from index.bdmv titles; falling back to playlist scan."));
                }
                else
                {
                    diagnostics.Add(Info(ChapterDiagnosticCode.ParseInfo,
                        "No movie titles found in index.bdmv; falling back to playlist scan."));
                }
            }
            else
            {
                diagnostics.Add(Info(
                    ChapterDiagnosticCode.ParseInfo,
                    $"Failed to parse index.bdmv: {indexError}. Falling back to playlist scan."));
            }
        }
        else
        {
            diagnostics.Add(Info(ChapterDiagnosticCode.ParseInfo,
                "index.bdmv not found; falling back to playlist scan."));
        }

        return ScanPlaylistFiles(bdmvRoot);
    }

    private static string? ExtractPlaylistFromTitle(IndexTitleEntry title)
    {
        try
        {
            if (title.IsMovieObject && !string.IsNullOrWhiteSpace(title.ObjectData))
            {
                var data = title.ObjectData.Trim();
                var match = MplsReferenceRegex().Match(data);
                if (match.Success)
                {
                    return $"{match.Groups["Mpls"].Value}.mpls";
                }
            }
        }
        catch (Exception)
        {
        }

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
            ["appInfo"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["length"] = index.AppInfoBDMV.Length,
                ["initialOutputModePreference"] = index.AppInfoBDMV.InitialOutputModePreference,
                ["ssContentExistFlag"] = index.AppInfoBDMV.SSContentExistFlag,
                ["initialDynamicRangeType"] = index.AppInfoBDMV.InitialDynamicRangeType,
                ["videoFormat"] = index.AppInfoBDMV.VideoFormat,
                ["frameRate"] = index.AppInfoBDMV.FrameRate,
                ["userData"] = index.AppInfoBDMV.UserData.TrimEnd('\0')
            },
            ["indexes"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["length"] = index.Indexes.Length,
                ["firstPlaybackTitle"] = TitleStructure(index.Indexes.FirstPlaybackTitle),
                ["topMenuTitle"] = TitleStructure(index.Indexes.TopMenuTitle),
                ["titleCount"] = index.Indexes.Titles.Count,
                ["titles"] = index.Indexes.Titles.Select(TitleStructure).ToList()
            }
        };

    private static IReadOnlyDictionary<string, object?> TitleStructure(IndexTitleEntry title) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["objectType"] = title.ObjectType,
            ["accessType"] = title.AccessType,
            ["playbackType"] = title.PlaybackType,
            ["objectData"] = title.ObjectData
        };

    private static List<string> ScanPlaylistFiles(string bdmvRoot)
    {
        var playlistDir = Path.Combine(bdmvRoot, "BDMV", "PLAYLIST");
        if (!Directory.Exists(playlistDir))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(playlistDir, "*.mpls")
                .Select(Path.GetFileName)
                .Where(static name => name != null)
                .OrderBy(static name => name)
                .ToList()!;
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string ReadDiscTitle(string bdmvRoot)
    {
        var metaPath = BdmvPathHelper.GetMetaXmlPath(bdmvRoot);
        if (metaPath == null)
        {
            return string.Empty;
        }

        try
        {
            var text = File.ReadAllText(metaPath);
            var match = DiscTitleRegex().Match(text);
            return match.Success ? match.Groups["Title"].Value.Trim() : string.Empty;
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
        string? sourceName = null,
        int? current = null,
        int? total = null) =>
        progress?.Report(new ChapterImportProgress(phase, fraction, sourceName, current, total));

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private static ChapterDiagnostic Info(
        ChapterDiagnosticCode code,
        string message,
        string? location = null,
        string? details = null,
        IReadOnlyDictionary<string, object?>? arguments = null) =>
        new(DiagnosticSeverity.Info, code, message, location, details, arguments);

    [GeneratedRegex(@"^(?<Mpls>\d{5})(?:\.mpls)?$", RegexOptions.IgnoreCase)]
    private static partial Regex MplsReferenceRegex();

    [GeneratedRegex(@"<di:name>\s*(?<Title>.*?)\s*</di:name>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DiscTitleRegex();
}
