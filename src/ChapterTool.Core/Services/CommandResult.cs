using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Services;

public sealed record CommandResult(
    bool Success,
    string StatusText,
    int Progress,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public static CommandResult Ok(string statusText = "", int progress = 100) =>
        new(true, statusText, progress, []);
}
