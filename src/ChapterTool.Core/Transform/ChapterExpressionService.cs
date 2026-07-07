using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using System.Globalization;

namespace ChapterTool.Core.Transform;

public sealed class ChapterExpressionService
{
    private readonly ILuaExpressionScriptService luaExpressionService;

    public ChapterExpressionService(ILuaExpressionScriptService? luaExpressionService = null)
    {
        this.luaExpressionService = luaExpressionService ?? new LuaExpressionScriptService();
    }

    public ChapterExpressionService(IExpressionService _)
        : this(new LuaExpressionScriptService())
    {
    }

    public ChapterExpressionResult Apply(ChapterInfo info, bool applyExpression, string expression)
    {
        if (!applyExpression)
        {
            return new ChapterExpressionResult(info, []);
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var nonSeparatorCount = info.Chapters.Count(static chapter => !chapter.IsSeparator);
        var nonSeparatorIndex = 0;
        var framesPerSecond = (decimal)info.FramesPerSecond;
        var chapters = info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter;
            }

            nonSeparatorIndex++;
            var originalSeconds = (decimal)chapter.Time.TotalSeconds;
            var evaluated = luaExpressionService.Evaluate(
                expression,
                new LuaExpressionContext(chapter, nonSeparatorIndex, nonSeparatorCount, originalSeconds, framesPerSecond));
            diagnostics.AddRange(evaluated.Diagnostics);
            if (!evaluated.Success)
            {
                return chapter;
            }

            var normalized = NormalizeSeconds(evaluated.Value, out var diagnostic);
            if (diagnostic is not null)
            {
                diagnostics.Add(diagnostic);
            }

            var frameDisplay = FormatFrames(normalized, framesPerSecond);
            return chapter with
            {
                Time = TimeSpan.FromSeconds((double)normalized),
                FramesInfo = frameDisplay.Text,
                FrameAccuracy = frameDisplay.Accuracy
            };
        }).ToList();

        return new ChapterExpressionResult(info with { Chapters = chapters }, diagnostics);
    }

    private static decimal NormalizeSeconds(decimal seconds, out ChapterDiagnostic? diagnostic)
    {
        if (seconds < 0)
        {
            diagnostic = InvalidTime($"Expression result cannot be negative: {seconds}.");
            return 0m;
        }

        if (seconds > (decimal)TimeSpan.MaxValue.TotalSeconds)
        {
            diagnostic = InvalidTime($"Expression result is too large: {seconds}.");
            return (decimal)TimeSpan.MaxValue.TotalSeconds;
        }

        diagnostic = null;
        return seconds;
    }

    private static FrameDisplay FormatFrames(decimal seconds, decimal framesPerSecond)
    {
        var frames = seconds * framesPerSecond;
        var rounded = ChapterRounding.RoundToInt64(frames);
        var accuracy = Math.Abs(frames - rounded) < 0.01m ? FrameAccuracy.Accurate : FrameAccuracy.Inexact;
        return new FrameDisplay(rounded.ToString(CultureInfo.InvariantCulture), accuracy);
    }

    private static ChapterDiagnostic InvalidTime(string message) =>
        new(DiagnosticSeverity.Warning, "InvalidExpressionTime", message);

    private sealed record FrameDisplay(string Text, FrameAccuracy Accuracy);
}

public sealed record ChapterExpressionResult(
    ChapterInfo Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
