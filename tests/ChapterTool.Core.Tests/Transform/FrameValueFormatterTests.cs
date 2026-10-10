using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class FrameValueFormatterTests
{
    private static readonly FramePresentationPolicy Compact = new(false, true, 3);

    [Theory]
    [InlineData(52.094, "1249", "", "006993")]
    [InlineData(52.0942, "1249", "0", "117882")]
    [InlineData(218.301, "5233", "", "990009")]
    [InlineData(218.3012, "5233", "9", "948051")]
    [InlineData(408.4082, "9792", "0", "047952")]
    public void Formats_selected_rational_frame_examples(double seconds, string integer, string prefix, string repeating)
    {
        var parts = FrameValueFormatter.Format(
            Exact(TimeSpan.FromSeconds(seconds).Ticks, new ChapterFrameRate(24000, 1001)),
            Compact);

        Assert.Equal(integer, parts.IntegerDigits);
        Assert.Equal(prefix, parts.NonRepeatingDigits);
        Assert.Equal(repeating, parts.RepeatingDigits);
        Assert.True(parts.HasRepeatingDigits);
    }

    [Fact]
    public void Exact_integer_omits_fractional_tail_and_keeps_fraction_details()
    {
        var parts = FrameValueFormatter.Format(
            Exact(TimeSpan.FromSeconds(408.408).Ticks, new ChapterFrameRate(24000, 1001)),
            Compact);

        Assert.Equal("9792", parts.IntegerDigits);
        Assert.Empty(parts.NonRepeatingDigits);
        Assert.Empty(parts.RepeatingDigits);
        Assert.Equal("9792", parts.PlainNumericText);
        Assert.Equal("9792/1", parts.ExactFractionText);
    }

    [Fact]
    public void Integer_rounding_takes_precedence_over_repeating_decimal_display()
    {
        var parts = FrameValueFormatter.Format(
            Exact(520_940_000, new ChapterFrameRate(24000, 1001)),
            new FramePresentationPolicy(true, true, -1));

        Assert.Equal("1249", parts.PlainNumericText);
        Assert.Empty(parts.NonRepeatingDigits);
        Assert.Empty(parts.RepeatingDigits);
    }

    [Fact]
    public void Finite_decimal_uses_ordinary_decimal_places()
    {
        var parts = FrameValueFormatter.Format(Exact(TimeSpan.FromSeconds(0.02).Ticks, new ChapterFrameRate(25, 1)), Compact);

        Assert.Equal("0.500", parts.PlainNumericText);
        Assert.Empty(parts.RepeatingDigits);
    }

    [Fact]
    public void Disabled_repeating_preference_keeps_ordinary_numeric_format()
    {
        var parts = FrameValueFormatter.Format(
            Exact(520_940_000, new ChapterFrameRate(24000, 1001)),
            new FramePresentationPolicy(false, false, 6));

        Assert.Equal("1249.006993", parts.PlainNumericText);
        Assert.Empty(parts.RepeatingDigits);
        Assert.Equal("1249006993", parts.IntegerDigits + parts.NonRepeatingDigits);
    }

    [Fact]
    public void No_truncation_mode_uses_full_available_decimal_precision_without_cycle_notation()
    {
        var parts = FrameValueFormatter.Format(
            Exact(520_940_000, new ChapterFrameRate(24000, 1001)),
            new FramePresentationPolicy(false, false, -1));

        Assert.StartsWith("1249.006993", parts.PlainNumericText, StringComparison.Ordinal);
        Assert.Empty(parts.RepeatingDigits);
    }

    [Fact]
    public void Negative_fraction_keeps_sign_before_zero_and_cycle()
    {
        var parts = FrameValueFormatter.Format(Exact(-TimeSpan.TicksPerSecond, new ChapterFrameRate(1, 3)), Compact);

        Assert.Equal("-", parts.Sign);
        Assert.Equal("0", parts.IntegerDigits);
        Assert.Equal("3", parts.RepeatingDigits);
    }

    [Fact]
    public void Leading_zero_prefix_and_repetend_are_preserved()
    {
        var parts = FrameValueFormatter.Format(Exact(TimeSpan.TicksPerSecond, new ChapterFrameRate(1, 300)), Compact);

        Assert.Equal("00", parts.NonRepeatingDigits);
        Assert.Equal("3", parts.RepeatingDigits);
    }

    [Fact]
    public void Unknown_rate_provenance_does_not_infer_a_cycle_from_numeric_text()
    {
        var input = FramePresentationInput.FromApproximateRate(0, 23.976m, "1.333333333");

        var parts = FrameValueFormatter.Format(input, Compact);

        Assert.Equal("1.333333333", parts.PlainNumericText);
        Assert.Empty(parts.RepeatingDigits);
        Assert.Null(parts.ExactFractionText);
    }

    [Fact]
    public void Cycle_longer_than_six_digits_uses_ordinary_fallback()
    {
        var parts = FrameValueFormatter.Format(Exact(TimeSpan.TicksPerSecond, new ChapterFrameRate(1, 17)), Compact);

        Assert.Equal("0.059", parts.PlainNumericText);
        Assert.Empty(parts.RepeatingDigits);
        Assert.Equal("1/17", parts.ExactFractionText);
    }

    private static FramePresentationInput Exact(long ticks, ChapterFrameRate rate) =>
        FramePresentationInput.FromExactRate(ticks, rate, "0");
}
