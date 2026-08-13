using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Cue;

/// <summary>
/// Parses CUE sheet text into ChapterTool chapter data.
/// </summary>
public sealed partial class CueSheetParser
{
    /// <summary>
    /// Executes the Parse operation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="path">The source path.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult Parse(string text, string path = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.EmptyCueFile, "CUE text is empty."));
        }

        var title = string.Empty;
        var sourceName = string.Empty;
        var chapters = new List<Chapter>();
        var currentNumber = 0;
        var currentName = string.Empty;
        var malformed = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var titleMatch = TitleRegex().Match(line);
            var fileMatch = FileRegex().Match(line);
            var trackMatch = TrackRegex().Match(line);
            var performerMatch = PerformerRegex().Match(line);
            var indexMatch = IndexRegex().Match(line);

            if (trackMatch.Success)
            {
                if (!int.TryParse(trackMatch.Groups["Number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out currentNumber))
                {
                    malformed = true;
                    break;
                }

                currentName = string.Empty;
                continue;
            }

            if (fileMatch.Success && sourceName.Length == 0)
            {
                sourceName = fileMatch.Groups["Name"].Value;
                continue;
            }

            if (titleMatch.Success)
            {
                if (currentNumber == 0)
                {
                    title = titleMatch.Groups["Title"].Value;
                }
                else
                {
                    currentName = titleMatch.Groups["Title"].Value;
                }

                continue;
            }

            if (performerMatch.Success && currentNumber != 0)
            {
                currentName += $" [{performerMatch.Groups["Performer"].Value}]";
                continue;
            }

            if (line.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                if (!indexMatch.Success)
                {
                    malformed = true;
                    break;
                }

                if (!int.TryParse(indexMatch.Groups["Index"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    malformed = true;
                    break;
                }

                if (index == 0)
                {
                    continue;
                }

                if (index != 1 || currentNumber == 0 || !TryParseCueTime(indexMatch, out var time))
                {
                    malformed = true;
                    break;
                }

                chapters.Add(new Chapter(currentNumber, time, currentName));
            }
        }

        if (malformed)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.MalformedCueSyntax, "CUE index syntax is unsupported or malformed."));
        }

        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.EmptyCueFile, "No CUE chapters were parsed."));
        }

        var ordered = chapters.OrderBy(static chapter => chapter.DisplayNumber).ToList();
        var info = new ChapterSet(
            title,
            sourceName.Length == 0 ? Path.GetFileName(path) : sourceName,
            ChapterImportFormat.Cue,
            0,
            ordered[^1].StartTime,
            ordered);
        var entry = new ChapterImportEntry("default", "CUE Chapters", info);
        return new ChapterImportResult(true, [new ChapterImportSource(path, [entry])], []);
    }

    private static bool TryParseCueTime(Match match, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (!int.TryParse(match.Groups["Minute"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            || !int.TryParse(match.Groups["Second"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var second)
            || !int.TryParse(match.Groups["Frame"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var frame))
        {
            return false;
        }

        var millisecond = (int)Math.Round(frame * (1000F / 75), MidpointRounding.ToEven);

        // Compute ticks in 64-bit so large minute values cannot overflow the TimeSpan constructor.
        var ticks = (minute * 60L + second) * TimeSpan.TicksPerSecond + millisecond * TimeSpan.TicksPerMillisecond;
        time = TimeSpan.FromTicks(ticks);
        return true;
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    [GeneratedRegex("""^TITLE\s+"(?<Title>.+)"$""", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("""^FILE\s+"(?<Name>.+)"\s+(WAVE|MP3|AIFF|BINARY|MOTOROLA)$""", RegexOptions.IgnoreCase)]
    private static partial Regex FileRegex();

    [GeneratedRegex(@"^TRACK\s+(?<Number>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TrackRegex();

    [GeneratedRegex("""^PERFORMER\s+"(?<Performer>.+)"$""", RegexOptions.IgnoreCase)]
    private static partial Regex PerformerRegex();

    [GeneratedRegex(@"^INDEX\s+(?<Index>\d+)\s+(?<Minute>\d{2,}):(?<Second>\d{2}):(?<Frame>\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRegex();
}
