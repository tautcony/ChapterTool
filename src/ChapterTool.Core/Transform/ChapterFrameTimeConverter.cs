using System.Numerics;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>The result of a frame and tick conversion.</summary>
public sealed record ChapterFrameConversionResult(bool Success, long Value, ChapterDiagnostic? Diagnostic = null);

/// <summary>Converts between frame numbers and 100-nanosecond ticks using deterministic integer arithmetic.</summary>
public static class ChapterFrameTimeConverter
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;

    /// <summary>Converts a signed frame count to ticks with midpoint-away-from-zero rounding.</summary>
    public static ChapterFrameConversionResult FramesToTicks(long frames, ChapterFrameRate frameRate) =>
        Convert(frames, (BigInteger)TicksPerSecond * frameRate.Denominator, frameRate.Numerator);

    /// <summary>Converts signed ticks to a frame count with midpoint-away-from-zero rounding.</summary>
    public static ChapterFrameConversionResult TicksToFrames(long ticks, ChapterFrameRate frameRate) =>
        Convert(ticks, frameRate.Numerator, (BigInteger)TicksPerSecond * frameRate.Denominator);

    private static ChapterFrameConversionResult Convert(long value, BigInteger numerator, BigInteger denominator)
    {
        var scaled = (BigInteger)value * numerator;
        var quotient = BigInteger.DivRem(scaled, denominator, out var remainder);
        if (BigInteger.Abs(remainder) * 2 >= denominator)
        {
            quotient += scaled.Sign >= 0 ? BigInteger.One : BigInteger.MinusOne;
        }

        if (quotient < long.MinValue || quotient > long.MaxValue)
        {
            var diagnostic = new ChapterDiagnostic(
                DiagnosticSeverity.Error,
                ChapterDiagnosticCode.FrameConversionOverflow,
                "The frame and time conversion exceeds the supported 64-bit range.");
            return new ChapterFrameConversionResult(false, 0, diagnostic);
        }

        return new ChapterFrameConversionResult(true, (long)quotient);
    }
}
