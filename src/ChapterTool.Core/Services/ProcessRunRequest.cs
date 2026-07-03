namespace ChapterTool.Core.Services;

public sealed record ProcessRunRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    TimeSpan? Timeout = null,
    bool RedirectOutput = true,
    bool CreateNoWindow = true);
