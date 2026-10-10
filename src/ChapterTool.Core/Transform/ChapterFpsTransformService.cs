using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Changes chapter timing between frame rates.
/// </summary>
public sealed class ChapterFpsTransformService
{
    /// <summary>
    /// Executes the ChangeFps operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="sourceFps">The source frame rate.</param>
    /// <param name="targetFps">The target frame rate.</param>
    /// <returns>The operation result.</returns>
    public static ChangeFpsResult ChangeFps(ChapterSet info, decimal sourceFps, decimal targetFps)
    {
        if (sourceFps <= 0 || targetFps <= 0)
        {
            return new ChangeFpsResult(
                false,
                info,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InvalidFrameRate, "Source and target frame rates must be greater than zero.")]);
        }

        var chapters = info.Chapters.Select(chapter => TransformChapter(chapter, sourceFps, targetFps)).ToList();
        var updated = info with
        {
            FramesPerSecond = (double)targetFps,
            Duration = TimeSpan.FromTicks(ConvertTicks(info.Duration.Ticks, sourceFps, targetFps)),
            Chapters = chapters
        };

        return new ChangeFpsResult(true, updated, []);
    }

    /// <summary>Changes chapter timing using authoritative rational source and target rates.</summary>
    public static ChangeFpsResult ChangeFps(ChapterSet info, ChapterFrameRate sourceFps, ChapterFrameRate targetFps)
    {
        ArgumentNullException.ThrowIfNull(info);
        var chapters = info.Chapters.Select(chapter => TransformChapter(chapter, sourceFps, targetFps)).ToList();
        var updated = info with
        {
            FramesPerSecond = (double)targetFps.Numerator / targetFps.Denominator,
            Duration = TimeSpan.FromTicks(ConvertTicks(info.Duration.Ticks, sourceFps, targetFps)),
            Chapters = chapters
        };
        return new ChangeFpsResult(true, updated, []);
    }

    /// <summary>Converts an absolute boundary through the same frame quantization as chapter times.</summary>
    internal static long ConvertTicks(long ticks, decimal sourceFps, decimal targetFps)
    {
        var source = ExactFrameMath.FromTicks(ticks, sourceFps).RoundAwayFromZero();
        return ExactFrameMath.TicksFromFrames(checked((long)source), targetFps);
    }

    internal static long ConvertTicks(long ticks, ChapterFrameRate sourceFps, ChapterFrameRate targetFps)
    {
        var source = ExactFrameMath.FromTicks(ticks, sourceFps).RoundAwayFromZero();
        return ExactFrameMath.TicksFromFrames(checked((long)source), targetFps);
    }

    private static Chapter TransformChapter(Chapter chapter, decimal sourceFps, decimal targetFps)
    {
        if (chapter.IsSeparator)
        {
            return chapter;
        }

        var frame = checked((long)ExactFrameMath.FromTicks(chapter.StartTime.Ticks, sourceFps).RoundAwayFromZero());
        var time = TimeSpan.FromTicks(ExactFrameMath.TicksFromFrames(frame, targetFps));
        TimeSpan? end = chapter.EndTime is null
            ? null
            : TransformEnd(chapter, chapter.EndTime.Value, sourceFps, targetFps, frame);
        return chapter with { StartTime = time, EndTime = end };
    }

    private static Chapter TransformChapter(Chapter chapter, ChapterFrameRate sourceFps, ChapterFrameRate targetFps)
    {
        if (chapter.IsSeparator)
        {
            return chapter;
        }

        var frame = checked((long)ExactFrameMath.FromTicks(chapter.StartTime.Ticks, sourceFps).RoundAwayFromZero());
        var time = TimeSpan.FromTicks(ExactFrameMath.TicksFromFrames(frame, targetFps));
        TimeSpan? end = chapter.EndTime is null
            ? null
            : TransformEnd(chapter, chapter.EndTime.Value, sourceFps, targetFps, frame);
        return chapter with { StartTime = time, EndTime = end };
    }

    private static TimeSpan TransformEnd(Chapter chapter, TimeSpan end, decimal sourceFps, decimal targetFps, long startFrame)
    {
        var endFrame = checked((long)ExactFrameMath.FromTicks(end.Ticks, sourceFps).RoundAwayFromZero());
        var frameSpan = Math.Max(0, endFrame - startFrame);
        return TimeSpan.FromTicks(ExactFrameMath.TicksFromFrames(startFrame + frameSpan, targetFps));
    }

    private static TimeSpan TransformEnd(Chapter chapter, TimeSpan end, ChapterFrameRate sourceFps, ChapterFrameRate targetFps, long startFrame)
    {
        var endFrame = checked((long)ExactFrameMath.FromTicks(end.Ticks, sourceFps).RoundAwayFromZero());
        var frameSpan = Math.Max(0, endFrame - startFrame);
        return TimeSpan.FromTicks(ExactFrameMath.TicksFromFrames(startFrame + frameSpan, targetFps));
    }
}
