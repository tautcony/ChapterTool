using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc.Clpi;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

/// <summary>
/// Imports Blu-ray chapter data from MPLS playlist files.
/// </summary>
public sealed class MplsChapterImporter : IChapterImporter
{
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
        var diagnostics = new List<ChapterDiagnostic>();
        await using var stream = request.Content ?? File.OpenRead(request.Path);
        try
        {
            var parsed = MplsPlaylistFile.Read(stream);
            _ = DiscoverClpiFromPath(request.Path, parsed, diagnostics);
            var projection = MplsPlaylistProjection.Create(parsed, BdmvPathHelper.FindBdmvRoot(request.Path));
            var entries = parsed.PlayList.PlayItems.Select((_, index) => ToOption(projection, index)).ToList();
            return new ChapterImportResult(true, [new ChapterImportSource(request.Path, entries)], diagnostics);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            return ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidMpls, exception.Message));
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
        return MplsPlaylistProjection.Read(path).ToChapterSet(title, sourceName, sourceType, duration);
    }

    private static IReadOnlyDictionary<string, ClpiFile>? DiscoverClpiFromPath(string path, MplsPlaylistFile parsed, List<ChapterDiagnostic>? diagnostics = null)
    {
        var bdmvRoot = BdmvPathHelper.FindBdmvRoot(path);
        if (bdmvRoot == null)
        {
            return null;
        }

        var clipNames = parsed.PlayList.PlayItems
            .SelectMany(static item => item.FullName.Split('&', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var clpiMap = BdmvPathHelper.DiscoverClpiFiles(bdmvRoot, clipNames, diagnostics);
        return clpiMap.Count > 0 ? clpiMap : null;
    }

    private static ChapterImportEntry ToOption(MplsPlaylistProjection projection, int playItemIndex)
    {
        var playItem = projection.Playlist.PlayList.PlayItems[playItemIndex];
        var chapters = projection.ChaptersForPlayItem(playItemIndex);
        var info = new ChapterSet(
            string.Empty,
            playItem.FullName,
            ChapterImportFormat.Mpls,
            MplsFrameRateCatalog.FromCode(playItem.STNTable.PrimaryVideoStreamEntries.FirstOrDefault()?.StreamAttributes.FrameRate),
            PtsToTime(playItem.OUTTime >= playItem.INTime ? playItem.OUTTime - playItem.INTime : 0),
            chapters);
        var refs = projection.ReferencesForPlayItem(playItemIndex);
        var displayName = projection.ClipDisplayNameForPlayItem(playItemIndex);
        return new ChapterImportEntry($"clip-{playItemIndex}", displayName, info, CanCombine: true, ReferencedMediaFiles: refs);
    }

}
