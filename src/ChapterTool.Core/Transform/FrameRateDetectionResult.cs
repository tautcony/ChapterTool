namespace ChapterTool.Core.Transform;

public sealed record FrameRateDetectionResult(
    FrameRateOption Option,
    int AccurateChapterCount,
    int EvaluatedChapterCount,
    decimal CumulativeDeviation,
    FrameRateConfidence Confidence);
