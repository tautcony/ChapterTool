namespace ChapterTool.Core.Models;

/// <summary>
/// Provides stable metadata for chapter source types.
/// </summary>
public static class ChapterImportFormats
{
    /// <summary>
    /// Returns the stable machine code for a chapter source type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The stable code.</returns>
    public static string Code(ChapterImportFormat sourceType) => sourceType switch
    {
        ChapterImportFormat.Ogm => "ogm",
        ChapterImportFormat.MatroskaXml => "matroska-xml",
        ChapterImportFormat.WebVtt => "webvtt",
        ChapterImportFormat.Cue => "cue",
        ChapterImportFormat.PremiereMarkers => "premiere-markers",
        ChapterImportFormat.Mpls => "mpls",
        ChapterImportFormat.DvdIfo => "dvd-ifo",
        ChapterImportFormat.HdDvdXpl => "hddvd-xpl",
        ChapterImportFormat.Media => "media",
        ChapterImportFormat.Bdmv => "bdmv",
        _ => "unknown"
    };

    /// <summary>
    /// Returns the user-facing display name for a chapter source type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(ChapterImportFormat sourceType) => sourceType switch
    {
        ChapterImportFormat.Ogm => "OGM",
        ChapterImportFormat.MatroskaXml => "Matroska XML",
        ChapterImportFormat.WebVtt => "WebVTT",
        ChapterImportFormat.Cue => "CUE",
        ChapterImportFormat.PremiereMarkers => "Adobe Premiere Pro markers",
        ChapterImportFormat.Mpls => "Blu-ray MPLS",
        ChapterImportFormat.DvdIfo => "DVD IFO",
        ChapterImportFormat.HdDvdXpl => "HD-DVD XPL",
        ChapterImportFormat.Media => "Media metadata",
        ChapterImportFormat.Bdmv => "BDMV",
        _ => string.Empty
    };
}
