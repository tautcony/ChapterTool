using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Typed source data for formatting one frame value without changing its domain representation.</summary>
public sealed record FramePresentationInput(
    long StartTicks,
    ChapterFrameRate? ExactRate,
    decimal? ApproximateFramesPerSecond,
    string NumericFallback)
{
    /// <summary>Creates an input from a timestamp and an authoritative rational frame rate.</summary>
    public static FramePresentationInput FromExactRate(long startTicks, ChapterFrameRate rate, string numericFallback) =>
        new(startTicks, rate, null, numericFallback);

    /// <summary>Creates an input whose numeric rate does not prove an exact rational frame rate.</summary>
    public static FramePresentationInput FromApproximateRate(
        long startTicks,
        decimal framesPerSecond,
        string numericFallback) =>
        new(startTicks, null, framesPerSecond > 0m ? framesPerSecond : null, numericFallback);

    /// <summary>Creates an input when only the existing numeric presentation is available.</summary>
    public static FramePresentationInput FromNumericText(string numericFallback) =>
        new(0, null, null, numericFallback);
}
