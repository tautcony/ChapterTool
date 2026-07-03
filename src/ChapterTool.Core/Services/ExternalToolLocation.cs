namespace ChapterTool.Core.Services;

public sealed record ExternalToolLocation(
    bool Found,
    string? Path,
    string? DiagnosticCode = null,
    string? Message = null);
