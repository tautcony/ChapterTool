using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform.Expressions;
using LuaExpressionScriptService = ChapterTool.Core.Transform.Expressions.Lua.LuaExpressionScriptService;

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
        Assert.Contains(service.Presets, preset => preset.Id == "space-consecutive-chapters" && preset.ScriptText.Contains("chapters[previous_index]", StringComparison.Ordinal));
    }

    [Fact]
    public void Space_consecutive_chapters_preset_handles_three_chapter_collision()
    {
        var preset = Assert.Single(service.Presets, preset => preset.Id == "space-consecutive-chapters");
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.FromSeconds(10), "First"),
            new Chapter(2, TimeSpan.FromSeconds(10.01), "Second"),
            new Chapter(3, TimeSpan.FromSeconds(10.02), "Third")
        };

        var second = service.Evaluate(preset.ScriptText, new ChapterExpressionContext(chapters[1], 2, 3, 10.01m, 24, chapters));
        var third = service.Evaluate(preset.ScriptText, new ChapterExpressionContext(chapters[2], 3, 3, 10.02m, 24, chapters));

        Assert.True(second.Success, DiagnosticText(second));
        Assert.True(third.Success, DiagnosticText(third));
        Assert.Equal(10m + (1m / 24m), second.Value, 10);
        Assert.Equal(10m + (2m / 24m), third.Value, 10);
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
    public void Exposes_all_non_separator_chapters_as_one_based_array()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.FromSeconds(2), "Intro", "48"),
            new Chapter(2, TimeSpan.FromSeconds(10), "Main", "240"),
            new Chapter(3, TimeSpan.FromSeconds(20), "Credits", "480")
        };
        var context = new ChapterExpressionContext(chapters[1], 2, 3, 10, 24, chapters);

        var result = service.Evaluate(
            "return chapters[index - 1].time + chapters[index + 1].time + (chapters[index] == chapter and 1 or 0)",
            context);

        Assert.True(result.Success, DiagnosticText(result));
        Assert.Equal(23, result.Value);
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
        Assert.Equal(ChapterDiagnosticSource.LuaExpressionReturn, Assert.Single(result.Diagnostics).Code.Source);
    }

    [Theory]
    [InlineData("return t +", ChapterDiagnosticReason.CompileFailed)]
    [InlineData("return missing()", ChapterDiagnosticReason.RuntimeFailed)]
    [InlineData("t 1 +", ChapterDiagnosticReason.CompileFailed)]
    public void Lua_failures_are_structured_diagnostics(string script, ChapterDiagnosticReason reason)
    {
        var result = service.Evaluate(script, Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal(10, result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ChapterDiagnosticSource.LuaExpression, diagnostic.Code.Source);
        Assert.Equal(reason, diagnostic.Code.Reason);
    }

    [Fact(Timeout = 2000)]
    public void Infinite_loop_is_cancelled_with_structured_diagnostic()
    {
        var result = service.Evaluate(
            "function transform(chapter) while true do end end",
            Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal(10, result.Value);
        Assert.Equal(ChapterDiagnosticCode.InvalidExpressionLuaCanceled, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Script_cannot_use_io_library()
    {
        var result = service.Evaluate("return io.open('chapters.txt')", Context(timeSeconds: 10, fps: 24));

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.InvalidExpressionLuaRuntime, Assert.Single(result.Diagnostics).Code);
    }

    private static ChapterExpressionContext Context(decimal timeSeconds, decimal fps, int index = 1, int count = 1, int number = 1)
    {
        var chapters = Enumerable.Range(1, count)
            .Select(item => new Chapter(item == index ? number : item, TimeSpan.FromSeconds(item == index ? (double)timeSeconds : item), $"Chapter {item}", ""))
            .ToList();
        return new ChapterExpressionContext(chapters[index - 1], index, count, timeSeconds, fps, chapters);
    }

    private static string DiagnosticText(ChapterExpressionEvaluationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
