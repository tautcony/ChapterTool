namespace ChapterTool.Contracts.Configuration;

public sealed record AppSettings(
    string? SavingPath = null,
    string Language = "",
    WindowLocation? MainWindowLocation = null,
    string? MkvToolnixPath = null,
    string? FfprobePath = null,
    string DefaultSaveFormat = "Txt",
    string DefaultXmlLanguage = "und",
    string OutputTextEncoding = "utf8",
    bool EmitBom = false,
    decimal FrameAccuracyTolerance = 0.15m);

public sealed record WindowLocation(int X, int Y);
