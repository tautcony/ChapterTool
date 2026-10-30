using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Provides hover guidance for chapter frame-rate conversions.</summary>
public sealed partial class MainWindowViewModel
{
    private FrameRateValue? frameRateReviewSource;
    private FrameRateValue? frameRateReviewTarget;
    private bool frameRateReviewTimeChanges;

    public bool HasFrameRateReview =>
        pendingContentPreview is { Operation: "Change chapter frame rate" }
        && frameRateReviewSource.HasValue;

    public string FrameRateReviewTooltip => HasFrameRateReview && frameRateReviewSource is { } source
        && frameRateReviewTarget is { } target
        ? Localizer.Format(
            "Main.FrameRateReview",
            new Dictionary<string, object?>
            {
                ["0"] = FormatReviewRate(source),
                ["1"] = FormatReviewRate(target),
                ["2"] = Localizer.GetString(!source.IsKnown
                    ? "Main.FrameRateReviewUnknownSource"
                    : frameRateReviewTimeChanges
                        ? "Main.FrameRateReviewTimesChanged"
                        : "Main.FrameRateReviewTimesUnchanged")
            })
        : Localizer.GetString("Main.ChangeFps");

    private void SetFrameRateReview(ChapterContentPreview preview, FrameRateValue sourceRate, FrameRateValue targetRate)
    {
        var chapterTimesChange = preview.Before.Tracks
            .Zip(preview.Candidate.Tracks)
            .Any(pair => pair.First.Chapters
                .Zip(pair.Second.Chapters)
                .Any(chapters => chapters.First.StartTicks != chapters.Second.StartTicks));
        frameRateReviewSource = sourceRate;
        frameRateReviewTarget = targetRate;
        frameRateReviewTimeChanges = chapterTimesChange;
        OnPropertyChanged(nameof(HasFrameRateReview));
        OnPropertyChanged(nameof(FrameRateReviewTooltip));
    }

    private void ClearFrameRateReview()
    {
        frameRateReviewSource = null;
        frameRateReviewTarget = null;
        OnPropertyChanged(nameof(HasFrameRateReview));
        OnPropertyChanged(nameof(FrameRateReviewTooltip));
    }

    private void RefreshFrameRateReview() => OnPropertyChanged(nameof(FrameRateReviewTooltip));

    private string FormatReviewRate(FrameRateValue rate) => rate.Kind switch
    {
        FrameRateKind.Rational => $"{rate} {Localizer.GetString("Expression.Unit.FramesPerSecond")}",
        FrameRateKind.Approximate => $"≈{rate} {Localizer.GetString("Expression.Unit.FramesPerSecond")}",
        _ => Localizer.GetString("Expression.Value.NotSet")
    };
}
