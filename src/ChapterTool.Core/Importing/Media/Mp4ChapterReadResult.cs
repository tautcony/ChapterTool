namespace ChapterTool.Core.Importing.Media;

public sealed record Mp4ChapterReadResult(
    bool Success,
    IReadOnlyList<Mp4ChapterClip> Chapters,
    string? DiagnosticCode = null,
    string? Message = null)
{
    public static Mp4ChapterReadResult Succeeded(params Mp4ChapterClip[] chapters) => new(true, chapters);

    public static Mp4ChapterReadResult Failed(string code, string message) => new(false, [], code, message);
}
