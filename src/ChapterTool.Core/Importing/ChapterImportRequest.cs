namespace ChapterTool.Core.Importing;

public sealed record ChapterImportRequest(
    string Path,
    Stream? Content = null,
    IReadOnlyDictionary<string, string>? Options = null,
    IProgress<ChapterLoadProgress>? Progress = null);
