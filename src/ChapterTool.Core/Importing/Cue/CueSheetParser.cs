using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Cue;

public sealed partial class CueSheetParser
{
    public static ChapterImportResult Parse(string text, string path = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ChapterImportResult.Failed(Error("EmptyCueFile", "CUE text is empty."));
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
                currentNumber = int.Parse(trackMatch.Groups["Number"].Value, CultureInfo.InvariantCulture);
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

                var index = int.Parse(indexMatch.Groups["Index"].Value, CultureInfo.InvariantCulture);
                if (index == 0)
                {
                    continue;
                }

                if (index != 1 || currentNumber == 0)
                {
                    malformed = true;
                    break;
                }

                chapters.Add(new Chapter(currentNumber, ParseCueTime(indexMatch), currentName));
            }
        }

        if (malformed)
        {
            return ChapterImportResult.Failed(Error("MalformedCueSyntax", "CUE index syntax is unsupported or malformed."));
        }

        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error("EmptyCueFile", "No CUE chapters were parsed."));
        }

        var ordered = chapters.OrderBy(static chapter => chapter.Number).ToList();
        var info = new ChapterInfo(
            title,
            sourceName.Length == 0 ? Path.GetFileName(path) : sourceName,
            0,
            "CUE",
            0,
            ordered[^1].Time,
            ordered,
            Tag: text,
            TagType: typeof(string).FullName);
        var option = new ChapterSourceOption("default", "CUE Chapters", info);
        return new ChapterImportResult(true, [new ChapterInfoGroup(path, [option])], []);
    }

    private static TimeSpan ParseCueTime(Match match)
    {
        var minute = int.Parse(match.Groups["Minute"].Value, CultureInfo.InvariantCulture);
        var second = int.Parse(match.Groups["Second"].Value, CultureInfo.InvariantCulture);
        var frame = int.Parse(match.Groups["Frame"].Value, CultureInfo.InvariantCulture);
        var millisecond = (int)Math.Round(frame * (1000F / 75), MidpointRounding.ToEven);
        return new TimeSpan(0, 0, minute, second, millisecond);
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    [GeneratedRegex(@"^TITLE\s+""(?<Title>.+)""$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^FILE\s+""(?<Name>.+)""\s+(WAVE|MP3|AIFF|BINARY|MOTOROLA)$", RegexOptions.IgnoreCase)]
    private static partial Regex FileRegex();

    [GeneratedRegex(@"^TRACK\s+(?<Number>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TrackRegex();

    [GeneratedRegex(@"^PERFORMER\s+""(?<Performer>.+)""$", RegexOptions.IgnoreCase)]
    private static partial Regex PerformerRegex();

    [GeneratedRegex(@"^INDEX\s+(?<Index>\d+)\s+(?<Minute>\d{2,}):(?<Second>\d{2}):(?<Frame>\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRegex();
}
