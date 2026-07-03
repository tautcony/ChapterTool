using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Infrastructure.Platform;

public sealed record PrivilegeResult(
    bool Success,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
