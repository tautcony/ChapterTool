using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public sealed partial class ChapterTimeFormatter : IChapterTimeFormatter
{
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

    public TimeSpan ParseOrZero(string text)
    {
        return TryParse(text, out var value) ? value : TimeSpan.Zero;
    }

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

        var hour = int.Parse(match.Groups["Hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["Minute"].Value, CultureInfo.InvariantCulture);
        var second = int.Parse(match.Groups["Second"].Value, CultureInfo.InvariantCulture);
        var millisecond = int.Parse(match.Groups["Millisecond"].Value, CultureInfo.InvariantCulture);
        value = new TimeSpan(0, hour, minute, second, millisecond);
        return true;
    }

    [GeneratedRegex(@"(?<Hour>\d+)\s*:\s*(?<Minute>\d+)\s*:\s*(?<Second>\d+)\s*[\.,]\s*(?<Millisecond>\d{3})")]
    private static partial Regex LegacyTimeRegex();
}
