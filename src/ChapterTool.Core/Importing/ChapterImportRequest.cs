namespace ChapterTool.Core.Importing;

/// <summary>
/// Describes a chapter import request.
/// </summary>
/// <param name="Path">The Path value.</param>
/// <param name="Content">The Content value.</param>
/// <param name="Progress">The Progress value.</param>
public sealed record ChapterImportRequest(
    string Path,
    Stream? Content = null,
    IProgress<ChapterLoadProgress>? Progress = null);
