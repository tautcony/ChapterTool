using ChapterTool.Core.Models;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Defines a chapter exporter for a concrete output format.
/// </summary>
public interface IChapterExporter
{
    /// <summary>
    /// Gets the export format handled by this exporter.
    /// </summary>
    ChapterExportFormat Format { get; }

    /// <summary>
    /// Exports chapter data with the supplied options.
    /// </summary>
    /// <param name="chapterSet">The chapter data to export.</param>
    /// <param name="options">The export options.</param>
    /// <returns>The export result.</returns>
    ChapterExportResult Export(ChapterSet chapterSet, ChapterExportOptions options);
}
