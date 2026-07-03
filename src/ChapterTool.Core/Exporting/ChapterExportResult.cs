using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Exporting;

public sealed record ChapterExportResult(
    bool Success,
    string Content,
    string FileExtension,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
