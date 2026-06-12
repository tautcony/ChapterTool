namespace ChapterTool.Core.Transform;

public static class ChapterRounding
{
    public static long RoundToInt64(decimal value) =>
        (long)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static int RoundToInt32(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static TimeSpan SecondsToTimeSpan(decimal seconds) =>
        TimeSpan.FromTicks(RoundToInt64(seconds * TimeSpan.TicksPerSecond));
}
