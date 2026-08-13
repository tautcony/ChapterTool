using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ChapterTimeFormatterTests
{
    private readonly ChapterTimeFormatter formatter = new();

    [Theory]
    [InlineData(1, 59, 45, 999, "01:59:45.999")]
    [InlineData(2, 1, 15, 0, "02:01:15.000")]
    [InlineData(3, 45, 59, 123, "03:45:59.123")]
    [InlineData(4, 15, 1, 456, "04:15:01.456")]
    public void Format_uses_legacy_hh_mm_ss_millisecond_shape(int hour, int minute, int second, int millisecond, string expected)
    {
        var time = new TimeSpan(0, hour, minute, second, millisecond);

        var actual = formatter.Format(time);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Format_carries_millisecond_rounding_through_seconds_and_minutes()
    {
        var time = new TimeSpan(0, 0, 59, 59, 999).Add(TimeSpan.FromTicks(9999));

        var actual = formatter.Format(time);

        Assert.Equal("01:00:00.000", actual);
    }

    [Fact]
    public void Format_carries_second_rollover_within_a_minute()
    {
        var time = new TimeSpan(0, 0, 0, 59, 999).Add(TimeSpan.FromTicks(9999));

        var actual = formatter.Format(time);

        Assert.Equal("00:01:00.000", actual);
    }

    [Fact]
    public void Format_uses_legacy_banker_midpoint_rounding()
    {
        var time = TimeSpan.FromTicks(5000);

        var actual = formatter.Format(time);

        Assert.Equal("00:00:00.000", actual);
    }

    [Fact]
    public void Format_uses_total_hours_for_times_over_one_day()
    {
        var time = TimeSpan.FromDays(1) + new TimeSpan(0, 1, 2, 3, 4);

        var actual = formatter.Format(time);

        Assert.Equal("25:02:03.004", actual);
    }

    [Fact]
    public void Format_clamps_negative_times_to_zero()
    {
        var actual = formatter.Format(new TimeSpan(0, 0, -1, -2, -500));

        Assert.Equal("00:00:00.000", actual);
    }

    [Theory]
    [InlineData("01:59:45.999", 0, 1, 59, 45, 999)]
    [InlineData(" 01 : 59 : 45 , 999 ", 0, 1, 59, 45, 999)]
    [InlineData("25:01:02.003", 1, 1, 1, 2, 3)]
    public void ParseOrZero_accepts_legacy_time_text(string text, int days, int hours, int minutes, int seconds, int milliseconds)
    {
        var actual = formatter.ParseOrZero(text);

        Assert.Equal(new TimeSpan(days, hours, minutes, seconds, milliseconds), actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad")]
    [InlineData("01:02:03")]
    [InlineData("01:02:03.12")]
    public void ParseOrZero_returns_zero_for_empty_or_malformed_text(string text)
    {
        var actual = formatter.ParseOrZero(text);

        Assert.Equal(TimeSpan.Zero, actual);
    }

    [Fact]
    public void Parse_returns_invalid_time_diagnostic_for_malformed_text()
    {
        var actual = formatter.Parse("not a time");

        Assert.Equal(TimeSpan.Zero, actual.Value);
        var diagnostic = Assert.Single(actual.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(ChapterDiagnosticCode.InvalidTimeText, diagnostic.Code);
    }

    [Theory]
    [InlineData("999999999999999999999:00:00.000")]
    [InlineData("00:999999999999999999999:00.000")]
    [InlineData("00:00:999999999999999999999.000")]
    public void Parse_returns_invalid_time_diagnostic_for_overflowing_numbers(string text)
    {
        var actual = formatter.Parse(text);

        Assert.Equal(TimeSpan.Zero, actual.Value);
        var diagnostic = Assert.Single(actual.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(ChapterDiagnosticCode.InvalidTimeText, diagnostic.Code);
    }

    [Theory]
    [InlineData(0, 15, 19, 280, "15:19:21")]
    [InlineData(1, 2, 3, 4, "62:03:00")]
    [InlineData(0, 0, 0, 999, "00:01:00")]
    [InlineData(0, 0, 59, 999, "01:00:00")]
    public void FormatCue_uses_total_minutes_and_carries_frame_75_into_seconds(
        int hours,
        int minutes,
        int seconds,
        int milliseconds,
        string expected)
    {
        var time = new TimeSpan(0, hours, minutes, seconds, milliseconds);

        var actual = formatter.FormatCue(time);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatCue_uses_total_minutes_for_times_over_one_day()
    {
        var time = TimeSpan.FromDays(1) + new TimeSpan(0, 1, 2, 3, 0);

        var actual = formatter.FormatCue(time);

        Assert.Equal("1502:03:00", actual);
    }

    [Fact]
    public void FormatCue_clamps_negative_times_to_zero()
    {
        var actual = formatter.FormatCue(TimeSpan.FromSeconds(-5));

        Assert.Equal("00:00:00", actual);
    }
}
