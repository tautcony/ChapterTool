using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using System.Globalization;

namespace ChapterTool.Core.Transform;

public sealed class ChapterExpressionService(IExpressionService expressionService)
{
    public ChapterExpressionResult Apply(ChapterInfo info, bool applyExpression, string expression)
    {
        if (!applyExpression)
        {
            return new ChapterExpressionResult(info, []);
        }

        var diagnostics = new List<ChapterDiagnostic>();
        var chapters = info.Chapters.Select(chapter =>
        {
            if (chapter.IsSeparator)
            {
                return chapter;
            }

            var evaluated = expressionService.EvaluateInfix(expression, (decimal)chapter.Time.TotalSeconds, (decimal)info.FramesPerSecond);
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

            return chapter with
            {
                Time = TimeSpan.FromSeconds((double)normalized),
                FramesInfo = FormatFrames(normalized, (decimal)info.FramesPerSecond)
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

    private static string FormatFrames(decimal seconds, decimal framesPerSecond)
    {
        var frames = seconds * framesPerSecond;
        var rounded = ChapterRounding.RoundToInt64(frames);
        var marker = Math.Abs(frames - rounded) < 0.01m ? "K" : "*";
        return $"{rounded.ToString(CultureInfo.InvariantCulture)} {marker}";
    }

    private static ChapterDiagnostic InvalidTime(string message) =>
        new(DiagnosticSeverity.Warning, "InvalidExpressionTime", message);
}

public sealed record ChapterExpressionResult(
    ChapterInfo Info,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);
