using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ExpressionAuthoringServiceTests
{
    private readonly ExpressionAuthoringService service = new();

    [Fact]
    public void Symbols_include_variables_constants_functions_and_operators()
    {
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "t", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "fps", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "M_PI", Kind: ExpressionTokenKind.Constant });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "floor", Kind: ExpressionTokenKind.Function, Arity: 1 });
        Assert.Contains(service.Symbols, symbol => symbol is { Text: "+", Kind: ExpressionTokenKind.Operator });
    }

    [Fact]
    public void Analyze_classifies_valid_expression_without_diagnostics()
    {
        var result = service.Analyze("t + floor(fps / 2)", 4);

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Spans, span => span is { Text: "t", Kind: ExpressionTokenKind.Variable });
        Assert.Contains(result.Spans, span => span is { Text: "+", Kind: ExpressionTokenKind.Operator });
        Assert.Contains(result.Spans, span => span is { Text: "floor", Kind: ExpressionTokenKind.Function });
        Assert.Contains(result.Spans, span => span is { Text: "(", Kind: ExpressionTokenKind.Punctuation });
        Assert.Contains(result.Spans, span => span is { Text: "2", Kind: ExpressionTokenKind.Number });
    }

    [Fact]
    public void Analyze_returns_completion_for_current_prefix()
    {
        var result = service.Analyze("flo", 3);
        var completion = Assert.Single(result.Completions, item => item.Text == "floor");

        Assert.Equal(0, completion.ReplacementStart);
        Assert.Equal(3, completion.ReplacementLength);
        Assert.Equal("floor()", completion.InsertText);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Analyze_treats_case_insensitive_prefix_as_completion_instead_of_unknown_token()
    {
        var result = service.Analyze("S", 1);

        Assert.Contains(result.Completions, item => item.Text == "sin");
        Assert.Contains(result.Completions, item => item.Text == "sqrt");
        Assert.Contains(result.Completions, item => item.Text == "sign");
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Spans, span => span is { Text: "S", Kind: ExpressionTokenKind.Function });
    }

    [Fact]
    public void Analyze_sorts_functions_before_variables_and_constants()
    {
        var result = service.Analyze("M_", 2);

        Assert.All(result.Completions, completion => Assert.Equal(ExpressionTokenKind.Constant, completion.Kind));

        var mixed = service.Analyze("s", 1).Completions.Take(4).ToList();
        Assert.All(mixed, completion => Assert.Equal(ExpressionTokenKind.Function, completion.Kind));
    }

    [Fact]
    public void Analyze_returns_diagnostic_with_suggestion_for_invalid_expression()
    {
        var result = service.Analyze("t +", 3);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.StartsWith("InvalidExpression.", diagnostic.Diagnostic.Code, StringComparison.Ordinal);
        Assert.Equal("Expression.Suggestion.AddOperand", diagnostic.Suggestion.Code);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Suggestion.Message));
        Assert.Equal(2, diagnostic.Start);
        Assert.Equal(1, diagnostic.Length);
    }

    [Fact]
    public void Analyze_reports_missing_right_operand_for_trailing_binary_operator()
    {
        var result = service.Analyze("2^", 2);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("InvalidExpression.InsufficientOperands", diagnostic.Diagnostic.Code);
        Assert.Equal("Expression.Suggestion.AddOperand", diagnostic.Suggestion.Code);
    }

    [Theory]
    [InlineData("2+", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2-", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2*", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2/", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2%", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2^", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2>", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2<", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2>=", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2<=", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("+", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("-", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2?", "InvalidExpression.TernaryUnmatchedQuestion", "Expression.Suggestion.MatchTernaryColon")]
    [InlineData("2?3:", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    [InlineData("2?3", "InvalidExpression.TernaryUnmatchedQuestion", "Expression.Suggestion.MatchTernaryColon")]
    [InlineData("floor(", "InvalidExpression.UnbalancedParentheses", "Expression.Suggestion.BalanceParentheses")]
    [InlineData("floor()", "InvalidExpression.MissingOperandBeforeParen", "Expression.Suggestion.AddOperandBeforeParen")]
    [InlineData("floor(2,", "InvalidExpression.MisplacedComma", "Expression.Suggestion.FixComma")]
    [InlineData("(2+", "InvalidExpression.InsufficientOperands", "Expression.Suggestion.AddOperand")]
    public void Analyze_reports_specific_suggestion_for_incomplete_expressions(
        string expression,
        string expectedCode,
        string expectedSuggestionCode)
    {
        var result = service.Analyze(expression, expression.Length);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(expectedCode, diagnostic.Diagnostic.Code);
        Assert.Equal(expectedSuggestionCode, diagnostic.Suggestion.Code);
    }
}
