using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

public sealed class ChapterFpsTransformService
{
    public ChangeFpsResult ChangeFps(ChapterInfo info, decimal sourceFps, decimal targetFps)
    {
        if (sourceFps <= 0 || targetFps <= 0)
        {
            return new ChangeFpsResult(
                false,
                info,
                [new ChapterDiagnostic(DiagnosticSeverity.Error, "InvalidFrameRate", "Source and target frame rates must be greater than zero.")]);
        }

        var chapters = info.Chapters.Select(chapter => TransformChapter(chapter, sourceFps, targetFps)).ToArray();
        var durationFrames = ChapterRounding.RoundToInt64((decimal)info.Duration.TotalSeconds * sourceFps);
        var updated = info with
        {
            FramesPerSecond = (double)targetFps,
            Duration = ChapterRounding.SecondsToTimeSpan(durationFrames / targetFps),
            Chapters = chapters
        };

        return new ChangeFpsResult(true, updated, Array.Empty<ChapterDiagnostic>());
    }

    private static Chapter TransformChapter(Chapter chapter, decimal sourceFps, decimal targetFps)
    {
        if (chapter.IsSeparator)
        {
            return chapter;
        }

        var frame = ChapterRounding.RoundToInt64((decimal)chapter.Time.TotalSeconds * sourceFps);
        var time = ChapterRounding.SecondsToTimeSpan(frame / targetFps);
        TimeSpan? end = chapter.End is null
            ? null
            : TransformEnd(chapter, chapter.End.Value, sourceFps, targetFps, frame);
        return chapter with { Time = time, End = end };
    }

    private static TimeSpan TransformEnd(Chapter chapter, TimeSpan end, decimal sourceFps, decimal targetFps, long startFrame)
    {
        var endFrame = ChapterRounding.RoundToInt64((decimal)end.TotalSeconds * sourceFps);
        var frameSpan = Math.Max(0, endFrame - startFrame);
        return ChapterRounding.SecondsToTimeSpan((startFrame + frameSpan) / targetFps);
    }
}
