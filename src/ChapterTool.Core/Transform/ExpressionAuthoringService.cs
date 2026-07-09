using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Provides tokenization, diagnostics, and completions for expression authoring.
/// </summary>
/// <param name="expressionEngine">The chapter expression engine.</param>
public sealed class ExpressionAuthoringService(IChapterExpressionEngine? expressionEngine = null) : IExpressionAuthoringService
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while"
    };

    private static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        "+", "-", "*", "/", "%", "^", "#", "==", "~=", "<=", ">=", "<", ">", "=", ".."
    };

    private readonly IChapterExpressionEngine expressionEngine = expressionEngine ?? new Expressions.Lua.LuaExpressionScriptService();

    /// <summary>
    /// Gets symbols available to expression authoring.
    /// </summary>
    public IReadOnlyList<ExpressionSymbol> Symbols { get; } = BuildSymbols(expressionEngine ?? new Expressions.Lua.LuaExpressionScriptService());

    /// <summary>
    /// Executes the Analyze operation.
    /// </summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="caretIndex">The caret index in the expression.</param>
    /// <param name="timeSeconds">The chapter time in seconds.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The operation result.</returns>
    public ExpressionAnalysisResult Analyze(string expression, int caretIndex, decimal timeSeconds = 0, decimal framesPerSecond = 24)
    {
        expression ??= string.Empty;
        caretIndex = Math.Clamp(caretIndex, 0, expression.Length);
        var spans = Tokenize(expression);
        var completions = Complete(expression, caretIndex).ToList();
        var diagnostics = completions.Count > 0 && IsCompletionOnly(expression, caretIndex)
            ? []
            : Diagnostics(expression, spans, timeSeconds, framesPerSecond);
        return new ExpressionAnalysisResult(spans, completions, diagnostics);
    }

    private static IReadOnlyList<ExpressionSymbol> BuildSymbols(IChapterExpressionEngine expressionEngine)
    {
        var symbols = new List<ExpressionSymbol>
        {
            new("t", ExpressionTokenKind.Variable, "Current chapter time in seconds."),
            new("fps", ExpressionTokenKind.Variable, "Current frame rate."),
            new("index", ExpressionTokenKind.Variable, "One-based index of the current non-separator chapter."),
            new("count", ExpressionTokenKind.Variable, "Total non-separator chapter count."),
            new("chapter", ExpressionTokenKind.Variable, "Current chapter context table."),
            new("chapter.time", ExpressionTokenKind.Variable, "Current chapter time in seconds."),
            new("chapter.name", ExpressionTokenKind.Variable, "Current chapter name."),
            new("chapter.number", ExpressionTokenKind.Variable, "Current chapter number."),
            new("math.floor", ExpressionTokenKind.Function, "Round down.", 1, "math.floor()"),
            new("math.ceil", ExpressionTokenKind.Function, "Round up.", 1, "math.ceil()"),
            new("math.abs", ExpressionTokenKind.Function, "Absolute value.", 1, "math.abs()"),
            new("math.min", ExpressionTokenKind.Function, "Smaller of values.", null, "math.min()"),
            new("math.max", ExpressionTokenKind.Function, "Larger of values.", null, "math.max()"),
            new("math.sqrt", ExpressionTokenKind.Function, "Square root.", 1, "math.sqrt()"),
            new("math.sin", ExpressionTokenKind.Function, "Sine.", 1, "math.sin()"),
            new("math.cos", ExpressionTokenKind.Function, "Cosine.", 1, "math.cos()"),
            new("math.tan", ExpressionTokenKind.Function, "Tangent.", 1, "math.tan()"),
            new("floor", ExpressionTokenKind.Function, "Alias for math.floor.", 1, "floor()"),
            new("ceil", ExpressionTokenKind.Function, "Alias for math.ceil.", 1, "ceil()"),
            new("round", ExpressionTokenKind.Function, "Round to nearest integer.", 1, "round()"),
            new("sin", ExpressionTokenKind.Function, "Alias for math.sin.", 1, "sin()"),
            new("sqrt", ExpressionTokenKind.Function, "Alias for math.sqrt.", 1, "sqrt()"),
            new("sign", ExpressionTokenKind.Function, "Sign of the value.", 1, "sign()"),
            new("return", ExpressionTokenKind.Keyword, "Return the transformed time."),
            new("preset", ExpressionTokenKind.Snippet, "Built-in Lua expression presets. Type preset. to browse them.", null, "preset."),
            new("function transform(chapter)\n  return t\nend", ExpressionTokenKind.Snippet, "Reusable transform function snippet.", null, "function transform(chapter)\n  return t\nend")
        };

        symbols.AddRange(expressionEngine.Presets.Select(static preset => new ExpressionSymbol(
            $"preset.{preset.Id}",
            ExpressionTokenKind.Snippet,
            preset.Description,
            InsertText: preset.ScriptText)));

        return symbols;
    }

    private List<ExpressionCompletion> Complete(string expression, int caretIndex)
    {
        var token = CurrentToken(expression, caretIndex);
        if (token.Prefix.Length == 0)
        {
            return [];
        }

        var candidates = Symbols
            .Where(symbol => symbol.Text.StartsWith(token.Prefix, StringComparison.OrdinalIgnoreCase) ||
                             symbol.InsertText.StartsWith(token.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static symbol => CompletionRank(symbol.Kind))
            .ThenBy(static symbol => symbol.Text, StringComparer.OrdinalIgnoreCase)
            .Select(symbol => new ExpressionCompletion(
                symbol.Text,
                symbol.Kind,
                symbol.Description,
                token.Start,
                token.Length,
                string.IsNullOrEmpty(symbol.InsertText) ? symbol.Text : symbol.InsertText))
            .ToList();

        if (token.Prefix.Contains('.', StringComparison.Ordinal) && candidates.Count == 0)
        {
            var suffix = token.Prefix[(token.Prefix.LastIndexOf('.') + 1)..];
            candidates = Symbols
                .Where(symbol => symbol.Text.Contains('.', StringComparison.Ordinal) && symbol.Text[(symbol.Text.LastIndexOf('.') + 1)..].StartsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static symbol => symbol.Text, StringComparer.OrdinalIgnoreCase)
                .Select(symbol => new ExpressionCompletion(
                    symbol.Text,
                    symbol.Kind,
                    symbol.Description,
                    token.Start,
                    token.Length,
                    string.IsNullOrEmpty(symbol.InsertText) ? symbol.Text : symbol.InsertText))
                .ToList();
        }

        return candidates;
    }

    private static bool IsCompletionOnly(string expression, int caretIndex)
    {
        var token = CurrentToken(expression, caretIndex);
        return token.Prefix.Length > 0 &&
               expression[..token.Start].Trim().Length == 0 &&
               expression[(token.Start + token.Length)..].Trim().Length == 0;
    }

    private List<ExpressionAuthoringDiagnostic> Diagnostics(string expression, IReadOnlyList<ExpressionTokenSpan> spans, decimal timeSeconds, decimal framesPerSecond)
    {
        if (spans.Any(static span => span.Kind == ExpressionTokenKind.Unknown))
        {
            var unknown = spans.First(static span => span.Kind == ExpressionTokenKind.Unknown);
            return [Diagnostic("InvalidExpression.LuaUnknownToken", $"Unsupported Lua token '{unknown.Text}'.", "Expression.Suggestion.CheckLuaSyntax", "Check the Lua expression syntax.", unknown.Start, unknown.Length)];
        }

        var result = expressionEngine.Evaluate(
            expression,
            new ChapterExpressionContext(new Models.Chapter(1, TimeSpan.FromSeconds((double)timeSeconds), "Chapter"), 1, 1, timeSeconds, framesPerSecond));
        if (result.Success)
        {
            return [];
        }

        var diagnostic = result.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
        {
            return [];
        }

        var target = LastMeaningfulSpan(spans) ?? new ExpressionTokenSpan(Math.Max(0, expression.Length - 1), expression.Length > 0 ? 1 : 0, string.Empty, ExpressionTokenKind.Unknown);
        var suggestion = SuggestionFor(diagnostic.Code, expression);
        return [new ExpressionAuthoringDiagnostic(diagnostic, suggestion, target.Start, Math.Max(1, target.Length))];
    }

    private static ExpressionDiagnosticSuggestion SuggestionFor(string diagnosticCode, string expression) =>
        diagnosticCode switch
        {
            "InvalidExpression.LuaCompile" when EndsWithOperator(expression) => new("Expression.Suggestion.AddOperand", "Add the missing operand before applying this token."),
            "InvalidExpression.LuaCompile" => new("Expression.Suggestion.CheckLuaSyntax", "Check the Lua syntax or complete the expression."),
            "InvalidExpression.LuaRuntime" => new("Expression.Suggestion.CheckLuaRuntime", "Check that referenced Lua variables and functions exist."),
            "InvalidExpression.LuaInvalidReturn" => new("Expression.Suggestion.ReturnNumber", "Return a finite number of seconds."),
            _ => new("Expression.Suggestion.CheckLuaSyntax", "Check the Lua expression syntax.")
        };

    private static bool EndsWithOperator(string expression)
    {
        var trimmed = expression.TrimEnd();
        return trimmed.EndsWith('+') || trimmed.EndsWith('-') || trimmed.EndsWith('*') || trimmed.EndsWith('/') || trimmed.EndsWith('%') || trimmed.EndsWith('^') || trimmed.EndsWith('.');
    }

    private static ExpressionAuthoringDiagnostic Diagnostic(string code, string message, string suggestionCode, string suggestion, int start, int length) =>
        new(new ChapterDiagnostic(DiagnosticSeverity.Warning, code, message, Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = message }), new ExpressionDiagnosticSuggestion(suggestionCode, suggestion), start, length);

    private static ExpressionTokenSpan? LastMeaningfulSpan(IReadOnlyList<ExpressionTokenSpan> spans) =>
        spans.LastOrDefault(static span => span.Kind != ExpressionTokenKind.Comment);

    private static int CompletionRank(ExpressionTokenKind kind) => kind switch
    {
        ExpressionTokenKind.Function => 0,
        ExpressionTokenKind.Variable => 1,
        ExpressionTokenKind.Keyword => 2,
        ExpressionTokenKind.Snippet => 3,
        _ => 4
    };

    private static List<ExpressionTokenSpan> Tokenize(string expression)
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

            if (c == '-' && i + 1 < expression.Length && expression[i + 1] == '-')
            {
                spans.Add(new ExpressionTokenSpan(i, expression.Length - i, expression[i..], ExpressionTokenKind.Comment));
                break;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var number = TryReadNumberOrDot(expression, ref i);
                spans.Add(number);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                spans.Add(ReadIdentifierChain(expression, ref i));
                continue;
            }

            var two = i + 1 < expression.Length ? expression.Substring(i, 2) : string.Empty;
            if (Operators.Contains(two))
            {
                spans.Add(new ExpressionTokenSpan(i, 2, two, ExpressionTokenKind.Operator));
                i += 2;
                continue;
            }

            if (Operators.Contains(c.ToString(CultureInfo.InvariantCulture)))
            {
                spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Operator));
                i++;
                continue;
            }

            if (c is '(' or ')' or ',' or ';' or '{' or '}' or '[' or ']')
            {
                spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Punctuation));
                i++;
                continue;
            }

            if (c is '\'' or '"')
            {
                spans.Add(ReadString(expression, ref i));
                continue;
            }

            spans.Add(new ExpressionTokenSpan(i, 1, c.ToString(CultureInfo.InvariantCulture), ExpressionTokenKind.Unknown));
            i++;
        }

        return spans;
    }

    private static ExpressionTokenSpan TryReadNumberOrDot(string expression, ref int i)
    {
        var start = i;
        if (expression[i] == '.' && (i + 1 >= expression.Length || !char.IsDigit(expression[i + 1])))
        {
            i++;
            return new ExpressionTokenSpan(start, 1, ".", ExpressionTokenKind.Punctuation);
        }

        i++;
        while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
        {
            i++;
        }

        return new ExpressionTokenSpan(start, i - start, expression[start..i], ExpressionTokenKind.Number);
    }

    private static ExpressionTokenSpan ReadIdentifierChain(string expression, ref int i)
    {
        var start = i;
        i++;
        while (i < expression.Length)
        {
            if (char.IsLetterOrDigit(expression[i]) || expression[i] == '_')
            {
                i++;
                continue;
            }

            if (expression[i] == '.' && i + 1 < expression.Length && (char.IsLetter(expression[i + 1]) || expression[i + 1] == '_'))
            {
                i += 2;
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }
                continue;
            }

            break;
        }

        var text = expression[start..i];
        return new ExpressionTokenSpan(start, i - start, text, KindForIdentifier(text));
    }

    private static ExpressionTokenSpan ReadString(string expression, ref int i)
    {
        var start = i;
        var quote = expression[i++];
        while (i < expression.Length)
        {
            if (expression[i] == quote && expression[i - 1] != '\\')
            {
                i++;
                break;
            }

            i++;
        }

        return new ExpressionTokenSpan(start, i - start, expression[start..i], ExpressionTokenKind.String);
    }

    private static ExpressionTokenKind KindForIdentifier(string text)
    {
        if (Keywords.Contains(text))
        {
            return ExpressionTokenKind.Keyword;
        }

        if (text is "t" or "fps" or "index" or "count" or "chapter" || text.StartsWith("chapter.", StringComparison.Ordinal))
        {
            return ExpressionTokenKind.Variable;
        }

        if (text.StartsWith("math.", StringComparison.Ordinal) || text is "floor" or "ceil" or "round" or "abs" or "min" or "max" or "sqrt" or "sin" or "cos" or "tan" or "sign")
        {
            return ExpressionTokenKind.Function;
        }

        return ExpressionTokenKind.Variable;
    }

    private static (int Start, int Length, string Prefix) CurrentToken(string expression, int caretIndex)
    {
        var start = caretIndex;
        while (start > 0 && IsCompletionPart(expression[start - 1]))
        {
            start--;
        }

        var end = caretIndex;
        while (end < expression.Length && IsCompletionPart(expression[end]))
        {
            end++;
        }

        return (start, end - start, expression[start..caretIndex]);
    }

    private static bool IsCompletionPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '-';
}
