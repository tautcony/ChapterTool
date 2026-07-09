namespace ChapterTool.Core.Models;

/// <summary>
/// Identifies the origin format or importer family for a chapter set.
/// </summary>
public enum ChapterImportFormat
{
    /// <summary>
    /// Import format is unknown or intentionally empty.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// OGM-style text chapters.
    /// </summary>
    Ogm = 10,

    /// <summary>
    /// Matroska XML chapters.
    /// </summary>
    MatroskaXml = 20,

    /// <summary>
    /// WebVTT chapter cues.
    /// </summary>
    WebVtt = 30,

    /// <summary>
    /// CUE sheet chapters.
    /// </summary>
    Cue = 40,

    /// <summary>
    /// Adobe Premiere Pro marker list chapters.
    /// </summary>
    PremiereMarkers = 50,

    /// <summary>
    /// Blu-ray MPLS playlist chapters.
    /// </summary>
    Mpls = 60,

    /// <summary>
    /// DVD IFO chapters.
    /// </summary>
    DvdIfo = 70,

    /// <summary>
    /// HD-DVD XPL chapters.
    /// </summary>
    HdDvdXpl = 80,

    /// <summary>
    /// Generic media-container chapters read by media metadata readers.
    /// </summary>
    Media = 90,

    /// <summary>
    /// Blu-ray disc chapters discovered through BDMV/eac3to integration.
    /// </summary>
    Bdmv = 100
}
