namespace ChapterTool.Core.Editing;

/// <summary>Defines how chapter times change when rows are deleted.</summary>
public enum DeleteRowsTimingMode
{
    /// <summary>Keep the original chapter timestamps.</summary>
    Preserve,
    /// <summary>Shift the remaining timestamps so the first row starts at zero.</summary>
    Normalize
}

/// <summary>Controls chapter editing behavior.</summary>
public sealed record ChapterEditingOptions(
    DeleteRowsTimingMode DeleteRowsTiming = DeleteRowsTimingMode.Preserve)
{
    /// <summary>Gets the default editing options.</summary>
    public static ChapterEditingOptions Default { get; } = new();
}
