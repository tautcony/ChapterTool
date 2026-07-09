namespace ChapterTool.Infrastructure.Services;

public sealed record ExternalToolLocation(
    bool Found,
    string? Path,
    Core.Diagnostics.ChapterDiagnosticCode? DiagnosticCode = null,
    string? Message = null);
