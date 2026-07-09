using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Represents the result of a chapter edit operation.
/// </summary>
/// <param name="ChapterSet">The chapter data after the edit operation.</param>
/// <param name="Diagnostics">Diagnostics produced while editing chapters.</param>
public sealed record ChapterEditResult(
    ChapterSet ChapterSet,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
