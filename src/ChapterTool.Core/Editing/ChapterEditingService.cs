using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Editing;

public sealed partial class ChapterEditingService(IChapterTimeFormatter timeFormatter) : IChapterEditingService
{
    public ChapterEditResult EditTime(ChapterInfo info, int index, string text)
    {
        var chapters = info.Chapters.ToArray();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        var parsed = timeFormatter.Parse(text);
        var value = parsed.Value >= TimeSpan.FromDays(1) ? TimeSpan.Zero : parsed.Value;
        chapters[index] = chapter with { Time = value };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, parsed.Diagnostics);
    }

    public ChapterEditResult EditFrame(ChapterInfo info, int index, string text, decimal framesPerSecond)
    {
        var chapters = info.Chapters.ToArray();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        var match = FirstIntegerRegex().Match(text);
        if (!match.Success || framesPerSecond <= 0)
        {
            return new ChapterEditResult(
                info,
                [new ChapterDiagnostic(DiagnosticSeverity.Warning, "InvalidFrameText", "Frame text did not contain a frame number or fps was invalid.")]);
        }

        var frame = decimal.Parse(match.Value, CultureInfo.InvariantCulture);
        var seconds = frame / framesPerSecond;
        chapters[index] = chapter with { Time = TimeSpan.FromSeconds((double)seconds), FramesInfo = $"{frame:0} K" };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterEditResult Rename(ChapterInfo info, int index, string name)
    {
        var chapters = info.Chapters.ToArray();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        chapters[index] = chapter with { Name = name };
        return new ChapterEditResult(info with { Chapters = chapters }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterEditResult Delete(ChapterInfo info, IReadOnlySet<int> indexes)
    {
        var chapters = info.Chapters.Where((_, index) => !indexes.Contains(index)).ToArray();
        if (chapters.Length > 0 && indexes.Contains(0))
        {
            var shift = chapters[0].Time;
            chapters = chapters.Select(chapter => chapter.IsSeparator ? chapter : chapter with { Time = chapter.Time - shift }).ToArray();
        }

        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterEditResult InsertBefore(ChapterInfo info, int index)
    {
        if (index < 0 || index > info.Chapters.Count)
        {
            return InvalidIndex(info, index);
        }

        var chapters = info.Chapters.ToList();
        chapters.Insert(index, new Chapter(0, TimeSpan.Zero, "New Chapter"));
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterEditResult ApplyOrderShift(ChapterInfo info, int shift)
    {
        var effectiveShift = Math.Max(0, shift);
        var number = 0;
        var chapters = info.Chapters
            .Select(chapter => chapter.IsSeparator ? chapter with { Number = 0 } : chapter with { Number = ++number + effectiveShift })
            .ToArray();
        var diagnostics = effectiveShift == shift
            ? Array.Empty<ChapterDiagnostic>()
            :
            [
                new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    "OrderShiftNormalized",
                    $"Chapter number shift {shift} would produce non-positive chapter numbers and was normalized to 0.")
            ];
        return new ChapterEditResult(info with { Chapters = chapters }, diagnostics);
    }

    public ChapterEditResult ApplyTemplate(ChapterInfo info, string templateText)
    {
        var names = templateText
            .Trim(' ', '\r', '\n')
            .Split('\n', StringSplitOptions.None)
            .Select(static line => line.TrimEnd('\r'))
            .ToArray();
        var chapters = info.Chapters
            .Select((chapter, index) => index < names.Length && names[index].Length > 0 ? chapter with { Name = names[index] } : chapter)
            .ToArray();
        return new ChapterEditResult(info with { Chapters = chapters }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterEditResult ShiftFramesForward(ChapterInfo info, int frames, decimal framesPerSecond)
    {
        if (framesPerSecond <= 0)
        {
            return new ChapterEditResult(
                info,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidFrameRate", "Frame rate must be greater than zero.")]);
        }

        var shift = ChapterRounding.SecondsToTimeSpan(frames / framesPerSecond);
        var chapters = info.Chapters
            .Select(chapter => chapter.IsSeparator ? chapter : chapter with { Time = chapter.Time - shift })
            .Where(static chapter => chapter.IsSeparator || chapter.Time >= TimeSpan.Zero)
            .ToArray();

        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, Array.Empty<ChapterDiagnostic>());
    }

    public ChapterZonesResult CreateZones(ChapterInfo info, IReadOnlySet<int> indexes, decimal framesPerSecond)
    {
        if (framesPerSecond <= 0)
        {
            return new ChapterZonesResult(
                string.Empty,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidFrameRate", "Frame rate must be greater than zero.")]);
        }

        if (indexes.Count == 0)
        {
            return new ChapterZonesResult(
                string.Empty,
                [new ChapterDiagnostic(DiagnosticSeverity.Warning, "NoRowsSelected", "Select one or more chapter rows first.")]);
        }

        var ranges = new List<(long Begin, long End)>();
        foreach (var index in indexes.Where(index => index >= 0 && index < info.Chapters.Count))
        {
            var chapter = info.Chapters[index];
            if (chapter.IsSeparator)
            {
                continue;
            }

            var nextIndex = index >= info.Chapters.Count - 1 ? index : index + 1;
            var next = info.Chapters[nextIndex];
            var begin = ChapterFrame(chapter, framesPerSecond);
            var end = Math.Max(begin, ChapterFrame(next, framesPerSecond) - 1);
            ranges.Add((begin, end));
        }

        if (ranges.Count == 0)
        {
            return new ChapterZonesResult(
                string.Empty,
                [new ChapterDiagnostic(DiagnosticSeverity.Warning, "NoRowsSelected", "No valid chapter rows were selected.")]);
        }

        var zones = "--zones " + string.Join("/", ranges.OrderBy(static range => range.Begin).Select(static range => $"{range.Begin},{range.End},"));
        return new ChapterZonesResult(zones, Array.Empty<ChapterDiagnostic>());
    }

    private static IReadOnlyList<Chapter> Renumber(IEnumerable<Chapter> chapters)
    {
        var number = 0;
        return chapters
            .Select(chapter => chapter.IsSeparator ? chapter : chapter with { Number = ++number })
            .ToArray();
    }

    private static bool TryGetChapter(IReadOnlyList<Chapter> chapters, int index, out Chapter chapter)
    {
        if (index >= 0 && index < chapters.Count)
        {
            chapter = chapters[index];
            return true;
        }

        chapter = new Chapter(0, TimeSpan.Zero, string.Empty);
        return false;
    }

    private static ChapterEditResult InvalidIndex(ChapterInfo info, int index) =>
        new(
            info,
            [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidChapterIndex", $"Chapter index {index} is out of range.")]);

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstIntegerRegex();

    private static long ChapterFrame(Chapter chapter, decimal framesPerSecond)
    {
        var match = FirstIntegerRegex().Match(chapter.FramesInfo);
        if (match.Success)
        {
            return long.Parse(match.Value, CultureInfo.InvariantCulture);
        }

        return ChapterRounding.RoundToInt64((decimal)chapter.Time.TotalSeconds * framesPerSecond);
    }
}
