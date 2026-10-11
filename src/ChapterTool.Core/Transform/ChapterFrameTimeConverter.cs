using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>The result of a frame and tick conversion.</summary>
public sealed record ChapterFrameConversionResult(bool Success, long Value, ChapterDiagnostic? Diagnostic = null);

/// <summary>Converts between frame numbers and 100-nanosecond ticks using deterministic integer arithmetic.</summary>
public static class ChapterFrameTimeConverter
{
    /// <summary>Converts a signed frame count to ticks with midpoint-away-from-zero rounding.</summary>
    public static ChapterFrameConversionResult FramesToTicks(long frames, ChapterFrameRate frameRate) =>
        FrameRateValue.TryRational(frameRate, out var rate, out var diagnostic)
            ? FramesToTicks(frames, rate) : new(false, 0, diagnostic);

    /// <summary>Converts a signed integer frame count with any canonical rate kind.</summary>
    public static ChapterFrameConversionResult FramesToTicks(long frames, FrameRateValue frameRate) =>
        Convert(frameRate, () => ExactFrameMath.TicksFromFrames((decimal)frames, frameRate));

    /// <summary>Converts signed ticks to a frame count with midpoint-away-from-zero rounding.</summary>
    public static ChapterFrameConversionResult TicksToFrames(long ticks, ChapterFrameRate frameRate) =>
        FrameRateValue.TryRational(frameRate, out var rate, out var diagnostic)
            ? TicksToFrames(ticks, rate) : new(false, 0, diagnostic);

    /// <summary>Converts a finite decimal frame count without changing rate authority.</summary>
    public static ChapterFrameConversionResult FramesToTicks(decimal frames, FrameRateValue frameRate) =>
        Convert(frameRate, () => ExactFrameMath.TicksFromFrames(frames, frameRate));

    /// <summary>Converts signed ticks to an integer frame count with one rounding boundary.</summary>
    public static ChapterFrameConversionResult TicksToFrames(long ticks, FrameRateValue frameRate) =>
        Convert(frameRate, () => checked((long)ExactFrameMath.FromTicks(ticks, frameRate).RoundAwayFromZero()));

    private static ChapterFrameConversionResult Convert(FrameRateValue rate, Func<long> calculate)
    {
        if (!rate.IsKnown)
        {
            return new(false, 0, FrameRateValidation.InvalidFrameRate());
        }

        try
        {
            return new ChapterFrameConversionResult(true, calculate());
        }
        catch (OverflowException)
        {
            var diagnostic = new ChapterDiagnostic(
                DiagnosticSeverity.Error,
                ChapterDiagnosticCode.FrameConversionOverflow,
                "The frame and time conversion exceeds the supported 64-bit range.");
            return new ChapterFrameConversionResult(false, 0, diagnostic);
        }

    }
}
