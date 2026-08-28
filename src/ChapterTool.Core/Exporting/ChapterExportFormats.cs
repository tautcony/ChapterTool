namespace ChapterTool.Core.Exporting;

/// <summary>
/// Provides stable metadata for supported chapter export formats.
/// </summary>
public static class ChapterExportFormats
{
    private static readonly IReadOnlyDictionary<ChapterExportFormat, (string Code, string Extension, string DisplayName, string Description)> Definitions =
        new Dictionary<ChapterExportFormat, (string, string, string, string)>
        {
            [ChapterExportFormat.Txt] = ("txt", ".txt", "TXT", "OGM chapter pairs"),
            [ChapterExportFormat.Xml] = ("xml", ".xml", "XML", "Matroska chapter XML"),
            [ChapterExportFormat.Qpfile] = ("qpf", ".qpf", "QPFile", "QPFile keyframe list"),
            [ChapterExportFormat.TimeCodes] = ("timecodes", ".TimeCodes.txt", "TimeCodes", "Chapter start times only"),
            [ChapterExportFormat.TsMuxerMeta] = ("tsmuxer", ".TsMuxeR_Meta.txt", "TsmuxerMeta", "tsMuxeR meta chapter list"),
            [ChapterExportFormat.Cue] = ("cue", ".cue", "CUE", "CUE sheet"),
            [ChapterExportFormat.Json] = ("json", ".json", "JSON", "Structured JSON chapter payload"),
            [ChapterExportFormat.WebVtt] = ("vtt", ".vtt", "WebVTT", "WebVTT cue list"),
            [ChapterExportFormat.Celltimes] = ("celltimes", ".txt", "Celltimes", "Celltimes frame list")
        };
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
    public static string Code(ChapterExportFormat format) => Definitions.TryGetValue(format, out var definition) ? definition.Code : string.Empty;

    /// <summary>
    /// Returns the default file extension for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The default file extension, including the leading dot.</returns>
    public static string Extension(ChapterExportFormat format) => Definitions.TryGetValue(format, out var definition) ? definition.Extension : string.Empty;

    /// <summary>
    /// Returns the short user-facing label for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The display label.</returns>
    public static string DisplayName(ChapterExportFormat format) => Definitions.TryGetValue(format, out var definition) ? definition.DisplayName : string.Empty;

    /// <summary>
    /// Returns the CLI description for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The description.</returns>
    public static string Description(ChapterExportFormat format) => Definitions.TryGetValue(format, out var definition) ? definition.Description : string.Empty;
}
