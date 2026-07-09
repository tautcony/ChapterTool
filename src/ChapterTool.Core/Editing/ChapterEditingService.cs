using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Provides chapter editing operations for ChapterTool chapter data.
/// </summary>
/// <param name="timeFormatter">The chapter time formatter.</param>
public sealed partial class ChapterEditingService(IChapterTimeFormatter timeFormatter) : IChapterEditingService
{
    /// <summary>
    /// Executes the EditTime operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="text">The text to parse.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult EditTime(ChapterSet info, int index, string text)
    {
        var chapters = info.Chapters.ToList();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        var parsed = timeFormatter.Parse(text);
        var value = parsed.Value >= TimeSpan.FromDays(1) ? TimeSpan.Zero : parsed.Value;
        chapters[index] = chapter with { StartTime = value };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, parsed.Diagnostics);
    }

    /// <summary>
    /// Executes the EditFrame operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="text">The text to parse.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult EditFrame(ChapterSet info, int index, string text, decimal framesPerSecond)
    {
        var chapters = info.Chapters.ToList();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        var match = FirstIntegerRegex().Match(text);
        if (!match.Success
            || framesPerSecond <= 0
            || !decimal.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame))
        {
            return InvalidFrameText(info);
        }

        var seconds = frame / framesPerSecond;
        if (seconds < 0 || seconds > (decimal)TimeSpan.MaxValue.TotalSeconds)
        {
            return InvalidFrameText(info);
        }

        chapters[index] = chapter with
        {
            StartTime = TimeSpan.FromSeconds((double)seconds),
            FramesInfo = frame.ToString("0", CultureInfo.InvariantCulture),
            FrameAccuracy = FrameAccuracy.Accurate
        };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
    }

    private static ChapterEditResult InvalidFrameText(ChapterSet info) =>
        new(
            info,
            [new ChapterDiagnostic(DiagnosticSeverity.Warning, "InvalidFrameText", "Frame text did not contain a frame number or fps was invalid.")]);

    /// <summary>
    /// Executes the Rename operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <param name="name">The chapter name.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult Rename(ChapterSet info, int index, string name)
    {
        var chapters = info.Chapters.ToList();
        if (!TryGetChapter(chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        chapters[index] = chapter with { Name = name };
        return new ChapterEditResult(info with { Chapters = chapters }, []);
    }

    /// <summary>
    /// Executes the Delete operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="indexes">The zero-based chapter indexes.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult Delete(ChapterSet info, IReadOnlySet<int> indexes)
    {
        var chapters = info.Chapters.Where((_, index) => !indexes.Contains(index)).ToList();
        if (chapters.Count > 0 && indexes.Contains(0))
        {
            var shift = chapters[0].StartTime;
            chapters = chapters.Select(chapter => chapter.IsSeparator ? chapter : chapter with { StartTime = chapter.StartTime - shift }).ToList();
        }

        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
    }

    /// <summary>
    /// Executes the InsertBefore operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="index">The zero-based chapter index.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult InsertBefore(ChapterSet info, int index)
    {
        if (index < 0 || index > info.Chapters.Count)
        {
            return InvalidIndex(info, index);
        }

        var chapters = info.Chapters.ToList();
        chapters.Insert(index, new Chapter(0, TimeSpan.Zero, "New Chapter"));
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
    }

    /// <summary>
    /// Executes the ApplyOrderShift operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="shift">The order shift.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult ApplyOrderShift(ChapterSet info, int shift)
    {
        var effectiveShift = Math.Max(0, shift);
        var number = 0;
        var chapters = info.Chapters
            .Select(chapter => chapter.IsSeparator ? chapter with { DisplayNumber = 0 } : chapter with { DisplayNumber = ++number + effectiveShift })
            .ToList();
        var diagnostics = effectiveShift == shift
            ? Array.Empty<ChapterDiagnostic>()
            :
            [
                new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    "OrderShiftNormalized",
                    $"Chapter number shift {shift} would produce non-positive chapter numbers and was normalized to 0.",
                    Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["shift"] = shift })
            ];
        return new ChapterEditResult(info with { Chapters = chapters }, diagnostics);
    }

    /// <summary>
    /// Executes the ApplyTemplate operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="templateText">The template text.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult ApplyTemplate(ChapterSet info, string templateText)
    {
        var names = templateText
            .Trim(' ', '\r', '\n')
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .ToList();
        var chapters = info.Chapters
            .Select((chapter, index) => index < names.Count && names[index].Length > 0 ? chapter with { Name = names[index] } : chapter)
            .ToList();
        return new ChapterEditResult(info with { Chapters = chapters }, []);
    }

    /// <summary>
    /// Executes the ShiftFramesForward operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="frames">The frame count.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult ShiftFramesForward(ChapterSet info, int frames, decimal framesPerSecond)
    {
        if (framesPerSecond <= 0)
        {
            return new ChapterEditResult(
                info,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidFrameRate", "Frame rate must be greater than zero.")]);
        }

        var shift = ChapterRounding.SecondsToTimeSpan(frames / framesPerSecond);
        var chapters = info.Chapters
            .Select(chapter => chapter.IsSeparator ? chapter : chapter with { StartTime = chapter.StartTime - shift })
            .Where(static chapter => chapter.IsSeparator || chapter.StartTime >= TimeSpan.Zero);

        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
    }

    /// <summary>
    /// Executes the CreateZones operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="indexes">The zero-based chapter indexes.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The operation result.</returns>
    public ChapterZonesResult CreateZones(ChapterSet info, IReadOnlySet<int> indexes, decimal framesPerSecond)
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
        return new ChapterZonesResult(zones, []);
    }

    private static List<Chapter> Renumber(IEnumerable<Chapter> chapters)
    {
        var number = 0;
        return chapters
            .Select(chapter => chapter.IsSeparator ? chapter : chapter with { DisplayNumber = ++number })
            .ToList();
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

    private static ChapterEditResult InvalidIndex(ChapterSet info, int index) =>
        new(
            info,
            [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidChapterIndex", $"Chapter index {index} is out of range.",
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["index"] = index })]);

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstIntegerRegex();

    private static long ChapterFrame(Chapter chapter, decimal framesPerSecond)
    {
        return ChapterRounding.RoundToInt64((decimal)chapter.StartTime.TotalSeconds * framesPerSecond);
    }
}
