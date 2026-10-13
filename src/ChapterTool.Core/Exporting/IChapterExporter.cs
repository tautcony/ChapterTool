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

}
