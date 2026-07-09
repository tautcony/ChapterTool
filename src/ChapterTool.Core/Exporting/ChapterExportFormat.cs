namespace ChapterTool.Core.Exporting;

/// <summary>
/// Identifies a supported chapter export format.
/// </summary>
public enum ChapterExportFormat
{
    /// <summary>
    /// Exports OGM-style chapter text.
    /// </summary>
    Txt = 10,
    /// <summary>
    /// Exports Matroska XML chapters.
    /// </summary>
    Xml = 20,
    /// <summary>
    /// Exports QPFile frame markers.
    /// </summary>
    Qpfile = 30,
    /// <summary>
    /// Exports timestamp lines.
    /// </summary>
    TimeCodes = 40,
    /// <summary>
    /// Exports tsMuxeR metadata.
    /// </summary>
    TsMuxerMeta = 50,
    /// <summary>
    /// Exports a CUE sheet.
    /// </summary>
    Cue = 60,
    /// <summary>
    /// Exports JSON chapter data.
    /// </summary>
    Json = 70,
    /// <summary>
    /// Exports WebVTT cues.
    /// </summary>
    WebVtt = 80,
    /// <summary>
    /// Exports celltimes frame numbers.
    /// </summary>
    Celltimes = 90
}
