using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ChapterRoundingTests
{
    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    [InlineData(2.49, 2)]
    [InlineData(-2.49, -2)]
    public void RoundToInt64_uses_away_from_zero_midpoint_rounding(double value, long expected)
    {
        Assert.Equal(expected, ChapterRounding.RoundToInt64((decimal)value));
    }

    [Fact]
    public void SecondsToTimeSpan_rounds_to_nearest_tick()
    {
        Assert.Equal(TimeSpan.FromTicks(1), ChapterRounding.SecondsToTimeSpan(0.00000005m));
        Assert.Equal(TimeSpan.FromTicks(-1), ChapterRounding.SecondsToTimeSpan(-0.00000005m));
    }

    [Fact]
    public void SecondsToTimeSpan_supports_values_near_maximum_timespan()
    {
        var seconds = TimeSpan.MaxValue.Ticks / (decimal)TimeSpan.TicksPerSecond;

        Assert.Equal(TimeSpan.MaxValue, ChapterRounding.SecondsToTimeSpan(seconds));
    }
}
