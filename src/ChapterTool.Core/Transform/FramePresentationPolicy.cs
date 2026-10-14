namespace ChapterTool.Core.Transform;

/// <summary>Presentation choices shared by frame-valued surfaces.</summary>
public sealed record FramePresentationPolicy(
    bool RoundToInteger,
    bool ShowRepeatingDecimals,
    int DecimalPlaces);
