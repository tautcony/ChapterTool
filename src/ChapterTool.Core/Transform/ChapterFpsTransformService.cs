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
        ArgumentNullException.ThrowIfNull(info);
        if (!FrameRateValue.TryFromDecimal(sourceFps, metadata: false, out var source, out var diagnostic)
            || !FrameRateValue.TryFromDecimal(targetFps, metadata: false, out var target, out diagnostic))
        {
            return new ChangeFpsResult(false, info, [diagnostic!]);
        }

        return ChangeFps(info, source, target);
    }

    /// <summary>Changes chapter timing using authoritative rational source and target rates.</summary>
    public static ChangeFpsResult ChangeFps(ChapterSet info, ChapterFrameRate sourceFps, ChapterFrameRate targetFps)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!FrameRateValue.TryRational(sourceFps, out var source, out var diagnostic)
            || !FrameRateValue.TryRational(targetFps, out var target, out diagnostic))
        {
            return new ChangeFpsResult(false, info, [diagnostic!]);
        }

        return ChangeFps(info, source, target);
    }

    /// <summary>Changes timing with canonical rates and rejects the whole result on invalid bounds.</summary>
    public static ChangeFpsResult ChangeFps(ChapterSet info, FrameRateValue sourceFps, FrameRateValue targetFps)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!sourceFps.IsKnown || !targetFps.IsKnown)
        {
            return Failure(info, ChapterDiagnosticCode.InvalidFrameRate, "Known source and target computation rates are required.");
        }

        if (info.Chapters.Any(static chapter => !chapter.IsSeparator
            && (chapter.StartTime.Ticks < 0 || (chapter.EndTime is { } end && end < chapter.StartTime))))
        {
            return Failure(info, ChapterDiagnosticCode.InvalidStructure, "Chapter bounds are invalid.");
        }

        try
        {
            var chapters = info.Chapters.Select(chapter => chapter.IsSeparator ? chapter : chapter with
            {
                StartTime = TimeSpan.FromTicks(ConvertTicks(chapter.StartTime.Ticks, sourceFps, targetFps)),
                EndTime = chapter.EndTime is { } end ? TimeSpan.FromTicks(ConvertTicks(end.Ticks, sourceFps, targetFps)) : null
            }).ToList();
            var updated = info with
            {
                FrameRate = targetFps,
                Duration = TimeSpan.FromTicks(ConvertTicks(info.Duration.Ticks, sourceFps, targetFps)),
                Chapters = chapters
            };
            return new ChangeFpsResult(true, updated, []);
        }
        catch (OverflowException)
        {
            return Failure(info, ChapterDiagnosticCode.InvalidStructure, "The converted time exceeds the supported Int64 range.");
        }
    }

    internal static long ConvertTicks(long ticks, FrameRateValue sourceFps, FrameRateValue targetFps) =>
        ExactFrameMath.TicksFromFrames(ExactFrameMath.FromTicks(ticks, sourceFps).RoundAwayFromZero(), targetFps);

    private static ChangeFpsResult Failure(ChapterSet info, ChapterDiagnosticCode code, string message) =>
        new(false, info, [new ChapterDiagnostic(DiagnosticSeverity.Error, code, message)]);

}
