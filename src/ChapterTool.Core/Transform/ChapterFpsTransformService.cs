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
        var durationFrames = ChapterRounding.RoundToInt64((decimal)info.Duration.TotalSeconds * sourceFps);
        var updated = info with
        {
            FramesPerSecond = (double)targetFps,
            Duration = ChapterRounding.SecondsToTimeSpan(durationFrames / targetFps),
            Chapters = chapters
        };

        return new ChangeFpsResult(true, updated, []);
    }

    private static Chapter TransformChapter(Chapter chapter, decimal sourceFps, decimal targetFps)
    {
        if (chapter.IsSeparator)
        {
            return chapter;
        }

        var frame = ChapterRounding.RoundToInt64((decimal)chapter.StartTime.TotalSeconds * sourceFps);
        var time = ChapterRounding.SecondsToTimeSpan(frame / targetFps);
        TimeSpan? end = chapter.EndTime is null
            ? null
            : TransformEnd(chapter, chapter.EndTime.Value, sourceFps, targetFps, frame);
        return chapter with { StartTime = time, EndTime = end };
    }

    private static TimeSpan TransformEnd(Chapter chapter, TimeSpan end, decimal sourceFps, decimal targetFps, long startFrame)
    {
        var endFrame = ChapterRounding.RoundToInt64((decimal)end.TotalSeconds * sourceFps);
        var frameSpan = Math.Max(0, endFrame - startFrame);
        return ChapterRounding.SecondsToTimeSpan((startFrame + frameSpan) / targetFps);
    }
}
