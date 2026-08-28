namespace ChapterTool.Core.Models;

/// <summary>
/// Provides stable metadata for chapter source types.
/// </summary>
public static class ChapterImportFormats
{
    private static readonly IReadOnlyDictionary<ChapterImportFormat, (string Code, string DisplayName)> Definitions =
        new Dictionary<ChapterImportFormat, (string, string)>
        {
            [ChapterImportFormat.Ogm] = ("ogm", "OGM"),
            [ChapterImportFormat.MatroskaXml] = ("matroska-xml", "Matroska XML"),
            [ChapterImportFormat.WebVtt] = ("webvtt", "WebVTT"),
            [ChapterImportFormat.Cue] = ("cue", "CUE"),
            [ChapterImportFormat.PremiereMarkers] = ("premiere-markers", "Adobe Premiere Pro markers"),
            [ChapterImportFormat.Mpls] = ("mpls", "Blu-ray MPLS"),
            [ChapterImportFormat.DvdIfo] = ("dvd-ifo", "DVD IFO"),
            [ChapterImportFormat.HdDvdXpl] = ("hddvd-xpl", "HD-DVD XPL"),
            [ChapterImportFormat.Media] = ("media", "Media metadata"),
            [ChapterImportFormat.Bdmv] = ("bdmv", "BDMV")
        };
    /// <summary>
    /// Returns the stable machine code for a chapter source type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The stable code.</returns>
    public static string Code(ChapterImportFormat sourceType) => Definitions.TryGetValue(sourceType, out var definition) ? definition.Code : "unknown";

    /// <summary>
    /// Returns the user-facing display name for a chapter source type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(ChapterImportFormat sourceType) => Definitions.TryGetValue(sourceType, out var definition) ? definition.DisplayName : string.Empty;
}
