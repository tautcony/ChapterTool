namespace ChapterTool.Contracts.Configuration;

public enum DeleteRowsTimingMode
{
    Preserve,
    Normalize
}

public static class DeleteRowsTimingModes
{
    public static DeleteRowsTimingMode ParseOrDefault(string? value) =>
        string.Equals(value, "normalize", StringComparison.OrdinalIgnoreCase)
            ? DeleteRowsTimingMode.Normalize
            : DeleteRowsTimingMode.Preserve;

    public static string Id(DeleteRowsTimingMode mode) =>
        mode == DeleteRowsTimingMode.Normalize ? "normalize" : "preserve";
}
