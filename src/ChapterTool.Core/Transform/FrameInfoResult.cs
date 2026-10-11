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

    /// <summary>Gets or initializes the retained numeric display projection.</summary>
    public decimal FramesPerSecond
    {
        get => Rate.ToDecimal();
        init => SelectedOption = SelectedOption with { Value = value };
    }

#pragma warning disable SA1313 // Preserve established named arguments.
    /// <summary>Adapts the retained numeric result constructor.</summary>
    public FrameInfoResult(ChapterSet Info, IReadOnlyList<Chapter> Chapters, FrameRateOption SelectedOption,
        decimal FramesPerSecond, IReadOnlyList<FrameAccuracy> Accuracy)
        : this(Info, Chapters, SelectedOption with { Value = FramesPerSecond }, Accuracy)
    {
    }

    /// <summary>Deconstructs the retained numeric result fields.</summary>
    public void Deconstruct(out ChapterSet info, out IReadOnlyList<Chapter> chapters, out FrameRateOption selectedOption,
        out decimal framesPerSecond, out IReadOnlyList<FrameAccuracy> accuracy)
    {
        info = this.Info;
        chapters = this.Chapters;
        selectedOption = this.SelectedOption;
        framesPerSecond = this.FramesPerSecond;
        accuracy = this.Accuracy;
    }
#pragma warning restore SA1313
}
