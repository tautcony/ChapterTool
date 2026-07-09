using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Represents calculated frame numbers and accuracy for chapters.
/// </summary>
/// <param name="Info">The Info value.</param>
/// <param name="Chapters">The Chapters value.</param>
/// <param name="SelectedOption">The SelectedOption value.</param>
/// <param name="FramesPerSecond">The FramesPerSecond value.</param>
/// <param name="Accuracy">The Accuracy value.</param>
public sealed record FrameInfoResult(
    ChapterSet Info,
    IReadOnlyList<Chapter> Chapters,
    FrameRateOption SelectedOption,
    decimal FramesPerSecond,
    IReadOnlyList<FrameAccuracy> Accuracy);
