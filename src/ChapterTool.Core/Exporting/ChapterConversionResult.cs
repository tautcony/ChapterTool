using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Exporting;

public sealed record ChapterConversionResult(
    bool Success,
    string Content,
    string Extension,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
