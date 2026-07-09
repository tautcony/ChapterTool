using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Represents the result of a chapter conversion operation.
/// </summary>
/// <param name="Success">Whether the conversion completed successfully.</param>
/// <param name="Content">The converted chapter text.</param>
/// <param name="Extension">The recommended output file extension.</param>
/// <param name="Diagnostics">Diagnostics produced during conversion.</param>
public sealed record ChapterConversionResult(
    bool Success,
    string Content,
    string Extension,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
