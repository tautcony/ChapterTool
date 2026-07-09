namespace ChapterTool.Infrastructure.Platform;

public sealed record NativeDependencyLocation(
    bool Found,
    string? Path,
    Core.Diagnostics.ChapterDiagnosticCode? DiagnosticCode = null,
    string? Message = null);
