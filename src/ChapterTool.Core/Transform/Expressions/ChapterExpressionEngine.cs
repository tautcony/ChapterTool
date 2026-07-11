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
    IReadOnlyList<ChapterDiagnostic> Diagnostics);

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
