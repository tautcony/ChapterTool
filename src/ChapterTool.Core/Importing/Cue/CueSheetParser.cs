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

        var state = new CueParseState();

        foreach (var rawLine in text.Split('\n'))
        {
            if (!TryApplyLine(rawLine, state))
            {
                break;
            }
        }

        if (state.Malformed)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.MalformedCueSyntax, "CUE index syntax is unsupported or malformed."));
        }

        if (state.Chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.EmptyCueFile, "No CUE chapters were parsed."));
        }

        var ordered = state.Chapters.OrderBy(static chapter => chapter.DisplayNumber).ToList();
        var info = new ChapterSet(
            state.Title,
            state.SourceName.Length == 0 ? Path.GetFileName(path) : state.SourceName,
            ChapterImportFormat.Cue,
            0,
            ordered[^1].StartTime,
            ordered);
        var entry = new ChapterImportEntry("default", "CUE Chapters", info);
        return new ChapterImportResult(true, [new ChapterImportSource(path, [entry])], []);
    }

    private static bool TryApplyLine(string rawLine, CueParseState state)
    {
        var line = rawLine.Trim();
        if (line.Length == 0)
        {
            return true;
        }

        var trackMatch = TrackRegex().Match(line);
        if (trackMatch.Success)
        {
            return TryApplyTrack(trackMatch, state);
        }

        var fileMatch = FileRegex().Match(line);
        if (fileMatch.Success && state.SourceName.Length == 0)
        {
            state.SourceName = fileMatch.Groups["Name"].Value;
            return true;
        }

        var titleMatch = TitleRegex().Match(line);
        if (titleMatch.Success)
        {
            return TryApplyTitle(titleMatch, state);
        }

        var performerMatch = PerformerRegex().Match(line);
        if (performerMatch.Success && state.CurrentNumber != 0)
        {
            state.CurrentName += $" [{performerMatch.Groups["Performer"].Value}]";
            return true;
        }

        if (!line.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryApplyIndex(line, state);
    }

    private static bool TryApplyTrack(Match trackMatch, CueParseState state)
    {
        if (!int.TryParse(trackMatch.Groups["Number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var currentNumber))
        {
            state.Malformed = true;
            return false;
        }

        state.CurrentNumber = currentNumber;
        state.CurrentName = string.Empty;
        return true;
    }

    private static bool TryApplyTitle(Match titleMatch, CueParseState state)
    {
        if (state.CurrentNumber == 0)
        {
            state.Title = titleMatch.Groups["Title"].Value;
        }
        else
        {
            state.CurrentName = titleMatch.Groups["Title"].Value;
        }

        return true;
    }

    private static bool TryApplyIndex(string line, CueParseState state)
    {
        var indexMatch = IndexRegex().Match(line);
        if (!indexMatch.Success || !int.TryParse(indexMatch.Groups["Index"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            state.Malformed = true;
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        if (index != 1 || state.CurrentNumber == 0 || !TryParseCueTime(indexMatch, out var time))
        {
            state.Malformed = true;
            return false;
        }

        state.Chapters.Add(new Chapter(state.CurrentNumber, time, state.CurrentName));
        return true;
    }

    private sealed class CueParseState
    {
        internal string Title { get; set; } = string.Empty;

        internal string SourceName { get; set; } = string.Empty;

        internal List<Chapter> Chapters { get; } = [];

        internal int CurrentNumber { get; set; }

        internal string CurrentName { get; set; } = string.Empty;

        internal bool Malformed { get; set; }
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
