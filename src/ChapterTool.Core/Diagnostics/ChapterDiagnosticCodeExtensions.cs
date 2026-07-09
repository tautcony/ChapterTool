namespace ChapterTool.Core.Diagnostics;

/// <summary>
/// Provides formatting helpers for diagnostic codes.
/// </summary>
public static class ChapterDiagnosticCodeExtensions
{
    /// <summary>
    /// Returns the stable string form used in localization resources, logs, and CLI output.
    /// </summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns>The stable display code.</returns>
    public static string ToDisplayCode(this ChapterDiagnosticCode code) => $"{code.Source}.{code.Reason}";
}
