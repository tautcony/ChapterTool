namespace ChapterTool.Core.Transform;

/// <summary>
/// Describes a supported frame rate option.
/// </summary>
/// <param name="Code">The stable frame rate option code.</param>
/// <param name="DisplayName">The frame rate label shown to users.</param>
/// <param name="Value">The frame rate value in frames per second.</param>
/// <param name="IsValid">Whether the option can be used for frame calculations.</param>
/// <param name="LegacyMplsCode">The Blu-ray MPLS frame rate code associated with the option.</param>
public sealed record FrameRateOption(
    string Code,
    string DisplayName,
    decimal Value,
    bool IsValid,
    int LegacyMplsCode);
