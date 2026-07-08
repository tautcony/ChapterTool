using System.Globalization;
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

    [Theory]
    [InlineData("t + 1", 11)]
    [InlineData("fps / 2", 12)]
    [InlineData("math.floor(1.9) + math.ceil(1.1)", 3)]
    [InlineData("math.max(1, 3) + math.min(2, 4)", 5)]
    [InlineData("math.pi > 3 and 1 or 0", 1)]
    [InlineData("1 + (2 * 3)", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("2 + 3 * 4 ^ 2", 50)]
    [InlineData("-t + 2", -8)]
    [InlineData("1 + -2 * 3", -5)]
    [InlineData("10 % 3", 1)]
    [InlineData("math.pow(2, 5)", 32)]
    [InlineData("t > 5 and t + 1 or fps / 2", 11)]
    [InlineData("t < 5 and t + 1 or fps / 2", 12)]
    public void Evaluates_lua_arithmetic_and_context_tokens(string expression, decimal expected)
    {
        var result = service.Evaluate(expression, Context(timeSeconds: 10, fps: 24));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(expected, Math.Round(result.Value, 6));
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
    [InlineData("math.sin(math.asin(1))", 1)]
    [InlineData("math.cos(math.acos(1))", 1)]
    [InlineData("math.tan(math.atan(1))", 1)]
    [InlineData("math.log(1000, 10)", 3)]
    [InlineData("math.sqrt(81)", 9)]
    public void Evaluates_lua_math_library_functions(string expression, decimal expected)
    {
        var result = service.Evaluate(expression, Context(timeSeconds: 0, fps: 24));

        Assert.True(result.Success, DiagnosticText(result));
        Assert.InRange(result.Value, expected - 0.0000000001m, expected + 0.0000000001m);
    }

    [Fact]
    public void Evaluates_uva_12803_expression_fixture_cases_with_lua()
    {
        var input = File.ReadAllLines(FixtureResolver.Fixture("Transform", "UVa-12803.in"));
        var output = File.ReadAllLines(FixtureResolver.Fixture("Transform", "UVa-12803.out"));

        Assert.Equal(input.Length, output.Length);

        for (var i = 0; i < input.Length; i++)
        {
            var result = service.Evaluate(input[i], Context(timeSeconds: 0, fps: 24));

            Assert.True(result.Success, $"Expression #{i + 1} failed: {input[i]}{Environment.NewLine}{DiagnosticText(result)}");
            Assert.Equal(output[i], result.Value.ToString("0.00", CultureInfo.InvariantCulture));
        }
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
