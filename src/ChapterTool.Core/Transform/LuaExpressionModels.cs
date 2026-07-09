using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Provides chapter context values to Lua expression scripts.
/// </summary>
/// <param name="Chapter">The chapter being evaluated.</param>
/// <param name="Index">The one-based index among non-separator chapters.</param>
/// <param name="Count">The total number of non-separator chapters.</param>
/// <param name="TimeSeconds">The chapter start time in seconds.</param>
/// <param name="FramesPerSecond">The frame rate available to the script.</param>
public sealed record LuaExpressionContext(
    Chapter Chapter,
    int Index,
    int Count,
    decimal TimeSeconds,
    decimal FramesPerSecond);

/// <summary>
/// Represents the result of Lua expression evaluation.
/// </summary>
/// <param name="Success">Whether the Lua expression evaluated successfully.</param>
/// <param name="Value">The numeric result returned by the Lua expression.</param>
/// <param name="Diagnostics">Diagnostics produced during Lua evaluation.</param>
public sealed record LuaExpressionEvaluationResult(
    bool Success,
    decimal Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);

/// <summary>
/// Describes a built-in Lua expression preset.
/// </summary>
/// <param name="Id">The stable preset identifier.</param>
/// <param name="DisplayName">The preset label shown to users.</param>
/// <param name="Description">The preset description shown to users.</param>
/// <param name="ScriptText">The Lua script text supplied by the preset.</param>
public sealed record LuaExpressionScriptPreset(
    string Id,
    string DisplayName,
    string Description,
    string ScriptText);

/// <summary>
/// Evaluates Lua scripts for chapter time transforms.
/// </summary>
public interface ILuaExpressionScriptService
{
    /// <summary>
    /// Gets built-in Lua expression presets.
    /// </summary>
    IReadOnlyList<LuaExpressionScriptPreset> Presets { get; }

    /// <summary>
    /// Evaluates Lua script text against a chapter expression context.
    /// </summary>
    /// <param name="scriptText">The Lua script text.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The Lua expression evaluation result.</returns>
    LuaExpressionEvaluationResult Evaluate(string scriptText, LuaExpressionContext context);
}
