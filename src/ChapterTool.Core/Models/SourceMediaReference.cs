namespace ChapterTool.Core.Models;

public sealed record SourceMediaReference(
    string DisplayName,
    string RelativePath,
    string? AbsolutePath = null);
