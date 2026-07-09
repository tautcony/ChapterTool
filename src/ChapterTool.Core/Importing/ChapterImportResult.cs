using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing;

/// <summary>
/// Represents the result of a chapter import operation.
/// </summary>
/// <param name="Success">Whether the import completed successfully.</param>
/// <param name="Groups">The imported sources and chapter entries.</param>
/// <param name="Diagnostics">Diagnostics produced during import.</param>
/// <param name="IsPartial">Whether the result contains usable data despite import diagnostics.</param>
public sealed record ChapterImportResult(
    bool Success,
    IReadOnlyList<ChapterImportSource> Groups,
    IReadOnlyList<ChapterDiagnostic> Diagnostics,
    bool IsPartial = false)
{
    /// <summary>
    /// Executes the Succeeded operation.
    /// </summary>
    /// <param name="groups">The imported groups.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult Succeeded(params ChapterImportSource[] groups) =>
        new(true, groups, []);

    /// <summary>
    /// Executes the Failed operation.
    /// </summary>
    /// <param name="diagnostics">The diagnostics for the operation.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult Failed(params ChapterDiagnostic[] diagnostics) =>
        new(false, [], diagnostics);
}
