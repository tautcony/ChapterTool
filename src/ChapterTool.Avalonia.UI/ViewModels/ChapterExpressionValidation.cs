using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>
/// Builds validation contexts and classifies Lua expression diagnostics for the shell.
/// </summary>
internal static class ChapterExpressionValidation
{
    public static ChapterSet CreateInput(ChapterSet? info)
    {
        var chapters = info?.Chapters.Where(static chapter => !chapter.IsSeparator).ToList() ?? [];
        var chapter = chapters.FirstOrDefault() ?? new Chapter(1, TimeSpan.Zero, "Chapter 01");
        return new ChapterSet(info?.Title ?? string.Empty, info?.SourceName, info?.ImportFormat ?? ChapterImportFormat.Unknown,
            info?.FrameRate ?? FrameRateValue.Unknown, info?.Duration ?? TimeSpan.Zero, chapters.Count > 0 ? chapters : [chapter]);
    }

    public static bool IsLuaExpressionDiagnostic(ChapterDiagnostic diagnostic) =>
        diagnostic.Code.Source is ChapterDiagnosticSource.LuaExpression
            or ChapterDiagnosticSource.LuaExpressionReturn
            or ChapterDiagnosticSource.LuaExpressionToken;
}
