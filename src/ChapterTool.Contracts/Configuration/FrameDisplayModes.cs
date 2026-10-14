namespace ChapterTool.Contracts.Configuration;

public enum FrameDisplayMode
{
    Round,
    DecimalPlaces,
    FullPrecision
}

public static class FrameDisplayModes
{
    public static FrameDisplayMode ParseOrDefault(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "decimal-places" => FrameDisplayMode.DecimalPlaces,
        "full-precision" => FrameDisplayMode.FullPrecision,
        _ => FrameDisplayMode.Round
    };

    public static string Id(FrameDisplayMode mode) =>
        mode switch
        {
            FrameDisplayMode.DecimalPlaces => "decimal-places",
            FrameDisplayMode.FullPrecision => "full-precision",
            _ => "round"
        };

    public static int NormalizeDecimalPlaces(int value) => Math.Clamp(value, 1, 6);
}
