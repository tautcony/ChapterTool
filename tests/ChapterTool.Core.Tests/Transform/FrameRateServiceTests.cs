using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class FrameRateServiceTests
{
    private readonly FrameRateService service = new();

    [Fact]
    public void Options_expose_legacy_mpls_frame_rate_codes_without_combo_box_indexes()
    {
        var options = service.Options;

        Assert.Collection(
            options,
            option => AssertFrameRate(option, "Auto", "Auto", 0m, false, 0),
            option => AssertFrameRate(option, "Fps23976", "24000 / 1001", 24000m / 1001m, true, 1),
            option => AssertFrameRate(option, "Fps24", "24000 / 1000", 24m, true, 2),
            option => AssertFrameRate(option, "Fps25", "25000 / 1000", 25m, true, 3),
            option => AssertFrameRate(option, "Fps2997", "30000 / 1001", 30000m / 1001m, true, 4),
            option => AssertFrameRate(option, "Reserved", "RESER / VED", 0m, false, 5),
            option => AssertFrameRate(option, "Fps50", "50000 / 1000", 50m, true, 6),
            option => AssertFrameRate(option, "Fps5994", "60000 / 1001", 60000m / 1001m, true, 7));
    }

    [Fact]
    public void FindByValue_matches_valid_frame_rate_with_legacy_tolerance()
    {
        var actual = service.FindByValue(23.976023976m);

        Assert.Equal("Fps23976", actual.Code);
    }

    [Fact]
    public void Detect_skips_auto_and_reserved_options()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromSeconds(7d / 25d), "Chapter 2"),
            new Chapter(3, TimeSpan.FromSeconds(8d / 25d), "Chapter 3")
        };
        var info = NewInfo(0m, chapters);

        var actual = service.Detect(info, tolerance: 0.01m);

        Assert.Equal("Fps25", actual.Code);
    }

    [Fact]
    public void Detect_returns_first_highest_scoring_valid_option_on_tie()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromSeconds(1), "Chapter 2")
        };
        var info = NewInfo(0m, chapters);

        var actual = service.Detect(info, tolerance: 0.15m);

        Assert.Equal("Fps24", actual.Code);
    }

    [Fact]
    public void DetectDetailed_returns_lowest_deviation_option_when_count_ties()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromSeconds(1), "Chapter 2"),
            new Chapter(3, TimeSpan.FromSeconds(2), "Chapter 3"),
            new Chapter(4, TimeSpan.FromSeconds(3), "Chapter 4")
        };
        var info = NewInfo(0m, chapters);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal("Fps24", result.Option.Code);
        Assert.Equal(0m, result.CumulativeDeviation);
    }

    [Fact]
    public void DetectDetailed_assigns_high_confidence_for_exact_matches()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromMilliseconds(40), "Chapter 2"),
            new Chapter(3, TimeSpan.FromMilliseconds(80), "Chapter 3")
        };
        var info = NewInfo(0m, chapters);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal("Fps25", result.Option.Code);
        Assert.Equal(FrameRateConfidence.High, result.Confidence);
        Assert.Equal(3, result.AccurateChapterCount);
        Assert.Equal(3, result.EvaluatedChapterCount);
    }

    [Fact]
    public void DetectDetailed_assigns_medium_confidence_when_some_deviation()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromMilliseconds(40), "Chapter 2"),
            new Chapter(3, TimeSpan.FromMilliseconds(80) + TimeSpan.FromTicks(15000), "Chapter 3")
        };
        var info = NewInfo(0m, chapters);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal("Fps25", result.Option.Code);
        Assert.Equal(FrameRateConfidence.Medium, result.Confidence);
        Assert.Equal(2, result.AccurateChapterCount);
    }

    [Fact]
    public void DetectDetailed_assigns_low_confidence_when_few_chapters_align()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromMilliseconds(123), "Chapter 2"),
            new Chapter(3, TimeSpan.FromMilliseconds(457), "Chapter 3"),
            new Chapter(4, TimeSpan.FromMilliseconds(891), "Chapter 4"),
            new Chapter(5, TimeSpan.FromMilliseconds(1357), "Chapter 5")
        };
        var info = NewInfo(0m, chapters);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal(FrameRateConfidence.Low, result.Confidence);
    }

    [Fact]
    public void DetectDetailed_returns_default_with_low_confidence_for_empty_chapters()
    {
        var info = NewInfo(0m, []);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal("Fps23976", result.Option.Code);
        Assert.Equal(FrameRateConfidence.Low, result.Confidence);
        Assert.Equal(0, result.EvaluatedChapterCount);
        Assert.Equal(0, result.AccurateChapterCount);
        Assert.Equal(0m, result.CumulativeDeviation);
    }

    [Fact]
    public void DetectDetailed_skips_separator_chapters_in_count()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(-1, Chapter.SeparatorTime, ""),
            new Chapter(2, TimeSpan.FromSeconds(7d / 25d), "Chapter 2")
        };
        var info = NewInfo(0m, chapters);

        var result = service.DetectDetailed(info, tolerance: 0.01m);

        Assert.Equal(2, result.EvaluatedChapterCount);
        Assert.Equal("Fps25", result.Option.Code);
    }

    [Fact]
    public void UpdateFrames_uses_detected_option_when_rounding_with_auto_option()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Chapter 1"),
            new Chapter(2, TimeSpan.FromSeconds(7d / 25d), "Chapter 2")
        };
        var info = NewInfo(0m, chapters);

        var actual = service.UpdateFrames(info, service.Options[0], round: true, tolerance: 0.01m);

        Assert.Equal("Fps25", actual.SelectedOption.Code);
        Assert.Equal(25m, actual.FramesPerSecond);
        Assert.Equal(["0", "7"], actual.Chapters.Select(chapter => chapter.FramesInfo));
        Assert.Equal([FrameAccuracy.Accurate, FrameAccuracy.Accurate], actual.Chapters.Select(chapter => chapter.FrameAccuracy));
    }

    [Fact]
    public void UpdateFrames_marks_rounded_frames_accurate_when_difference_is_less_than_tolerance()
    {
        var info = NewInfo(0m, [new Chapter(1, TimeSpan.FromSeconds(1.0004), "Chapter 1")]);

        var actual = service.UpdateFrames(info, service.Options[3], round: true, tolerance: 0.15m);

        Assert.Equal("25", actual.Chapters.Single().FramesInfo);
        Assert.Equal(FrameAccuracy.Accurate, actual.Chapters.Single().FrameAccuracy);
    }

    [Fact]
    public void UpdateFrames_marks_rounded_frames_inexact_when_difference_is_not_less_than_tolerance()
    {
        var info = NewInfo(0m, [new Chapter(1, TimeSpan.FromSeconds(1.004), "Chapter 1")]);

        var actual = service.UpdateFrames(info, service.Options[3], round: true, tolerance: 0.01m);

        Assert.Equal("25", actual.Chapters.Single().FramesInfo);
        Assert.Equal(FrameAccuracy.Inexact, actual.Chapters.Single().FrameAccuracy);
    }

    [Fact]
    public void UpdateFrames_uses_away_from_zero_midpoint_rounding()
    {
        var info = NewInfo(0m, [new Chapter(1, TimeSpan.FromSeconds(0.5), "Chapter 1")]);

        var actual = service.UpdateFrames(info, service.Options[3], round: true, tolerance: 0.01m);

        Assert.Equal("13", actual.Chapters.Single().FramesInfo);
        Assert.Equal(FrameAccuracy.Inexact, actual.Chapters.Single().FrameAccuracy);
    }

    [Fact]
    public void UpdateFrames_displays_raw_decimal_without_marker_when_rounding_is_disabled()
    {
        var info = NewInfo(0m, [new Chapter(1, TimeSpan.FromSeconds(0.5), "Chapter 1")]);

        var actual = service.UpdateFrames(info, service.Options[2], round: false, tolerance: 0.15m);

        Assert.Equal("12.0", actual.Chapters.Single().FramesInfo);
        Assert.Equal(FrameAccuracy.Neutral, actual.Chapters.Single().FrameAccuracy);
    }

    private static ChapterInfo NewInfo(decimal fps, IReadOnlyList<Chapter> chapters)
    {
        return new ChapterInfo(
            Title: "Title",
            SourceName: null,
            SourceIndex: 0,
            SourceType: "OGM",
            FramesPerSecond: (double)fps,
            Duration: TimeSpan.Zero,
            Chapters: chapters);
    }

    private static void AssertFrameRate(
        FrameRateOption option,
        string code,
        string displayName,
        decimal value,
        bool isValid,
        int legacyMplsCode)
    {
        Assert.Equal(code, option.Code);
        Assert.Equal(displayName, option.DisplayName);
        Assert.Equal(value, option.Value);
        Assert.Equal(isValid, option.IsValid);
        Assert.Equal(legacyMplsCode, option.LegacyMplsCode);
    }
}
