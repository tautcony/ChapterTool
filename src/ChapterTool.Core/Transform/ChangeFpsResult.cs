using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Represents the result of changing chapter timing between frame rates.
/// </summary>
/// <param name="Success">Whether the frame rate change completed successfully.</param>
/// <param name="Info">The chapter set after frame rate conversion.</param>
/// <param name="Diagnostics">Diagnostics produced while changing frame rates.</param>
public sealed record ChangeFpsResult(
    bool Success,
    ChapterSet Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
