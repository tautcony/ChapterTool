using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Provides chapter editing operations for ChapterTool chapter data.
/// </summary>
/// <param name="timeFormatter">The chapter time formatter.</param>
public sealed class ChapterEditingService(IChapterTimeFormatter timeFormatter) : IChapterEditingService
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
        var diagnostics = parsed.Diagnostics.ToList();
        var value = parsed.Value;
        if (value >= TimeSpan.FromDays(1))
        {
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Warning,
                ChapterDiagnosticCode.InvalidTimeText,
                "A chapter time must be less than 24 hours."));
            return new ChapterEditResult(info, diagnostics);
        }

        chapters[index] = chapter with { StartTime = value };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, diagnostics);
    }

    /// <summary>Edits a finite decimal frame count at the nearest-tick boundary.</summary>
    public ChapterEditResult EditFrame(ChapterSet info, int index, string text, FrameRateValue framesPerSecond)
    {
        if (!TryGetChapter(info.Chapters, index, out var chapter))
        {
            return InvalidIndex(info, index);
        }

        var rate = framesPerSecond;
        if (!rate.IsKnown)
        {
            return new ChapterEditResult(info, [FrameRateValidation.InvalidFrameRate()]);
        }

        var valueText = text.Trim();
        if (valueText.EndsWith(" frames", StringComparison.OrdinalIgnoreCase))
        {
            valueText = valueText[..^7].TrimEnd();
        }

        if (!FrameRateValue.TryParseInvariantDecimal(valueText, out var frame) || frame < 0)
        {
            return InvalidFrameText(info);
        }

        var converted = ChapterFrameTimeConverter.FramesToTicks(frame, rate);
        if (!converted.Success)
        {
            return new ChapterEditResult(info, [converted.Diagnostic!]);
        }

        var normalizedFrameText = frame.ToString("G29", CultureInfo.InvariantCulture);
        if (converted.Value == chapter.StartTime.Ticks
            && string.Equals(normalizedFrameText, chapter.FramesInfo, StringComparison.Ordinal))
        {
            return new ChapterEditResult(info, []);
        }

        var chapters = info.Chapters.ToList();
        chapters[index] = chapter with
        {
            StartTime = TimeSpan.FromTicks(converted.Value),
            FramesInfo = normalizedFrameText,
            FrameAccuracy = FrameAccuracy.Accurate
        };
        return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
    }

    private static ChapterEditResult InvalidFrameText(ChapterSet info) =>
        new(
            info,
            [new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.InvalidFrameText, "Frame text did not contain a frame number or fps was invalid.")]);

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
    /// <param name="options">The editing options. The default preserves chapter times.</param>
    /// <returns>The operation result.</returns>
    public ChapterEditResult Delete(ChapterSet info, IReadOnlySet<int> indexes, ChapterEditingOptions? options = null)
    {
        var chapters = info.Chapters.Where((_, index) => !indexes.Contains(index)).ToList();
        if ((options ?? ChapterEditingOptions.Default).DeleteRowsTiming == DeleteRowsTimingMode.Normalize
            && chapters.Count > 0 && indexes.Contains(0))
        {
            var shift = chapters[0].StartTime;
            chapters = [.. chapters.Select(chapter => chapter.IsSeparator ? chapter : chapter with { StartTime = chapter.StartTime - shift })];
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
                    ChapterDiagnosticCode.OrderShiftNormalized,
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

    /// <summary>Shifts starts on a canonical frame grid using the retained scalar negative-row policy.</summary>
    public ChapterEditResult ShiftFramesForward(ChapterSet info, int frames, FrameRateValue framesPerSecond)
    {
        var rate = framesPerSecond;
        var converted = ChapterFrameTimeConverter.FramesToTicks(frames, rate);
        if (!converted.Success)
        {
            return new ChapterEditResult(info, [converted.Diagnostic!]);
        }

        try
        {
            var chapters = info.Chapters.Select(chapter => chapter.IsSeparator ? chapter : chapter with
            {
                StartTime = TimeSpan.FromTicks(checked(chapter.StartTime.Ticks - converted.Value)),
                EndTime = chapter.EndTime is { } end ? TimeSpan.FromTicks(checked(end.Ticks - converted.Value)) : null
            }).Where(static chapter => chapter.IsSeparator || chapter.StartTime >= TimeSpan.Zero).ToArray();
            return new ChapterEditResult(info with { Chapters = Renumber(chapters) }, []);
        }
        catch (OverflowException)
        {
            return new ChapterEditResult(info, [new ChapterDiagnostic(DiagnosticSeverity.Error,
                ChapterDiagnosticCode.FrameConversionOverflow, "The frame shift exceeds the supported time range.")]);
        }
    }

    /// <summary>Creates zones from ticks on an explicitly resolved canonical frame grid.</summary>
    public ChapterZonesResult CreateZones(ChapterSet info, IReadOnlySet<int> indexes, FrameRateValue framesPerSecond)
    {
        try
        {
            return CreateZonesCore(info, indexes, framesPerSecond);
        }
        catch (OverflowException)
        {
            return new ChapterZonesResult(string.Empty, [new ChapterDiagnostic(DiagnosticSeverity.Error,
                ChapterDiagnosticCode.FrameConversionOverflow, "Zone frames exceed the supported Int64 range.")]);
        }
    }

    private static ChapterZonesResult CreateZonesCore(ChapterSet info, IReadOnlySet<int> indexes, FrameRateValue framesPerSecond)
    {
        if (!framesPerSecond.IsKnown)
        {
            return new ChapterZonesResult(
                string.Empty,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidFrameRate, "Frame rate must be greater than zero.")]);
        }

        if (indexes.Count == 0)
        {
            return new ChapterZonesResult(
                string.Empty,
                [new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NoRowsSelected, "Select one or more chapter rows first.")]);
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
                [new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.NoRowsSelected, "No valid chapter rows were selected.")]);
        }

        var zones = "--zones " + string.Join("/", ranges.OrderBy(static range => range.Begin).Select(static range => $"{range.Begin},{range.End},"));
        return new ChapterZonesResult(zones, []);
    }

    private static List<Chapter> Renumber(IEnumerable<Chapter> chapters)
    {
        var number = 0;
        return
        [
            .. chapters
                .Select(chapter => chapter.IsSeparator ? chapter : chapter with { DisplayNumber = ++number })
        ];
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
            [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidChapterIndex, $"Chapter index {index} is out of range.",
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["index"] = index })]);

    private static long ChapterFrame(Chapter chapter, FrameRateValue framesPerSecond)
    {
        return checked((long)ExactFrameMath.FromTicks(chapter.StartTime.Ticks, framesPerSecond).RoundAwayFromZero());
    }
}
