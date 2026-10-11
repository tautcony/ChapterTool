using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Typed source data for formatting one frame value without changing its domain representation.</summary>
public sealed record FramePresentationInput(
    long StartTicks,
    FrameRateValue Rate,
    string NumericFallback)
{
    /// <summary>Creates an input when only the existing numeric presentation is available.</summary>
    public static FramePresentationInput FromNumericText(string numericFallback) =>
        new(0, FrameRateValue.Unknown, numericFallback);
}
