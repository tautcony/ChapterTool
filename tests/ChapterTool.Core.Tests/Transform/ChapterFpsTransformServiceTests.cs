using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ChapterFpsTransformServiceTests
{
    [Fact]
    public void ChangeFps_preserves_chapter_frame_numbers()
    {
        var info = Sample();

        var result = ChapterFpsTransformService.ChangeFps(info, FrameRateValue.Approximate(24m), FrameRateValue.Approximate(48m));

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Info.Chapters[1].StartTime);
        Assert.Equal(FrameRateValue.Approximate(48m), result.Info.FrameRate);
    }

    [Fact]
    public void ChangeFps_preserves_frame_span_when_end_exists()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.FromSeconds(10), "A", EndTime: TimeSpan.FromSeconds(12))
            ]
        };

        var result = ChapterFpsTransformService.ChangeFps(info, FrameRateValue.Approximate(24m), FrameRateValue.Approximate(48m));

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Info.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(6), result.Info.Chapters[0].EndTime);
    }

    [Fact]
    public void ChangeFps_uses_exact_rational_rates_and_timestamp_ticks()
    {
        var source = new ChapterFrameRate(24_000, 1_001);
        var target = new ChapterFrameRate(30_000, 1_001);
        var timestamp = TimeSpan.FromTicks(1_001_000_000);
        var info = Sample() with
        {
            Duration = timestamp,
            Chapters = [new Chapter(1, timestamp, "Late")]
        };

        var result = ChapterFpsTransformService.ChangeFps(info, FrameRateValue.Rational(source), FrameRateValue.Rational(target));

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromTicks(800_800_000), result.Info.Chapters[0].StartTime);
        Assert.Equal(TimeSpan.FromTicks(800_800_000), result.Info.Duration);
    }

    [Fact]
    public void ChangeFps_invalid_fps_returns_diagnostic_and_preserves_input()
    {
        var info = Sample();

        var result = ChapterFpsTransformService.ChangeFps(info, FrameRateValue.Unknown, FrameRateValue.Approximate(24m));

        Assert.False(result.Success);
        Assert.Same(info, result.Info);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidFrameRate);
    }

    private static ChapterSet Sample() =>
        new(
            "Title",
            "source",
            ChapterImportFormat.Ogm,
            FrameRateValue.Approximate(24m),
            TimeSpan.FromSeconds(20),
            [
                new Chapter(1, TimeSpan.Zero, "Intro"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle")
            ]);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Canonical_conversion_preserves_both_rate_kinds(bool exactSource, bool exactTarget)
    {
        var source = exactSource ? FrameRateValue.Rational(24000, 1001) : FrameRateValue.Approximate(23.976m);
        var target = exactTarget ? FrameRateValue.Rational(30000, 1001) : FrameRateValue.Approximate(29.97m);
        var timestamp = TimeSpan.FromTicks(1_001_000_000);
        var input = Sample() with { FrameRate = source, Duration = timestamp, Chapters = [new Chapter(1, timestamp, "A")] };
        var result = ChapterFpsTransformService.ChangeFps(input, source, target);
        Assert.True(result.Success);
        Assert.Equal(target, result.Info.FrameRate);
        Assert.Equal(exactTarget ? 800_800_000 : 800_800_801, result.Info.Chapters[0].StartTime.Ticks);
        Assert.Equal(result.Info.Duration, result.Info.Chapters[0].StartTime);
        Assert.Equal(source, input.FrameRate);
    }

    [Fact]
    public void Same_rate_conversion_still_quantizes_off_grid_time()
    {
        var rate = FrameRateValue.Rational(24, 1);
        var input = Sample() with { FrameRate = rate, Chapters = [new Chapter(1, TimeSpan.FromTicks(1), "A")] };
        var result = ChapterFpsTransformService.ChangeFps(input, rate, rate);
        Assert.True(result.Success);
        Assert.Equal(0, result.Info.Chapters[0].StartTime.Ticks);
        Assert.Equal(1, input.Chapters[0].StartTime.Ticks);
    }

    [Fact]
    public void Large_intermediate_frame_count_does_not_overflow_when_final_ticks_fit()
    {
        var rate = FrameRateValue.Rational(long.MaxValue, 1);
        var input = Sample() with { Duration = TimeSpan.FromTicks(long.MaxValue), Chapters = [new Chapter(1, TimeSpan.FromTicks(long.MaxValue), "A")] };
        var result = ChapterFpsTransformService.ChangeFps(input, rate, rate);
        Assert.True(result.Success);
        Assert.Equal(long.MaxValue, result.Info.Chapters[0].StartTime.Ticks);
    }

    [Fact]
    public void Overflow_and_invalid_bounds_reject_the_whole_result()
    {
        var input = Sample() with { Duration = TimeSpan.FromTicks(long.MaxValue) };
        var result = ChapterFpsTransformService.ChangeFps(input, FrameRateValue.Rational(48, 1), FrameRateValue.Rational(24, 1));
        Assert.False(result.Success);
        Assert.Same(input, result.Info);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidStructure);

        input = Sample() with { Chapters = [new Chapter(1, TimeSpan.FromTicks(2), "A", EndTime: TimeSpan.FromTicks(1))] };
        result = ChapterFpsTransformService.ChangeFps(input, FrameRateValue.Rational(24, 1), FrameRateValue.Rational(24, 1));
        Assert.False(result.Success);
        Assert.Same(input, result.Info);
    }

    [Fact]
    public void Default_rational_and_unknown_computation_rates_return_diagnostics()
    {
        var input = Sample();
        var result = ChapterFpsTransformService.ChangeFps(input, FrameRateValue.Unknown, FrameRateValue.Rational(24, 1));
        Assert.False(result.Success);
        Assert.Same(input, result.Info);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidFrameRate);
        result = ChapterFpsTransformService.ChangeFps(input, FrameRateValue.Unknown, FrameRateValue.Rational(24, 1));
        Assert.False(result.Success);
        Assert.Same(input, result.Info);
    }
}
