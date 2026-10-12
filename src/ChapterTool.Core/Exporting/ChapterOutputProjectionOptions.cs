namespace ChapterTool.Core.Exporting;

/// <summary>Describes an explicit content projection before serialization.</summary>
public sealed record ChapterOutputProjectionOptions(
    bool ApplyExpression = false,
    string Expression = "t",
    bool AutoGenerateNames = false,
    bool UseTemplateNames = false,
    string? ChapterNameTemplateText = "",
    int OrderShift = 0,
    Models.FrameRateValue? ComputationRate = null);
