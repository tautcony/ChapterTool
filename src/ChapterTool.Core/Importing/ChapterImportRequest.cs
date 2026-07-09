namespace ChapterTool.Core.Importing;

/// <summary>
/// Describes a chapter import request.
/// </summary>
/// <param name="Path">The path of the source file to import.</param>
/// <param name="Content">An optional stream containing source content in place of opening <paramref name="Path"/>.</param>
/// <param name="Progress">An optional progress reporter for long-running import operations.</param>
public sealed record ChapterImportRequest(
    string Path,
    Stream? Content = null,
    IProgress<ChapterLoadProgress>? Progress = null);
