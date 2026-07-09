namespace ChapterTool.Infrastructure.Configuration;

public sealed record AppSettings(
    string? SavingPath = null,
    string Language = "",
    WindowLocation? MainWindowLocation = null,
    string? MkvToolnixPath = null,
    string? Eac3toPath = null,
    string? FfprobePath = null,
    string? FfmpegPath = null,
    string DefaultSaveFormat = "Txt",
    string DefaultXmlLanguage = "und",
    bool EmitBom = true,
    decimal FrameAccuracyTolerance = 0.15m);

public sealed record WindowLocation(int X, int Y);
