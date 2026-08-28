namespace ChapterTool.Contracts.Configuration;

public sealed record AppSettings(
    string? SavingPath = null,
    string Language = "",
    string? MkvToolnixPath = null,
    string? FfprobePath = null,
    string DefaultSaveFormat = "Txt",
    string DefaultXmlLanguage = "und",
    string OutputTextEncoding = "utf8",
    bool EmitBom = false,
    decimal FrameAccuracyTolerance = 0.15m,
    string DeleteRowsTimingMode = "preserve",
    string FrameDisplayMode = "round",
    int FrameDecimalPlaces = 3);
