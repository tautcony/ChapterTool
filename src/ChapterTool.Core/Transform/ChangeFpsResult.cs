using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Represents the result of changing chapter timing between frame rates.
/// </summary>
/// <param name="Success">The Success value.</param>
/// <param name="Info">The Info value.</param>
/// <param name="Diagnostics">The Diagnostics value.</param>
public sealed record ChangeFpsResult(
    bool Success,
    ChapterSet Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
