namespace ChapterTool.Contracts.Configuration;

public enum FrameDisplayMode
{
    Round,
    DecimalPlaces
}

public static class FrameDisplayModes
{
    public static FrameDisplayMode ParseOrDefault(string? value) =>
        string.Equals(value, "decimal-places", StringComparison.OrdinalIgnoreCase)
            ? FrameDisplayMode.DecimalPlaces
            : FrameDisplayMode.Round;

    public static string Id(FrameDisplayMode mode) =>
        mode == FrameDisplayMode.DecimalPlaces ? "decimal-places" : "round";

    public static int NormalizeDecimalPlaces(int value) => Math.Clamp(value, 1, 6);
}
