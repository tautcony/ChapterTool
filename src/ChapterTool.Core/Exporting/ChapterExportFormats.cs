namespace ChapterTool.Core.Exporting;

/// <summary>
/// Provides stable metadata for supported chapter export formats.
/// </summary>
public static class ChapterExportFormats
{
    /// <summary>
    /// Supported export formats in UI and CLI presentation order.
    /// </summary>
    public static IReadOnlyList<ChapterExportFormat> All { get; } =
    [
        ChapterExportFormat.Txt,
        ChapterExportFormat.Xml,
        ChapterExportFormat.Qpfile,
        ChapterExportFormat.TimeCodes,
        ChapterExportFormat.TsMuxerMeta,
        ChapterExportFormat.Cue,
        ChapterExportFormat.Json,
        ChapterExportFormat.WebVtt,
        ChapterExportFormat.Celltimes
    ];

    /// <summary>
    /// Returns the presentation index for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The zero-based index, or -1 when the value is unsupported.</returns>
    public static int IndexOf(ChapterExportFormat format)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index] == format)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the export format at a clamped presentation index.
    /// </summary>
    /// <param name="index">The requested index.</param>
    /// <returns>The matching export format.</returns>
    public static ChapterExportFormat AtIndex(int index) => All[Math.Clamp(index, 0, All.Count - 1)];

    /// <summary>
    /// Returns the stable machine code for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The stable code.</returns>
    public static string Code(ChapterExportFormat format) => format switch
    {
        ChapterExportFormat.Txt => "txt",
        ChapterExportFormat.Xml => "xml",
        ChapterExportFormat.Qpfile => "qpf",
        ChapterExportFormat.TimeCodes => "timecodes",
        ChapterExportFormat.TsMuxerMeta => "tsmuxer",
        ChapterExportFormat.Cue => "cue",
        ChapterExportFormat.Json => "json",
        ChapterExportFormat.WebVtt => "vtt",
        ChapterExportFormat.Celltimes => "celltimes",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the default file extension for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The default file extension, including the leading dot.</returns>
    public static string Extension(ChapterExportFormat format) => format switch
    {
        ChapterExportFormat.Txt => ".txt",
        ChapterExportFormat.Xml => ".xml",
        ChapterExportFormat.Qpfile => ".qpf",
        ChapterExportFormat.TimeCodes => ".TimeCodes.txt",
        ChapterExportFormat.TsMuxerMeta => ".TsMuxeR_Meta.txt",
        ChapterExportFormat.Cue => ".cue",
        ChapterExportFormat.Json => ".json",
        ChapterExportFormat.WebVtt => ".vtt",
        ChapterExportFormat.Celltimes => ".txt",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the short user-facing label for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The display label.</returns>
    public static string DisplayName(ChapterExportFormat format) => format switch
    {
        ChapterExportFormat.Txt => "TXT",
        ChapterExportFormat.Xml => "XML",
        ChapterExportFormat.Qpfile => "QPFile",
        ChapterExportFormat.TimeCodes => "TimeCodes",
        ChapterExportFormat.TsMuxerMeta => "TsmuxerMeta",
        ChapterExportFormat.Cue => "CUE",
        ChapterExportFormat.Json => "JSON",
        ChapterExportFormat.WebVtt => "WebVTT",
        ChapterExportFormat.Celltimes => "Celltimes",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the CLI description for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The description.</returns>
    public static string Description(ChapterExportFormat format) => format switch
    {
        ChapterExportFormat.Txt => "OGM chapter pairs",
        ChapterExportFormat.Xml => "Matroska chapter XML",
        ChapterExportFormat.Qpfile => "QPFile keyframe list",
        ChapterExportFormat.TimeCodes => "Chapter start times only",
        ChapterExportFormat.TsMuxerMeta => "tsMuxeR meta chapter list",
        ChapterExportFormat.Cue => "CUE sheet",
        ChapterExportFormat.Json => "Structured JSON chapter payload",
        ChapterExportFormat.WebVtt => "WebVTT cue list",
        ChapterExportFormat.Celltimes => "Celltimes frame list",
        _ => string.Empty
    };
}
