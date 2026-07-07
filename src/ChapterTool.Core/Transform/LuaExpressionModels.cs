using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Transform;

public sealed record LuaExpressionContext(
    Chapter Chapter,
    int Index,
    int Count,
    decimal TimeSeconds,
    decimal FramesPerSecond);

public sealed record LuaExpressionEvaluationResult(
    bool Success,
    decimal Value,
    IReadOnlyList<ChapterDiagnostic> Diagnostics);

public sealed record LuaExpressionScriptPreset(
    string Id,
    string DisplayName,
    string Description,
    string ScriptText);

public interface ILuaExpressionScriptService
{
    IReadOnlyList<LuaExpressionScriptPreset> Presets { get; }

    LuaExpressionEvaluationResult Evaluate(string scriptText, LuaExpressionContext context);
}
