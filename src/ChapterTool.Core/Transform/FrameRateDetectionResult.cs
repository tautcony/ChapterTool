namespace ChapterTool.Core.Transform;

/// <summary>
/// Represents detailed frame rate detection output.
/// </summary>
/// <param name="Option">The detected frame rate option.</param>
/// <param name="AccurateChapterCount">The number of chapters that landed within the detection tolerance.</param>
/// <param name="EvaluatedChapterCount">The number of non-separator chapters evaluated.</param>
/// <param name="CumulativeDeviation">The accumulated frame deviation across evaluated chapters.</param>
/// <param name="Confidence">The confidence level assigned to the detected option.</param>
public sealed record FrameRateDetectionResult(
    FrameRateOption Option,
    int AccurateChapterCount,
    int EvaluatedChapterCount,
    decimal CumulativeDeviation,
    FrameRateConfidence Confidence);
