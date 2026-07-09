using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Formats and parses chapter time strings.
/// </summary>
public interface IChapterTimeFormatter
{
    /// <summary>
    /// Formats a time value as a ChapterTool timestamp.
    /// </summary>
    /// <param name="time">The time value to format.</param>
    /// <returns>The formatted timestamp.</returns>
    string Format(TimeSpan time);

    /// <summary>
    /// Parses a ChapterTool timestamp, returning zero when parsing fails.
    /// </summary>
    /// <param name="text">The timestamp text.</param>
    /// <returns>The parsed time, or zero when parsing fails.</returns>
    TimeSpan ParseOrZero(string text);

    /// <summary>
    /// Parses a ChapterTool timestamp and reports diagnostics.
    /// </summary>
    /// <param name="text">The timestamp text.</param>
    /// <returns>The parse result.</returns>
    TimeParseResult Parse(string text);

    /// <summary>
    /// Formats a time value as a CUE sheet timestamp.
    /// </summary>
    /// <param name="time">The time value to format.</param>
    /// <returns>The formatted CUE timestamp.</returns>
    string FormatCue(TimeSpan time);
}

/// <summary>
/// Represents the result of parsing a chapter time string.
/// </summary>
/// <param name="Value">Parsed timestamp value.</param>
/// <param name="Diagnostics">Diagnostics produced while parsing the timestamp.</param>
public sealed record TimeParseResult(
    TimeSpan Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
