using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Exporting;

/// <summary>
/// Represents the result of a chapter export operation.
/// </summary>
/// <param name="Success">Whether the export completed successfully.</param>
/// <param name="Content">The exported chapter file content.</param>
/// <param name="FileExtension">The recommended output file extension.</param>
/// <param name="Diagnostics">Diagnostics produced during export.</param>
public sealed record ChapterExportResult(
    bool Success,
    string Content,
    string FileExtension,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
