using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform.Expressions;

/// <summary>
/// Provides chapter context values to expression engines.
/// </summary>
/// <param name="Chapter">The chapter being evaluated.</param>
/// <param name="Index">The one-based index among non-separator chapters.</param>
/// <param name="Count">The total number of non-separator chapters.</param>
/// <param name="TimeSeconds">The chapter start time in seconds.</param>
/// <param name="FramesPerSecond">The frame rate available to the expression.</param>
/// <param name="Chapters">The ordered snapshot of all non-separator chapters available to the expression.</param>
public sealed record ChapterExpressionContext(
    Chapter Chapter,
    int Index,
    int Count,
    decimal TimeSeconds,
    decimal FramesPerSecond,
    IReadOnlyList<Chapter> Chapters);

/// <summary>
/// Represents the result of expression evaluation.
/// </summary>
/// <param name="Success">Whether the expression evaluated successfully.</param>
/// <param name="Value">The numeric result returned by the expression.</param>
/// <param name="Diagnostics">Diagnostics produced during expression evaluation.</param>
public sealed record ChapterExpressionEvaluationResult(
    bool Success,
    decimal Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    /// <summary>Gets whether an engine returned the unchanged numeric time projection.</summary>
    public bool TimeUnchanged { get; init; }
}

/// <summary>Provides ticks and a canonical computation rate to repository expression engines.</summary>
public sealed record CanonicalChapterExpressionContext(
    Chapter Chapter,
    int Index,
    int Count,
    long StartTicks,
    FrameRateValue FrameRate,
    IReadOnlyList<Chapter> Chapters)
{
    /// <summary>Gets seconds only for the floating-point language boundary.</summary>
    public decimal TimeSeconds => StartTicks / (decimal)TimeSpan.TicksPerSecond;
}

/// <summary>Defines the canonical engine contract used inside the repository.</summary>
public interface ICanonicalChapterExpressionEngine
{
    /// <summary>Evaluates a script against canonical content and computation metadata.</summary>
    ChapterExpressionEvaluationResult Evaluate(string sourceText, CanonicalChapterExpressionContext context);
}

internal static class ChapterExpressionEngineBoundary
{
    public static ChapterExpressionEvaluationResult Evaluate(IChapterExpressionEngine engine, string source,
        CanonicalChapterExpressionContext context) => engine is ICanonicalChapterExpressionEngine canonical
            ? canonical.Evaluate(source, context)
            : engine.Evaluate(source, new ChapterExpressionContext(context.Chapter, context.Index, context.Count,
                context.TimeSeconds, context.FrameRate.ToDecimal(), context.Chapters));
}

/// <summary>
/// Describes a built-in expression preset.
/// </summary>
/// <param name="Id">The stable preset identifier.</param>
/// <param name="DisplayName">The preset label shown to users.</param>
/// <param name="Description">The preset description shown to users.</param>
/// <param name="ScriptText">The expression source text supplied by the preset.</param>
public sealed record ChapterExpressionPreset(
    string Id,
    string DisplayName,
    string Description,
    string ScriptText);

/// <summary>
/// Evaluates chapter time expressions through a pluggable language or execution framework.
/// </summary>
public interface IChapterExpressionEngine
{
    /// <summary>
    /// Gets the stable language or engine identifier.
    /// </summary>
    string EngineId { get; }

    /// <summary>
    /// Gets built-in expression presets supported by the engine.
    /// </summary>
    IReadOnlyList<ChapterExpressionPreset> Presets { get; }

    /// <summary>
    /// Evaluates expression source text against a chapter expression context.
    /// </summary>
    /// <param name="sourceText">The expression source text.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The expression evaluation result.</returns>
    ChapterExpressionEvaluationResult Evaluate(string sourceText, ChapterExpressionContext context);
}
