using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

public sealed record ChangeFpsResult(
    bool Success,
    ChapterInfo Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
