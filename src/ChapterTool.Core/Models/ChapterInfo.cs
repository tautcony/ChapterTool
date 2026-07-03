namespace ChapterTool.Core.Models;

public sealed record ChapterInfo(
    string Title,
    string? SourceName,
    int SourceIndex,
    string SourceType,
    double FramesPerSecond,
    TimeSpan Duration,
    IReadOnlyList<Chapter> Chapters,
    string Expression = "t",
    object? Tag = null,
    string? TagType = null);
