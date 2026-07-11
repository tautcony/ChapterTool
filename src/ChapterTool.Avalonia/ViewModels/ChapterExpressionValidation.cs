using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.ViewModels;

/// <summary>
/// Builds validation contexts and classifies Lua expression diagnostics for the shell.
/// </summary>
internal static class ChapterExpressionValidation
{
    public static ChapterExpressionContext CreateContext(ChapterSet? info)
    {
        var chapters = info?.Chapters.Where(static chapter => !chapter.IsSeparator).ToList() ?? [];
        var chapter = chapters.FirstOrDefault() ?? new Chapter(1, TimeSpan.Zero, "Chapter 01");
        var fps = info is { FramesPerSecond: > 0 }
            ? (decimal)info.FramesPerSecond
            : 24m;
        return new ChapterExpressionContext(
            chapter,
            1,
            Math.Max(1, chapters.Count),
            (decimal)chapter.StartTime.TotalSeconds,
            fps,
            chapters.Count > 0 ? chapters : [chapter]);
    }

    public static bool IsLuaExpressionDiagnostic(ChapterDiagnostic diagnostic) =>
        diagnostic.Code.Source is ChapterDiagnosticSource.LuaExpression
            or ChapterDiagnosticSource.LuaExpressionReturn
            or ChapterDiagnosticSource.LuaExpressionToken;
}
