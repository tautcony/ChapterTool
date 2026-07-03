namespace ChapterTool.Core.Importing.Media;

public sealed record MediaChapterEntry(
    int? Id,
    string? TimeBase,
    long? Start,
    long? End,
    string? StartTime,
    string? EndTime,
    IReadOnlyDictionary<string, string> Tags,
    int SourceOrder);
