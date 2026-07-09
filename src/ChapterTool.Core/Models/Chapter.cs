namespace ChapterTool.Core.Models;

/// <summary>
/// Represents a single chapter row with timing, naming, frame metadata, and structural role.
/// </summary>
/// <param name="DisplayNumber">The output-facing chapter number.</param>
/// <param name="StartTime">The chapter start time.</param>
/// <param name="Name">The chapter display name.</param>
/// <param name="FramesInfo">The frame display/edit text associated with the chapter.</param>
/// <param name="EndTime">The optional chapter end time.</param>
/// <param name="FrameAccuracy">Whether the start time lands exactly on a frame boundary.</param>
/// <param name="Kind">The chapter row kind.</param>
public sealed record Chapter(
    int DisplayNumber,
    TimeSpan StartTime,
    string Name,
    string FramesInfo = "",
    TimeSpan? EndTime = null,
    FrameAccuracy FrameAccuracy = FrameAccuracy.Neutral,
    ChapterKind Kind = ChapterKind.Marker)
{
    /// <summary>
    /// Gets whether this row is a structural separator rather than a real chapter marker.
    /// </summary>
    public bool IsSeparator => Kind == ChapterKind.Separator;

    /// <summary>
    /// Creates a structural separator row.
    /// </summary>
    /// <param name="name">The optional separator label.</param>
    /// <returns>A separator chapter row.</returns>
    public static Chapter Separator(string name = "") =>
        new(0, TimeSpan.Zero, name, Kind: ChapterKind.Separator);
}

/// <summary>
/// Identifies whether a chapter row is a real marker or a structural separator.
/// </summary>
public enum ChapterKind
{
    /// <summary>
    /// A real chapter marker with a meaningful start time.
    /// </summary>
    Marker,

    /// <summary>
    /// A structural separator row used to split chapter sections.
    /// </summary>
    Separator
}

/// <summary>
/// Identifies whether a chapter time lands exactly on a frame boundary.
/// </summary>
public enum FrameAccuracy
{
    /// <summary>
    /// Frame accuracy was not evaluated.
    /// </summary>
    Neutral,
    /// <summary>
    /// The value is frame accurate.
    /// </summary>
    Accurate,
    /// <summary>
    /// The value does not land exactly on a frame boundary.
    /// </summary>
    Inexact
}
