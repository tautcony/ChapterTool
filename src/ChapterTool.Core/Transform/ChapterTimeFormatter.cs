using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Formats and parses ChapterTool time strings.
/// </summary>
public sealed partial class ChapterTimeFormatter : IChapterTimeFormatter
{
    /// <summary>
    /// Gets the export format.
    /// </summary>
    /// <param name="time">The chapter time.</param>
    /// <returns>The operation result.</returns>
    public string Format(TimeSpan time)
    {
        var millisecond = (int)Math.Round(
            (time.TotalSeconds - Math.Floor(time.TotalSeconds)) * 1000,
            MidpointRounding.ToEven);

        var seconds = millisecond == 1000
            ? $"{time.Seconds + 1:D2}.000"
            : $"{time.Seconds:D2}.{millisecond:D3}";

        return $"{time.Hours:D2}:{time.Minutes:D2}:{seconds}";
    }

    /// <summary>
    /// Executes the ParseOrZero operation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The operation result.</returns>
    public TimeSpan ParseOrZero(string text)
    {
        return TryParse(text, out var value) ? value : TimeSpan.Zero;
    }

    /// <summary>
    /// Executes the Parse operation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The operation result.</returns>
    public TimeParseResult Parse(string text)
    {
        if (TryParse(text, out var value))
        {
            return new TimeParseResult(value, []);
        }

        return new TimeParseResult(
            TimeSpan.Zero,
            [
                new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    "InvalidTimeText",
                    "Time text is empty or does not match the legacy HH:mm:ss.sss format.")
            ]);
    }

    /// <summary>
    /// Executes the FormatCue operation.
    /// </summary>
    /// <param name="time">The chapter time.</param>
    /// <returns>The operation result.</returns>
    public string FormatCue(TimeSpan time)
    {
        var frames = (int)Math.Round(time.Milliseconds * 75 / 1000F, MidpointRounding.ToEven);
        if (frames > 99)
        {
            frames = 99;
        }

        return $"{time.Hours * 60 + time.Minutes:D2}:{time.Seconds:D2}:{frames:D2}";
    }

    private static bool TryParse(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = LegacyTimeRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["Hour"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(match.Groups["Minute"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            || !int.TryParse(match.Groups["Second"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var second)
            || !int.TryParse(match.Groups["Millisecond"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var millisecond))
        {
            return false;
        }

        try
        {
            value = new TimeSpan(0, hour, minute, second, millisecond);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = TimeSpan.Zero;
            return false;
        }
        catch (OverflowException)
        {
            value = TimeSpan.Zero;
            return false;
        }
    }

    [GeneratedRegex(@"(?<Hour>\d+)\s*:\s*(?<Minute>\d+)\s*:\s*(?<Second>\d+)\s*[\.,]\s*(?<Millisecond>\d{3})")]
    private static partial Regex LegacyTimeRegex();
}
