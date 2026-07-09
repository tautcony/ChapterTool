namespace ChapterTool.Core.Importing.Media;

/// <summary>
/// Represents the result returned by a media chapter reader.
/// </summary>
/// <param name="Success">Whether the media reader completed successfully.</param>
/// <param name="Chapters">The raw chapter entries returned by the media reader.</param>
/// <param name="DiagnosticCode">The diagnostic code to report when reading fails.</param>
/// <param name="Message">The diagnostic message to report when reading fails.</param>
/// <param name="Details">Additional diagnostic details from the media reader.</param>
public sealed record MediaChapterReadResult(
    bool Success,
    IReadOnlyList<MediaChapterEntry> Chapters,
    string? DiagnosticCode = null,
    string? Message = null,
    string? Details = null)
{
    /// <summary>
    /// Executes the Succeeded operation.
    /// </summary>
    /// <param name="chapters">The chapter entries.</param>
    /// <returns>The operation result.</returns>
    public static MediaChapterReadResult Succeeded(params MediaChapterEntry[] chapters) => new(true, chapters);

    /// <summary>
    /// Executes the Failed operation.
    /// </summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="details">Additional diagnostic details.</param>
    /// <returns>The operation result.</returns>
    public static MediaChapterReadResult Failed(string code, string message, string? details = null) =>
        new(false, [], code, message, details);
}
