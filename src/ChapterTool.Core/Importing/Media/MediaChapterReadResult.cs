namespace ChapterTool.Core.Importing.Media;

public sealed record MediaChapterReadResult(
    bool Success,
    IReadOnlyList<MediaChapterEntry> Chapters,
    string? DiagnosticCode = null,
    string? Message = null,
    string? Details = null)
{
    public static MediaChapterReadResult Succeeded(params MediaChapterEntry[] chapters) => new(true, chapters);

    public static MediaChapterReadResult Failed(string code, string message, string? details = null) =>
        new(false, [], code, message, details);
}
