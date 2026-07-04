using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Transform;

public sealed class ExpressionServiceTests
{
    private readonly ExpressionService service = new();

    [Theory]
    [InlineData("t + 1", 11)]
    [InlineData("fps / 2", 12)]
    [InlineData("floor(1.9) + ceil(1.1)", 3)]
    [InlineData("max(1, 3) + min(2, 4)", 5)]
    [InlineData("M_PI > 3", 1)]
    [InlineData("M_LOG2E > 1", 1)]
    [InlineData("1 + (2 * 3)", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("2 + 3 * 4 ^ 2", 50)]
    [InlineData("-t + +2", -8)]
    [InlineData("1 + -2 * 3", -5)]
    [InlineData("10 % 3", 1)]
    [InlineData("pow(2, 5)", 32)]
    [InlineData("1 ? 2 : 3", 2)]
    [InlineData("0 ? 2 : 3", 3)]
    [InlineData("t > 5 ? t + 1 : fps / 2", 11)]
    [InlineData("t < 5 ? t + 1 : fps / 2", 12)]
    [InlineData("1 ? 0 ? 2 : 3 : 4", 3)]
    [InlineData("0 ? 1 : 0 ? 2 : 3", 3)]
    [InlineData("0 ? 1 : -2", -2)]
    [InlineData("(0 ? 1 : 2) ^ 3", 8)]
    public void EvaluateInfix_supports_documented_tokens(string expression, decimal expected)
    {
        var result = service.EvaluateInfix(expression, 10, 24);

        Assert.True(result.Success);
        Assert.Equal(expected, Math.Round(result.Value, 6));
    }

    [Theory]
    [InlineData("1+1/2+1/3+1/4+1/5+1/6+1/7+1/8+1/9+1/10", "2.9289682539682539682539682540")]
    [InlineData("1-1/2-1/4+1/8-1/16+1/32-1/64+1/128-1/256+1/512-1/1024", "0.3330078125")]
    [InlineData("1-(1/2)-(1/4)+(1/8)-(1/16)+(1/32)-(1/64)+(1/128)-(1/256)+(1/512)-(1/1024)", "0.3330078125")]
    [InlineData("2^2^2^2", "65536")]
    [InlineData("2^(2^(2^2))", "65536")]
    public void EvaluateInfix_matches_legacy_arithmetic_cases(string expression, string expected)
    {
        var result = service.EvaluateInfix(expression, 0, 0);

        Assert.True(result.Success);
        Assert.Equal(decimal.Parse(expected), result.Value);
    }

    [Theory]
    [InlineData("floor(1.133) + floor(log10(1023)) - ceil(0.9)", 3)]
    [InlineData("abs(-1908.8976)", 1908.8976)]
    [InlineData("abs(1908.8976)", 1908.8976)]
    [InlineData("abs(-1908)", 1908)]
    [InlineData("abs(1908)", 1908)]
    [InlineData("log10(1000.0)", 3)]
    [InlineData("log10(10 ^ 14)", 14)]
    public void EvaluateInfix_matches_legacy_function_cases(string expression, decimal expected)
    {
        var result = service.EvaluateInfix(expression, 0, 0);

        Assert.True(result.Success);
        Assert.Equal(expected, Math.Round(result.Value, 10));
    }

    [Theory]
    [InlineData("sin(asin(1))", 1)]
    [InlineData("cos(acos(1))", 1)]
    [InlineData("tan(atan(1))", 1)]
    [InlineData("log10(5482.2158)", 3.73895612695404)]
    [InlineData("log10(458723662312872.125782332587)", 14.6615511428938)]
    [InlineData("log10(0.12348583358871)", -0.908382862219234)]
    public void EvaluateInfix_matches_legacy_near_function_cases(string expression, decimal expected)
    {
        var result = service.EvaluateInfix(expression, 0, 0);

        Assert.True(result.Success);
        Assert.InRange(result.Value, expected - 0.0000000001m, expected + 0.0000000001m);
    }

    [Fact]
    public void EvaluateInfix_stops_at_comment()
    {
        var result = service.EvaluateInfix("2^10%10   + 6 \t///comment sample", 0, 0);

        Assert.True(result.Success);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void EvaluatePostfix_matches_legacy_postfix_case()
    {
        var result = service.EvaluatePostfix("2 10 ^ 10 % 6 +".Split(), 0, 0);

        Assert.True(result.Success);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void EvaluateInfix_matches_legacy_expression_fixture_cases()
    {
        var input = File.ReadAllLines(FixtureResolver.Fixture("Transform", "expression.in"));
        var output = File.ReadAllLines(FixtureResolver.Fixture("Transform", "expression.out"));

        Assert.Equal(input.Length, output.Length);

        for (var i = 0; i < input.Length; i++)
        {
            var result = service.EvaluateInfix(input[i], 0, 0);

            Assert.True(result.Success, $"Expression #{i + 1} failed: {input[i]}");
            Assert.Equal(output[i], result.Value.ToString("0.00"));
        }
    }

    [Fact]
    public void EvaluatePostfix_stops_at_comment()
    {
        var result = service.EvaluatePostfix(["t", "1", "+", "//", "100", "*"], 10, 24);

        Assert.True(result.Success);
        Assert.Equal(11, result.Value);
    }

    [Theory]
    [InlineData("t +")]
    [InlineData("and(1, 2)")]
    [InlineData("1 and 2")]
    [InlineData("(t + 1")]
    [InlineData("t + 1)")]
    [InlineData("max(1 2)")]
    [InlineData("max(1, )")]
    [InlineData("1 2")]
    [InlineData("sin 1")]
    [InlineData("1 * * 2")]
    [InlineData("1 ? 2")]
    [InlineData("1 : 2")]
    [InlineData("1 ? : 2")]
    [InlineData("1 ? 2 :")]
    [InlineData("1 ? 2 : 3 : 4")]
    public void Invalid_expression_returns_warning_and_original_time(string expression)
    {
        var result = service.EvaluateInfix(expression, 10, 24);

        Assert.False(result.Success);
        Assert.Equal(10, result.Value);
        Assert.StartsWith("InvalidExpression", Assert.Single(result.Diagnostics).Code);
    }
}
