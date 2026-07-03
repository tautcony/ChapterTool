namespace ChapterTool.Infrastructure.Platform;

public sealed record NativeDependencyLocation(
    bool Found,
    string? Path,
    string? DiagnosticCode = null,
    string? Message = null);
