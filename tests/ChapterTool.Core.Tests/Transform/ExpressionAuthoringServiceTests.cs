using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ExpressionAuthoringServiceTests
{
    private readonly ExpressionAuthoringService service = new();

    [Fact]
    public void Symbols_include_lua_globals_helpers_keywords_and_presets()
    {
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "t", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "fps", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "index", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "count", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "chapter.time", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "math.floor", Kind: ExpressionTokenKind.Function, Arity: 1 });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "floor", Kind: ExpressionTokenKind.Function, Arity: 1 });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "return", Kind: ExpressionTokenKind.Keyword });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "preset", Kind: ExpressionTokenKind.Snippet });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "preset.identity", Kind: ExpressionTokenKind.Snippet });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "preset.round-to-frame", Kind: ExpressionTokenKind.Snippet });
    }

    [Fact]
    public void Analyze_classifies_valid_lua_shorthand_without_diagnostics()
    {
        var result = service.Analyze("t + math.floor(fps / 2)", 10);

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Spans, span => span is { Text: "t", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(result.Spans, span => span is { Text: "+", Kind: ExpressionTokenKind.Operator });
        Assert.Contains(result.Spans, span => span is { Text: "math.floor", Kind: ExpressionTokenKind.Function });
        Assert.Contains(result.Spans, span => span is { Text: "(", Kind: ExpressionTokenKind.Punctuation });
        Assert.Contains(result.Spans, span => span is { Text: "2", Kind: ExpressionTokenKind.Number });
    }

    [Fact]
    public void Analyze_classifies_transform_function_script()
    {
        var result = service.Analyze("function transform(chapter)\n  return chapter.time + index\nend", 8);

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Spans, span => span is { Text: "function", Kind: ExpressionTokenKind.Keyword });
        Assert.Contains(result.Spans, span => span is { Text: "chapter", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(result.Spans, span => span is { Text: "return", Kind: ExpressionTokenKind.Keyword });
        Assert.Contains(result.Spans, span => span is { Text: "chapter.time", Kind: ExpressionTokenKind.Variable });
    }

    [Fact]
    public void Analyze_returns_completion_for_math_member_prefix()
    {
        var result = service.Analyze("math.flo", 8);
        var completion = Assert.Single(result.Completions, item => item.Text == "math.floor");

        Assert.Equal(0, completion.ReplacementStart);
        Assert.Equal(8, completion.ReplacementLength);
        Assert.Equal("math.floor()", completion.InsertText);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Analyze_returns_completion_for_lua_global_prefix()
    {
        var result = service.Analyze("cha", 3);

        Assert.Contains(result.Completions, item => item.Text == "chapter");
        Assert.Contains(result.Completions, item => item.Text == "chapter.time");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Analyze_returns_preset_snippet_completion()
    {
        var result = service.Analyze("preset.", 7);
        var completion = Assert.Single(result.Completions, item => item.Text == "preset.round-to-frame");

        Assert.Equal(ExpressionTokenKind.Snippet, completion.Kind);
        Assert.Equal("PRESET", completion.KindLabel);
        Assert.Contains("fps", completion.InsertText, StringComparison.Ordinal);
    }


    [Fact]
    public void Analyze_returns_discoverable_preset_namespace_completion()
    {
        var result = service.Analyze("pre", 3);
        var completion = Assert.Single(result.Completions, item => item.Text == "preset");

        Assert.Equal(ExpressionTokenKind.Snippet, completion.Kind);
        Assert.Equal("preset.", completion.InsertText);
        Assert.Contains("presets", completion.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_reports_lua_diagnostic_with_suggestion_for_invalid_shorthand()
    {
        var result = service.Analyze("t +", 3);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(ChapterDiagnosticSource.LuaExpression, diagnostic.Diagnostic.Code.Source);
        Assert.Equal(ChapterDiagnosticReason.CompileFailed, diagnostic.Diagnostic.Code.Reason);
        Assert.Equal("Expression.Suggestion.AddOperand", diagnostic.Suggestion.Code);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Suggestion.Message));
        Assert.Equal(2, diagnostic.Start);
        Assert.Equal(1, diagnostic.Length);
    }

    [Fact]
    public void Analyze_reports_lua_runtime_suggestion_for_unknown_function()
    {
        var result = service.Analyze("missing(t)", 10);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(ChapterDiagnosticCode.InvalidExpressionLuaRuntime, diagnostic.Diagnostic.Code);
        Assert.Equal("Expression.Suggestion.CheckLuaRuntime", diagnostic.Suggestion.Code);
    }

    [Fact]
    public void Analyze_reports_return_number_suggestion_for_invalid_return_type()
    {
        var result = service.Analyze("return 'bad'", 12);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(ChapterDiagnosticCode.InvalidExpressionLuaInvalidReturn, diagnostic.Diagnostic.Code);
        Assert.Equal("Expression.Suggestion.ReturnNumber", diagnostic.Suggestion.Code);
    }
}
