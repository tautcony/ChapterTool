namespace ChapterTool.Core.Diagnostics;

/// <summary>
/// Represents a diagnostic message produced while importing, editing, transforming, or exporting chapters.
/// </summary>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Message">The user-facing diagnostic message.</param>
/// <param name="Location">The source location associated with the diagnostic, when available.</param>
/// <param name="Details">Additional diagnostic details for troubleshooting, when available.</param>
/// <param name="Arguments">Structured diagnostic arguments for localization or formatting, when available.</param>
public sealed record ChapterDiagnostic(
    DiagnosticSeverity Severity,
    ChapterDiagnosticCode Code,
    string Message,
    string? Location = null,
    string? Details = null,
    IReadOnlyDictionary<string, object?>? Arguments = null)
{
    /// <summary>
    /// Gets the stable string code used in localization resources, logs, and CLI output.
    /// </summary>
    public string DisplayCode => Code.ToDisplayCode();
}
