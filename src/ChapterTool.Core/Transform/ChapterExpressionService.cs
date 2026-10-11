using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Applies expression-based time transforms to chapter data.
/// </summary>
public sealed class ChapterExpressionService
{
    private readonly IChapterExpressionEngine expressionEngine;

    /// <summary>
    /// Applies expression-based time transforms to chapter data.
    /// </summary>
    /// <param name="expressionEngine">The chapter expression engine.</param>
    public ChapterExpressionService(IChapterExpressionEngine? expressionEngine = null)
    {
        this.expressionEngine = expressionEngine ?? new Expressions.Lua.LuaExpressionScriptService();
    }

    /// <summary>
    /// Executes the Apply operation.
    /// </summary>
    /// <param name="info">The chapter data to process.</param>
    /// <param name="applyExpression">Whether to apply the expression.</param>
    /// <param name="expression">The expression text.</param>
    /// <returns>The operation result.</returns>
    public ChapterExpressionResult Apply(ChapterSet info, bool applyExpression, string expression) =>
        Apply(info, applyExpression, expression, computationRate: null);

    /// <summary>Applies a script with a captured canonical computation-rate override.</summary>
    public ChapterExpressionResult Apply(ChapterSet info, bool applyExpression, string expression, FrameRateValue? computationRate)
    {
        if (!applyExpression)
        {
            return new ChapterExpressionResult(info, []);
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var seenDiagnostics = new HashSet<(ChapterDiagnosticCode Code, string Message, string? Location)>();
        var expressionChapters = info.Chapters.Where(static chapter => !chapter.IsSeparator).ToList();
        var nonSeparatorCount = expressionChapters.Count;
        var nonSeparatorIndex = 0;
        var framesPerSecond = computationRate ?? info.FrameRate;
        if (computationRate is { IsKnown: false })
        {
            return new ChapterExpressionResult(info, [FrameRateValidation.InvalidFrameRate()]);
        }

        var chapters = info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter;
            }

            nonSeparatorIndex++;
            var originalSeconds = chapter.StartTime.Ticks / (decimal)TimeSpan.TicksPerSecond;
            var evaluated = ChapterExpressionEngineBoundary.Evaluate(expressionEngine, expression,
                new CanonicalChapterExpressionContext(chapter, nonSeparatorIndex, nonSeparatorCount,
                    chapter.StartTime.Ticks, framesPerSecond, expressionChapters));
            foreach (var expressionDiagnostic in evaluated.Diagnostics)
            {
                // An expression can fail identically for every chapter (for example, while
                // the user is typing an incomplete identifier). Keep the preview diagnostic
                // list focused on distinct problems instead of repeating one message per row.
                if (seenDiagnostics.Add((expressionDiagnostic.Code, expressionDiagnostic.Message, expressionDiagnostic.Location)))
                {
                    diagnostics.Add(expressionDiagnostic);
                }
            }
            if (!evaluated.Success)
            {
                return chapter;
            }

            try
            {
                var ticks = evaluated.TimeUnchanged || evaluated.Value == originalSeconds
                    ? chapter.StartTime.Ticks : ChapterRounding.RoundToInt64(evaluated.Value * TimeSpan.TicksPerSecond);
                if (ticks < 0)
                {
                    diagnostics.Add(InvalidTime("Expression result cannot be negative."));
                    return chapter;
                }

                var frameDisplay = FormatFrames(ticks, framesPerSecond);
                return chapter with
                {
                    StartTime = TimeSpan.FromTicks(ticks),
                    FramesInfo = frameDisplay.Text,
                    FrameAccuracy = frameDisplay.Accuracy
                };
            }
            catch (OverflowException)
            {
                diagnostics.Add(InvalidTime("Expression result exceeds the supported time or frame range."));
                return chapter;
            }
        }).ToList();

        return new ChapterExpressionResult(diagnostics.Count == 0 ? info with { Chapters = chapters } : info, diagnostics);
    }

    private static FrameDisplay FormatFrames(long ticks, FrameRateValue framesPerSecond)
    {
        if (!framesPerSecond.IsKnown)
        {
            return new FrameDisplay(string.Empty, FrameAccuracy.Neutral);
        }

        var frames = ExactFrameMath.FromTicks(ticks, framesPerSecond);
        var rounded = frames.RoundAwayFromZero();
        var delta = frames - new ExactFrameMath(rounded, System.Numerics.BigInteger.One);
        var accuracy = delta.IsWithin(0.01m) ? FrameAccuracy.Accurate : FrameAccuracy.Inexact;
        return new FrameDisplay(checked((long)rounded).ToString(CultureInfo.InvariantCulture), accuracy);
    }

    private static ChapterDiagnostic InvalidTime(string message) =>
        new(DiagnosticSeverity.Warning, ChapterDiagnosticCode.InvalidExpressionTime, message);

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
