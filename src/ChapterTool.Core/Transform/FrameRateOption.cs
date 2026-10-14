using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>Describes a selectable rate with its provenance.</summary>
public sealed record FrameRateOption(
    string Code,
    string DisplayName,
    FrameRateValue Rate,
    bool IsValid,
    int MplsFrameRateCode);
