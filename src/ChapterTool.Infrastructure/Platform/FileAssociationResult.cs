using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Infrastructure.Platform;

public sealed record FileAssociationResult(
    bool Success,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
