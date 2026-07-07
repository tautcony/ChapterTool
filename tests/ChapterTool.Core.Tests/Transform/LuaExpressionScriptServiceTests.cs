using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class LuaExpressionScriptServiceTests
{
    private readonly LuaExpressionScriptService service = new();

    [Fact]
    public void Presets_include_common_lua_transforms()
    {
        Assert.Contains(service.Presets, preset => preset.Id == "identity" && preset.ScriptText == "t");
        Assert.Contains(service.Presets, preset => preset.Id == "offset-seconds");
        Assert.Contains(service.Presets, preset => preset.Id == "round-to-frame");
        Assert.Contains(service.Presets, preset => preset.Id == "half-frame-earlier");
    }

    [Fact]
    public void Evaluates_shorthand_arithmetic_without_return()
    {
        var result = service.Evaluate("t + 1", Context(timeSeconds: 10, fps: 24));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(11, result.Value);
    }

    [Fact]
    public void Evaluates_direct_return_script()
    {
        var result = service.Evaluate("return t + math.floor(fps / 2)", Context(timeSeconds: 10, fps: 24));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(22, result.Value);
    }

    [Fact]
    public void Evaluates_transform_function_with_chapter_context()
    {
        var script = "function transform(chapter) return chapter.time + index + count + chapter.number end";

        var result = service.Evaluate(script, Context(timeSeconds: 10, fps: 24, index: 2, count: 4, number: 3));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(19, result.Value);
    }

    [Fact]
    public void Supports_safe_math_aliases_for_low_friction_arithmetic()
    {
        var result = service.Evaluate("floor(t * fps + 0.5) / fps", Context(timeSeconds: 10.49m, fps: 10));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(10.5m, result.Value);
    }

    [Theory]
    [InlineData("return nil")]
    [InlineData("return 'bad'")]
    [InlineData("return 0/0")]
    public void Invalid_return_preserves_original_time(string script)
    {
        var result = service.Evaluate(script, Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal(10, result.Value);
        Assert.StartsWith("InvalidExpression.Lua", Assert.Single(result.Diagnostics).Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("return t +", "InvalidExpression.LuaCompile")]
    [InlineData("return missing()", "InvalidExpression.LuaRuntime")]
    [InlineData("t 1 +", "InvalidExpression.LuaCompile")]
    public void Lua_failures_are_structured_diagnostics(string script, string expectedCode)
    {
        var result = service.Evaluate(script, Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal(10, result.Value);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Script_cannot_use_io_library()
    {
        var result = service.Evaluate("return io.open('chapters.txt')", Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal("InvalidExpression.LuaRuntime", Assert.Single(result.Diagnostics).Code);
    }

    private static LuaExpressionContext Context(decimal timeSeconds, decimal fps, int index = 1, int count = 1, int number = 1) =>
        new(new Chapter(number, TimeSpan.FromSeconds((double)timeSeconds), "Intro", ""), index, count, timeSeconds, fps);

    private static string DiagnosticText(LuaExpressionEvaluationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
