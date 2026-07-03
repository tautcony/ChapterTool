namespace ChapterTool.Core.Services;

public sealed record ProcessRunResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    bool Cancelled,
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory);
