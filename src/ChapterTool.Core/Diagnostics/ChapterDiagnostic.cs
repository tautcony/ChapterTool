namespace ChapterTool.Core.Diagnostics;

public sealed record ChapterDiagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Location = null,
    string? Details = null,
    IReadOnlyDictionary<string, object?>? Arguments = null);
