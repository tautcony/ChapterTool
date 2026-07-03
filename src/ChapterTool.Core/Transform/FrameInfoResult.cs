using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

public sealed record FrameInfoResult(
    ChapterInfo Info,
    IReadOnlyList<Chapter> Chapters,
    FrameRateOption SelectedOption,
    decimal FramesPerSecond,
    IReadOnlyList<FrameAccuracy> Accuracy);
