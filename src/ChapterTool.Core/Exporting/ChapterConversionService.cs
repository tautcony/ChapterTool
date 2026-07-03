using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Exporting;

public sealed class ChapterConversionService(IChapterTimeFormatter timeFormatter)
{
    public static ChapterConversionResult ToCelltimes(ChapterInfo info, decimal framesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (framesPerSecond <= 0)
        {
            return Failure("InvalidFrameRate", "Frame rate must be greater than zero.");
        }

        var lines = info.Chapters
            .Where(static chapter => !chapter.IsSeparator)
            .Select(chapter => ChapterRounding
                .RoundToInt64((decimal)chapter.Time.TotalSeconds * framesPerSecond)
                .ToString(CultureInfo.InvariantCulture));

        return Success(string.Join(Environment.NewLine, lines), ".celltimes.txt");
    }

    public ChapterConversionResult ChapterTextToQpfile(string chapterText, decimal framesPerSecond, string? timecodeText = null)
    {
        if (string.IsNullOrWhiteSpace(chapterText))
        {
            return Failure("InvalidChapterText", "Chapter text is empty.");
        }

        if (framesPerSecond <= 0 && string.IsNullOrWhiteSpace(timecodeText))
        {
            return Failure("InvalidFrameRate", "Frame rate must be greater than zero when no timecode file is provided.");
        }

        var times = ParseChapterTimes(chapterText);
        if (times.Count == 0)
        {
            return Failure("InvalidChapterText", "No valid OGM chapter time entries were found.");
        }

        TimecodeMap? timecodeMap = null;
        if (!string.IsNullOrWhiteSpace(timecodeText))
        {
            var parsed = TimecodeMap.Parse(timecodeText);
            if (parsed is null)
            {
                return Failure("InvalidTimecodeText", "No valid timecode entries were found.");
            }

            timecodeMap = parsed;
        }

        var lines = times.Select(time =>
        {
            var frame = timecodeMap?.FrameFor(time) ??
                ChapterRounding.RoundToInt64((decimal)time.TotalSeconds * framesPerSecond);
            return frame.ToString(CultureInfo.InvariantCulture) + " I";
        });

        return Success(string.Join(Environment.NewLine, lines), ".qpf");
    }

    private List<TimeSpan> ParseChapterTimes(string chapterText)
    {
        var times = new List<TimeSpan>();
        foreach (var rawLine in chapterText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("CHAPTER", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= "CHAPTER".Length || line.Contains("NAME=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var numberText = line["CHAPTER".Length..equals];
            if (!numberText.All(char.IsDigit))
            {
                continue;
            }

            var timeText = line[(equals + 1)..].Trim();
            var parsed = timeFormatter.Parse(timeText);
            if (parsed.Diagnostics.Count == 0)
            {
                times.Add(parsed.Value);
            }
        }

        return times;
    }

    private static ChapterConversionResult Success(string content, string extension) =>
        new(true, content, extension, []);

    private static ChapterConversionResult Failure(string code, string message) =>
        new(false, string.Empty, string.Empty, [new ChapterDiagnostic(DiagnosticSeverity.Error, code, message)]);

    private sealed class TimecodeMap
    {
        private readonly double[] milliseconds;

        private TimecodeMap(double[] milliseconds)
        {
            this.milliseconds = milliseconds;
        }

        public static TimecodeMap? Parse(string text)
        {
            var values = text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0 && char.IsDigit(line[0]))
                .Select(static line => double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : (double?)null)
                .Where(static value => value.HasValue)
                .Select(static value => value!.Value)
                .ToArray();

            return values.Length == 0 ? null : new TimecodeMap(values);
        }

        public long FrameFor(TimeSpan time)
        {
            var target = time.TotalMilliseconds - 0.5d;
            var index = Array.BinarySearch(milliseconds, target);
            if (index < 0)
            {
                return ~index;
            }

            while (index > 0 && milliseconds[index - 1] >= target)
            {
                index--;
            }

            return index;
        }
    }
}
