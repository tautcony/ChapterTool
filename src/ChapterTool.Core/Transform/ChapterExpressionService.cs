using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using System.Globalization;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Applies expression-based time transforms to chapter data.
/// </summary>
public sealed class ChapterExpressionService
{
    private readonly ILuaExpressionScriptService luaExpressionService;

    /// <summary>
    /// Applies expression-based time transforms to chapter data.
    /// </summary>
    /// <param name="luaExpressionService">The Lua expression service.</param>
    public ChapterExpressionService(ILuaExpressionScriptService? luaExpressionService = null)
    {
        this.luaExpressionService = luaExpressionService ?? new LuaExpressionScriptService();
    }

    /// <summary>
    /// Executes the Apply operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="applyExpression">Whether to apply the expression.</param>
    /// <param name="expression">The expression text.</param>
    /// <returns>The operation result.</returns>
    public ChapterExpressionResult Apply(ChapterSet info, bool applyExpression, string expression)
    {
        if (!applyExpression)
        {
            return new ChapterExpressionResult(info, []);
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var nonSeparatorCount = info.Chapters.Count(static chapter => !chapter.IsSeparator);
        var nonSeparatorIndex = 0;
        if (!FrameRateValidation.TryNormalize(info.FramesPerSecond, out var framesPerSecond, out var frameRateDiagnostic))
        {
            return new ChapterExpressionResult(info, [frameRateDiagnostic!]);
        }

        var chapters = info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter;
            }

            nonSeparatorIndex++;
            var originalSeconds = (decimal)chapter.StartTime.TotalSeconds;
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
                StartTime = TimeSpan.FromSeconds((double)normalized),
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

/// <summary>
/// Represents the result of applying an expression transform.
/// </summary>
/// <param name="Info">The chapter set after applying the expression transform.</param>
/// <param name="Diagnostics">Diagnostics produced while applying the expression.</param>
public sealed record ChapterExpressionResult(
    ChapterSet Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
