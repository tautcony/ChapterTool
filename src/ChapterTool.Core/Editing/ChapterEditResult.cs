using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Editing;

/// <summary>
/// Represents the result of a chapter edit operation.
/// </summary>
/// <param name="ChapterSet">The ChapterSet value.</param>
/// <param name="Diagnostics">The Diagnostics value.</param>
public sealed record ChapterEditResult(
    ChapterSet ChapterSet,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
