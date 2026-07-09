namespace ChapterTool.Core.Importing.Media;

/// <summary>
/// Represents raw chapter metadata read from a media container.
/// </summary>
/// <param name="Id">The chapter identifier reported by the media container, when available.</param>
/// <param name="TimeBase">The media time base used by <paramref name="Start"/> and <paramref name="End"/>.</param>
/// <param name="Start">The raw chapter start timestamp in media time-base units, when available.</param>
/// <param name="End">The raw chapter end timestamp in media time-base units, when available.</param>
/// <param name="StartTime">The formatted chapter start time reported by the media reader, when available.</param>
/// <param name="EndTime">The formatted chapter end time reported by the media reader, when available.</param>
/// <param name="Tags">Container tags associated with the chapter.</param>
/// <param name="SourceOrder">The zero-based order in which the media reader returned the chapter.</param>
public sealed record MediaChapterEntry(
    int? Id,
    string? TimeBase,
    long? Start,
    long? End,
    string? StartTime,
    string? EndTime,
    IReadOnlyDictionary<string, string> Tags,
    int SourceOrder);
