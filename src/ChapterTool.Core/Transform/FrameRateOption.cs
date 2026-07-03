namespace ChapterTool.Core.Transform;

public sealed record FrameRateOption(
    string Code,
    string DisplayName,
    decimal Value,
    bool IsValid,
    int LegacyMplsCode);
