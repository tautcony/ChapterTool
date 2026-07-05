using System.Globalization;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public sealed class ExpressionAuthoringService(IExpressionService? expressionService = null) : IExpressionAuthoringService
{
    private readonly IExpressionService expressionService = expressionService ?? new ExpressionService();

    public IReadOnlyList<ExpressionSymbol> Symbols { get; } =
    [
        new("t", ExpressionTokenKind.Variable, "Current chapter time in seconds."),
        new("fps", ExpressionTokenKind.Variable, "Current frame rate."),
        new("M_E", ExpressionTokenKind.Constant, "Euler's number."),
        new("M_LOG2E", ExpressionTokenKind.Constant, "Log2(e)."),
        new("M_LOG10E", ExpressionTokenKind.Constant, "Log10(e)."),
        new("M_PI", ExpressionTokenKind.Constant, "Pi."),
        new("M_PI_2", ExpressionTokenKind.Constant, "Pi divided by 2."),
        new("M_PI_4", ExpressionTokenKind.Constant, "Pi divided by 4."),
        new("M_1_PI", ExpressionTokenKind.Constant, "1 divided by pi."),
        new("M_2_PI", ExpressionTokenKind.Constant, "2 divided by pi."),
        new("M_2_SQRTPI", ExpressionTokenKind.Constant, "2 divided by sqrt(pi)."),
        new("M_LN2", ExpressionTokenKind.Constant, "Natural log of 2."),
        new("M_LN10", ExpressionTokenKind.Constant, "Natural log of 10."),
        new("M_SQRT2", ExpressionTokenKind.Constant, "Square root of 2."),
        new("M_SQRT1_2", ExpressionTokenKind.Constant, "1 divided by square root of 2."),
        new("abs", ExpressionTokenKind.Function, "Absolute value.", 1, "abs()"),
        new("acos", ExpressionTokenKind.Function, "Arc cosine.", 1, "acos()"),
        new("asin", ExpressionTokenKind.Function, "Arc sine.", 1, "asin()"),
        new("atan", ExpressionTokenKind.Function, "Arc tangent.", 1, "atan()"),
        new("atan2", ExpressionTokenKind.Function, "Arc tangent of two values.", 2, "atan2(, )"),
        new("cos", ExpressionTokenKind.Function, "Cosine.", 1, "cos()"),
        new("sin", ExpressionTokenKind.Function, "Sine.", 1, "sin()"),
        new("tan", ExpressionTokenKind.Function, "Tangent.", 1, "tan()"),
        new("cosh", ExpressionTokenKind.Function, "Hyperbolic cosine.", 1, "cosh()"),
        new("sinh", ExpressionTokenKind.Function, "Hyperbolic sine.", 1, "sinh()"),
        new("tanh", ExpressionTokenKind.Function, "Hyperbolic tangent.", 1, "tanh()"),
        new("exp", ExpressionTokenKind.Function, "e raised to a power.", 1, "exp()"),
        new("log", ExpressionTokenKind.Function, "Natural logarithm.", 1, "log()"),
        new("log10", ExpressionTokenKind.Function, "Base-10 logarithm.", 1, "log10()"),
        new("sqrt", ExpressionTokenKind.Function, "Square root.", 1, "sqrt()"),
        new("ceil", ExpressionTokenKind.Function, "Round up.", 1, "ceil()"),
        new("floor", ExpressionTokenKind.Function, "Round down.", 1, "floor()"),
        new("round", ExpressionTokenKind.Function, "Round to nearest integer.", 1, "round()"),
        new("int", ExpressionTokenKind.Function, "Truncate to integer.", 1, "int()"),
        new("sign", ExpressionTokenKind.Function, "Sign of the value.", 1, "sign()"),
        new("pow", ExpressionTokenKind.Function, "Raise a value to a power.", 2, "pow(, )"),
        new("max", ExpressionTokenKind.Function, "Larger of two values.", 2, "max(, )"),
        new("min", ExpressionTokenKind.Function, "Smaller of two values.", 2, "min(, )"),
        new("+", ExpressionTokenKind.Operator, "Add."),
        new("-", ExpressionTokenKind.Operator, "Subtract or negate."),
        new("*", ExpressionTokenKind.Operator, "Multiply."),
        new("/", ExpressionTokenKind.Operator, "Divide."),
        new("%", ExpressionTokenKind.Operator, "Modulo."),
        new("^", ExpressionTokenKind.Operator, "Power."),
        new(">", ExpressionTokenKind.Operator, "Greater than."),
        new("<", ExpressionTokenKind.Operator, "Less than."),
        new(">=", ExpressionTokenKind.Operator, "Greater than or equal."),
        new("<=", ExpressionTokenKind.Operator, "Less than or equal."),
        new("?", ExpressionTokenKind.Operator, "Ternary condition."),
        new(":", ExpressionTokenKind.Operator, "Ternary separator.")
    ];

    public ExpressionAnalysisResult Analyze(string? expression, int caretIndex, decimal timeSeconds = 0, decimal framesPerSecond = 24)
    {
        expression ??= string.Empty;
        caretIndex = Math.Clamp(caretIndex, 0, expression.Length);

        var spans = Classify(expression);
        var token = CurrentToken(expression, caretIndex);
        var completions = Complete(token);
        var diagnostics = Validate(expression, timeSeconds, framesPerSecond);
        if (ShouldSuppressPrefixDiagnostic(expression, caretIndex, token, completions, diagnostics))
        {
            diagnostics = [];
        }

        return new ExpressionAnalysisResult(spans, completions, diagnostics);
    }

    private IReadOnlyList<ExpressionCompletion> Complete((int Start, int Length, string Prefix) token)
    {
        var (start, length, prefix) = token;
        if (prefix.Length == 0)
        {
            return [];
        }

        return Symbols
            .Where(symbol => symbol.Kind is ExpressionTokenKind.Variable or ExpressionTokenKind.Constant or ExpressionTokenKind.Function)
            .Where(symbol => symbol.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(SymbolSortOrder)
            .ThenBy(symbol => symbol.Text.Length)
            .ThenBy(symbol => symbol.Text, StringComparer.Ordinal)
            .Select(symbol => new ExpressionCompletion(
                symbol.Text,
                symbol.Kind,
                symbol.Description,
                start,
                length,
                string.IsNullOrEmpty(symbol.InsertText) ? symbol.Text : symbol.InsertText))
            .ToList();
    }

    private static int SymbolSortOrder(ExpressionSymbol symbol) =>
        symbol.Kind switch
        {
            ExpressionTokenKind.Function => 0,
            ExpressionTokenKind.Variable => 1,
            ExpressionTokenKind.Constant => 2,
            _ => 3
        };

    private static bool ShouldSuppressPrefixDiagnostic(
        string expression,
        int caretIndex,
        (int Start, int Length, string Prefix) token,
        IReadOnlyList<ExpressionCompletion> completions,
        IReadOnlyList<ExpressionAuthoringDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0 || completions.Count == 0 || token.Prefix.Length == 0)
        {
            return false;
        }

        if (caretIndex != token.Start + token.Length)
        {
            return false;
        }

        var currentToken = expression.Substring(token.Start, token.Length);
        if (currentToken.Length == 0 || string.Equals(currentToken, completions[0].Text, StringComparison.Ordinal))
        {
            return false;
        }

        return diagnostics.All(static diagnostic => diagnostic.Diagnostic.Code is "InvalidExpression.UnknownToken" or "InvalidExpression.UnsupportedFunction" or "InvalidExpression.UnsupportedToken");
    }

    private IReadOnlyList<ExpressionAuthoringDiagnostic> Validate(string expression, decimal timeSeconds, decimal framesPerSecond)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        var result = expressionService.EvaluateInfix(expression, timeSeconds, framesPerSecond);
        if (result.Success)
        {
            return [];
        }

        return result.Diagnostics
            .Select(diagnostic => new ExpressionAuthoringDiagnostic(
                diagnostic,
                Suggest(diagnostic.Code),
                DiagnosticStart(expression, diagnostic),
                DiagnosticLength(expression, diagnostic)))
            .ToList();
    }

    private static int DiagnosticStart(string expression, ChapterDiagnostic diagnostic)
    {
        var token = TokenArgument(diagnostic);
        if (string.IsNullOrEmpty(token))
        {
            return Math.Max(0, expression.Length - 1);
        }

        var index = expression.LastIndexOf(token, StringComparison.Ordinal);
        return index >= 0 ? index : Math.Max(0, expression.Length - token.Length);
    }

    private static int DiagnosticLength(string expression, ChapterDiagnostic diagnostic)
    {
        var token = TokenArgument(diagnostic);
        if (!string.IsNullOrEmpty(token))
        {
            return token.Length;
        }

        return expression.Length == 0 ? 0 : 1;
    }

    private static string? TokenArgument(ChapterDiagnostic diagnostic)
    {
        if (diagnostic.Arguments is null)
        {
            return null;
        }

        return diagnostic.Arguments.TryGetValue("token", out var token) ? token as string : null;
    }

    private static ExpressionDiagnosticSuggestion Suggest(string code) =>
        code switch
        {
            "InvalidExpression.Incomplete" => new("Expression.Suggestion.CompleteExpression", "Complete the expression so it produces one value."),
            "InvalidExpression.InsufficientOperands" => new("Expression.Suggestion.AddOperand", "Add the missing operand before applying this token."),
            "InvalidExpression.InvalidCharacter" => new("Expression.Suggestion.RemoveInvalidCharacter", "Remove the invalid character or replace it with a supported token."),
            "InvalidExpression.MisplacedComma" => new("Expression.Suggestion.FixComma", "Use commas only between function arguments."),
            "InvalidExpression.MissingOperandBeforeParen" => new("Expression.Suggestion.AddOperandBeforeParen", "Add an operand before the closing parenthesis."),
            "InvalidExpression.MissingOperator" => new("Expression.Suggestion.AddOperator", "Add an operator between adjacent values."),
            "InvalidExpression.MissingOperatorBeforeFunction" => new("Expression.Suggestion.AddOperatorBeforeFunction", "Add an operator before the function call."),
            "InvalidExpression.MissingOperatorBeforeParen" => new("Expression.Suggestion.AddOperatorBeforeParen", "Add an operator before the opening parenthesis."),
            "InvalidExpression.OperatorRequiresLeftOperand" => new("Expression.Suggestion.AddLeftOperand", "Add a left operand before this operator."),
            "InvalidExpression.TernaryMissingCondition" => new("Expression.Suggestion.AddTernaryCondition", "Add a condition before '?'."),
            "InvalidExpression.TernaryMissingTrueExpression" => new("Expression.Suggestion.AddTernaryTrueExpression", "Add the true expression between '?' and ':'."),
            "InvalidExpression.TernaryUnmatchedColon" => new("Expression.Suggestion.MatchTernaryQuestion", "Add a matching '?' before this ':'."),
            "InvalidExpression.TernaryUnmatchedQuestion" => new("Expression.Suggestion.MatchTernaryColon", "Add a matching ':' and false expression."),
            "InvalidExpression.UnbalancedParentheses" => new("Expression.Suggestion.BalanceParentheses", "Add or remove parentheses so every '(' has a matching ')'."),
            "InvalidExpression.UnknownToken" => new("Expression.Suggestion.UseKnownToken", "Use a supported variable, constant, function, or operator."),
            "InvalidExpression.UnsupportedFunction" => new("Expression.Suggestion.UseSupportedFunction", "Replace the function with a supported expression function."),
            "InvalidExpression.UnsupportedOperator" => new("Expression.Suggestion.UseSupportedOperator", "Replace the operator with a supported expression operator."),
            "InvalidExpression.UnsupportedToken" => new("Expression.Suggestion.UseSupportedToken", "Replace the token with a supported expression token."),
            _ => new("Expression.Suggestion.CheckSyntax", "Check the expression syntax.")
        };

    private IReadOnlyList<ExpressionTokenSpan> Classify(string expression)
    {
        var spans = new List<ExpressionTokenSpan>();
        for (var i = 0; i < expression.Length;)
        {
            var c = expression[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < expression.Length && expression[i + 1] == '/')
            {
                spans.Add(new ExpressionTokenSpan(i, expression.Length - i, expression[i..], ExpressionTokenKind.Comment));
                break;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i++;
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    i++;
                }

                spans.Add(new ExpressionTokenSpan(start, i - start, expression[start..i], ExpressionTokenKind.Number));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i++;
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                var text = expression[start..i];
                spans.Add(new ExpressionTokenSpan(start, i - start, text, KindForSymbol(text)));
                continue;
            }

            if (c is '>' or '<' && i + 1 < expression.Length && expression[i + 1] == '=')
            {
                spans.Add(new ExpressionTokenSpan(i, 2, expression.Substring(i, 2), ExpressionTokenKind.Operator));
                i += 2;
                continue;
            }

            if ("+-*/%^<>\u003f:".Contains(c, StringComparison.Ordinal))
            {
                spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Operator));
                i++;
                continue;
            }

            if ("(),".Contains(c, StringComparison.Ordinal))
            {
                spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Punctuation));
                i++;
                continue;
            }

            spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Unknown));
            i++;
        }

        return spans;
    }

    private ExpressionTokenKind KindForSymbol(string text)
    {
        var symbol = Symbols.FirstOrDefault(symbol => string.Equals(symbol.Text, text, StringComparison.Ordinal));
        if (symbol?.Kind is ExpressionTokenKind.Variable or ExpressionTokenKind.Constant or ExpressionTokenKind.Function)
        {
            return symbol.Kind;
        }

        var matchingKinds = Symbols
            .Where(symbol => symbol.Kind is ExpressionTokenKind.Variable or ExpressionTokenKind.Constant or ExpressionTokenKind.Function)
            .Where(symbol => symbol.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .Select(static symbol => symbol.Kind)
            .Distinct()
            .ToList();

        return matchingKinds.Count switch
        {
            1 => matchingKinds[0],
            > 1 when matchingKinds.Contains(ExpressionTokenKind.Function) => ExpressionTokenKind.Function,
            _ => ExpressionTokenKind.Unknown
        };
    }

    private static (int Start, int Length, string Prefix) CurrentToken(string expression, int caretIndex)
    {
        var start = caretIndex;
        while (start > 0 && IsIdentifierPart(expression[start - 1]))
        {
            start--;
        }

        var end = caretIndex;
        while (end < expression.Length && IsIdentifierPart(expression[end]))
        {
            end++;
        }

        return (start, end - start, expression[start..caretIndex]);
    }

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
