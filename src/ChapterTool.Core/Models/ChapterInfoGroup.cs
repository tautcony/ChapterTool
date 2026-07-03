namespace ChapterTool.Core.Models;

public sealed record ChapterInfoGroup(
    string SourcePath,
    IReadOnlyList<ChapterSourceOption> Options,
    int DefaultOptionIndex = 0);
