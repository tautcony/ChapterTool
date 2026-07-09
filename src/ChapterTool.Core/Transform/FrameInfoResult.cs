using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Represents calculated frame numbers and accuracy for chapters.
/// </summary>
/// <param name="Info">The chapter set updated with frame metadata.</param>
/// <param name="Chapters">The chapters updated with frame display values.</param>
/// <param name="SelectedOption">The frame rate option used for the calculation.</param>
/// <param name="FramesPerSecond">The effective frame rate in frames per second.</param>
/// <param name="Accuracy">Per-chapter frame accuracy classifications.</param>
public sealed record FrameInfoResult(
    ChapterSet Info,
    IReadOnlyList<Chapter> Chapters,
    FrameRateOption SelectedOption,
    decimal FramesPerSecond,
    IReadOnlyList<FrameAccuracy> Accuracy);
