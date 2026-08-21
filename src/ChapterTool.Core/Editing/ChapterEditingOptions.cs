namespace ChapterTool.Core.Editing;

/// <summary>Defines how chapter times change when rows are deleted.</summary>
public enum DeleteRowsTimingMode
{
    /// <summary>Keep the original chapter timestamps.</summary>
    Preserve,
    /// <summary>Shift the remaining timestamps so the first row starts at zero.</summary>
    Normalize
}

/// <summary>Defines how the application formats chapter frame values.</summary>
public enum FrameDisplayMode
{
    /// <summary>Round frame values to integers.</summary>
    Round,
    /// <summary>Format frame values with a fixed number of decimal places.</summary>
    DecimalPlaces
}

/// <summary>Controls chapter editing behavior.</summary>
public sealed record ChapterEditingOptions(
    DeleteRowsTimingMode DeleteRowsTiming = DeleteRowsTimingMode.Preserve,
    FrameDisplayMode FrameDisplay = FrameDisplayMode.Round,
    int FrameDecimalPlaces = 3)
{
    /// <summary>Gets the default editing options.</summary>
    public static ChapterEditingOptions Default { get; } = new();

    /// <summary>Gets the number of decimal places that the frame formatter uses.</summary>
    public int EffectiveFrameDecimalPlaces => FrameDisplay == FrameDisplayMode.Round
        ? 0
        : Math.Clamp(FrameDecimalPlaces, 1, 6);
}
