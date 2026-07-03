using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

public sealed record ChapterEditResult(
    ChapterInfo ChapterInfo,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
