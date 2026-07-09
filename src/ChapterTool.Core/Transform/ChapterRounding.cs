namespace ChapterTool.Core.Transform;

/// <summary>
/// Provides deterministic rounding helpers for chapter time calculations.
/// </summary>
public static class ChapterRounding
{
    /// <summary>
    /// Executes the RoundToInt64 operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    public static long RoundToInt64(decimal value) =>
        (long)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Executes the RoundToInt32 operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operation result.</returns>
    public static int RoundToInt32(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Executes the SecondsToTimeSpan operation.
    /// </summary>
    /// <param name="seconds">The duration in seconds to convert.</param>
    /// <returns>The operation result.</returns>
    public static TimeSpan SecondsToTimeSpan(decimal seconds) =>
        TimeSpan.FromTicks(RoundToInt64(seconds * TimeSpan.TicksPerSecond));
}
