namespace ChapterTool.Core.Importing;

/// <summary>
/// Receives semantic chapter import progress updates.
/// </summary>
public interface IChapterImportProgressReporter
{
    /// <summary>
    /// Reports import progress.
    /// </summary>
    /// <param name="progress">The import progress update.</param>
    void Report(ChapterImportProgress progress);
}

/// <summary>
/// Identifies the current phase of a chapter import operation.
/// </summary>
public enum ChapterImportProgressPhase
{
    /// <summary>
    /// The source is being opened or prepared.
    /// </summary>
    LoadingSource,

    /// <summary>
    /// The source structure or required dependencies are being validated.
    /// </summary>
    ValidatingSource,

    /// <summary>
    /// Candidate chapter-bearing titles or streams are being discovered.
    /// </summary>
    DiscoveringTitles,

    /// <summary>
    /// Chapter text is being exported from an external source.
    /// </summary>
    ExportingChapters,

    /// <summary>
    /// Exported or embedded chapter text is being parsed.
    /// </summary>
    ParsingChapters
}

/// <summary>
/// Reports semantic chapter import progress.
/// </summary>
/// <param name="Phase">The import phase currently being performed.</param>
/// <param name="Fraction">The optional progress fraction, typically from 0 to 1.</param>
/// <param name="SourceName">The source item currently being processed, when available.</param>
/// <param name="Current">The one-based item index currently being processed, when available.</param>
/// <param name="Total">The total item count for the current phase, when available.</param>
public sealed record ChapterImportProgress(
    ChapterImportProgressPhase Phase,
    double? Fraction = null,
    string? SourceName = null,
    int? Current = null,
    int? Total = null);
