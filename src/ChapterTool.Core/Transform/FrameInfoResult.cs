using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Represents display frame values and their computation rate.</summary>
public sealed record FrameInfoResult(
    ChapterSet Info,
    IReadOnlyList<Chapter> Chapters,
    FrameRateOption SelectedOption,
    IReadOnlyList<FrameAccuracy> Accuracy)
{
    /// <summary>Gets the display computation rate without changing content authority.</summary>
    public FrameRateValue Rate => SelectedOption.Rate;
}
