using System.Globalization;
using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public sealed class ExpressionService : IExpressionService
{
    private static readonly Dictionary<string, decimal> Constants = new(StringComparer.Ordinal)
    {
        ["M_E"] = 2.71828182845904523536m,
        ["M_LOG2E"] = 1.44269504088896340736m,
        ["M_LOG10E"] = 0.43429448190325182765m,
        ["M_PI"] = 3.14159265358979323846m,
        ["M_PI_2"] = 1.57079632679489661923m,
        ["M_PI_4"] = 0.78539816339744830962m,
        ["M_1_PI"] = 0.31830988618379067154m,
        ["M_2_PI"] = 0.63661977236758134308m,
        ["M_2_SQRTPI"] = 1.12837916709551257390m,
        ["M_LN2"] = 0.69314718055994530942m,
        ["M_LN10"] = 2.30258509299404568402m,
        ["M_SQRT2"] = 1.41421356237309504880m,
        ["M_SQRT1_2"] = 0.70710678118654752440m
    };

    private static readonly Dictionary<string, int> Functions = new(StringComparer.Ordinal)
    {
        ["abs"] = 1,
        ["acos"] = 1,
        ["asin"] = 1,
        ["atan"] = 1,
        ["atan2"] = 2,
        ["cos"] = 1,
        ["sin"] = 1,
        ["tan"] = 1,
        ["cosh"] = 1,
        ["sinh"] = 1,
        ["tanh"] = 1,
        ["exp"] = 1,
        ["log"] = 1,
        ["log10"] = 1,
        ["sqrt"] = 1,
        ["ceil"] = 1,
        ["floor"] = 1,
        ["round"] = 1,
        ["int"] = 1,
        ["sign"] = 1,
        ["pow"] = 2,
        ["max"] = 2,
        ["min"] = 2
    };

    private static readonly Dictionary<string, int> Precedence = new(StringComparer.Ordinal)
    {
        [">"] = -1,
        ["<"] = -1,
        [">="] = -1,
        ["<="] = -1,
        ["+"] = 0,
        ["-"] = 0,
        ["*"] = 1,
        ["/"] = 1,
        ["%"] = 1,
        ["^"] = 2,
        ["u+"] = 3,
        ["u-"] = 3,
        ["?:"] = -2
    };

    private static readonly HashSet<string> RightAssociativeOperators = new(StringComparer.Ordinal)
    {
        "^",
        "u+",
        "u-",
        "?:"
    };

    public ExpressionEvaluationResult EvaluateInfix(string expression, decimal timeSeconds, decimal framesPerSecond)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                expression = "t";
            }

            var postfix = ToPostfix(Tokenize(expression));
            return EvaluatePostfix(postfix, timeSeconds, framesPerSecond);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        {
            return exception is ExpressionException ee
                ? Failure(timeSeconds, ee.Code, ee.Message, ee.Arguments)
                : Failure(timeSeconds, "InvalidExpression", exception.Message);
        }
    }

    public ExpressionEvaluationResult EvaluatePostfix(IEnumerable<string> tokens, decimal timeSeconds, decimal framesPerSecond)
    {
        try
        {
            var values = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["t"] = timeSeconds,
                ["fps"] = framesPerSecond
            };
            var stack = new Stack<decimal>();

            foreach (var token in tokens.TakeWhile(token => !token.StartsWith("//", StringComparison.Ordinal)).Where(token => token.Length > 0))
            {
                if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    stack.Push(number);
                }
                else if (values.TryGetValue(token, out var variable))
                {
                    stack.Push(variable);
                }
                else if (Constants.TryGetValue(token, out var constant))
                {
                    stack.Push(constant);
                }
                else if (Functions.ContainsKey(token))
                {
                    ApplyFunction(token, stack);
                }
                else if (Precedence.ContainsKey(token))
                {
                    ApplyOperator(token, stack);
                }
                else if (token is "and" or "or" or "xor")
                {
                    throw new ExpressionException("InvalidExpression.UnsupportedOperator", $"Unsupported operator '{token}'.", Args(("token", token)));
                }
                else
                {
                    throw new ExpressionException("InvalidExpression.UnknownToken", $"Unknown token '{token}'.", Args(("token", token)));
                }
            }

            if (stack.Count != 1)
            {
                throw new ExpressionException("InvalidExpression.Incomplete", "Expression did not reduce to one value.");
            }

            return new ExpressionEvaluationResult(true, stack.Pop(), []);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DivideByZeroException or OverflowException)
        {
            return exception is ExpressionException ee
                ? Failure(timeSeconds, ee.Code, ee.Message, ee.Arguments)
                : Failure(timeSeconds, "InvalidExpression", exception.Message);
        }
    }

    private static ExpressionEvaluationResult Failure(decimal fallback, string code, string message, IReadOnlyDictionary<string, object?>? arguments = null) =>
        new(
            false,
            fallback,
            [
                new ChapterDiagnostic(DiagnosticSeverity.Warning, code, message, Arguments: arguments)
            ]);

    private static IReadOnlyDictionary<string, object?>? Args(params (string Name, object? Value)[] pairs) =>
        pairs.Length == 0 ? null : pairs.ToDictionary(static pair => pair.Name, static pair => pair.Value, StringComparer.Ordinal);

    private sealed class ExpressionException : InvalidOperationException
    {
        public ExpressionException(string code, string message, IReadOnlyDictionary<string, object?>? arguments = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
            Arguments = arguments;
        }

        public string Code { get; }
        public IReadOnlyDictionary<string, object?>? Arguments { get; }
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
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
                break;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = i++;
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    i++;
                }

                tokens.Add(expression[start..i]);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i++;
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                tokens.Add(expression[start..i]);
                continue;
            }

            if (c is '>' or '<' && i + 1 < expression.Length && expression[i + 1] == '=')
            {
                tokens.Add(expression.Substring(i, 2));
                i += 2;
                continue;
            }

            if ("()+-*/%^,<>\u003f:".Contains(c, StringComparison.Ordinal))
            {
                tokens.Add(c.ToString(CultureInfo.InvariantCulture));
                i++;
                continue;
            }

            throw new ExpressionException("InvalidExpression.InvalidCharacter", $"Invalid character '{c}'.", Args(("character", c)));
        }

        return tokens;
    }

    private static List<string> ToPostfix(IReadOnlyList<string> tokens)
    {
        var output = new List<string>();
        var operators = new Stack<string>();
        var previous = string.Empty;

        foreach (var token in tokens)
        {
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
                token is "t" or "fps" ||
                Constants.ContainsKey(token))
            {
                if (previous.Length > 0 &&
                    previous != "(" &&
                    previous != "," &&
                    previous != "?" &&
                    previous != ":" &&
                    !Precedence.ContainsKey(previous))
                {
                    throw new ExpressionException("InvalidExpression.MissingOperator", $"Missing operator before '{token}'.", Args(("token", token)));
                }

                output.Add(token);
            }
            else if (Functions.ContainsKey(token))
            {
                if (previous is not "" and not "(" and not "," and not "?" and not ":" && !Precedence.ContainsKey(previous))
                {
                    throw new ExpressionException("InvalidExpression.MissingOperatorBeforeFunction", $"Missing operator before function '{token}'.", Args(("token", token)));
                }

                operators.Push(token);
            }
            else switch (token)
            {
                case "," when previous.Length == 0 ||
                              previous == "(" ||
                              previous == "," ||
                              Precedence.ContainsKey(previous) ||
                              Functions.ContainsKey(previous) ||
                              previous == "?":
                    throw new ExpressionException("InvalidExpression.MisplacedComma", "Misplaced comma.");
                case ",":
                {
                    while (operators.Count > 0 && operators.Peek() != "(")
                    {
                        output.Add(operators.Pop());
                    }

                    if (operators.Count == 0)
                    {
                        throw new ExpressionException("InvalidExpression.MisplacedComma", "Misplaced comma.");
                    }

                    break;
                }
                case "(" when previous.Length > 0 &&
                              previous != "(" &&
                              previous != "," &&
                              previous != "?" &&
                              previous != ":" &&
                              !Precedence.ContainsKey(previous) &&
                              !Functions.ContainsKey(previous):
                    throw new ExpressionException("InvalidExpression.MissingOperatorBeforeParen", "Missing operator before '('.");
                case "(":
                    operators.Push(token);
                    break;
                case ")" when previous.Length == 0 ||
                              previous == "(" ||
                              previous == "," ||
                              Precedence.ContainsKey(previous) ||
                              Functions.ContainsKey(previous) ||
                              previous == "?":
                    throw new ExpressionException("InvalidExpression.MissingOperandBeforeParen", "Missing operand before ')'.");
                case ")":
                {
                    while (operators.Count > 0 && operators.Peek() != "(")
                    {
                        output.Add(operators.Pop());
                    }

                    if (operators.Count == 0)
                    {
                        throw new ExpressionException("InvalidExpression.UnbalancedParentheses", "Unbalanced parentheses.");
                    }

                    operators.Pop();
                    if (operators.Count > 0 && Functions.ContainsKey(operators.Peek()))
                    {
                        output.Add(operators.Pop());
                    }

                    break;
                }
                case "?" when previous.Length == 0 ||
                              previous == "(" ||
                              previous == "," ||
                              previous == "?" ||
                              Precedence.ContainsKey(previous) ||
                              Functions.ContainsKey(previous):
                    throw new ExpressionException("InvalidExpression.TernaryMissingCondition", "Operator '?' requires a condition.");
                case "?":
                {
                    while (operators.Count > 0 && Precedence.TryGetValue(operators.Peek(), out var lastPrecedence) &&
                           ShouldPopOperator(lastPrecedence, "?:"))
                    {
                        output.Add(operators.Pop());
                    }

                    operators.Push(token);
                    break;
                }
                case ":" when previous.Length == 0 ||
                              previous == "(" ||
                              previous == "," ||
                              previous == "?" ||
                              Precedence.ContainsKey(previous) ||
                              Functions.ContainsKey(previous):
                    throw new ExpressionException("InvalidExpression.TernaryMissingTrueExpression", "Operator ':' requires a true expression.");
                case ":":
                {
                    while (operators.Count > 0 && operators.Peek() != "?" && operators.Peek() != "(")
                    {
                        output.Add(operators.Pop());
                    }

                    if (operators.Count == 0 || operators.Peek() != "?")
                    {
                        throw new ExpressionException("InvalidExpression.TernaryUnmatchedColon", "Operator ':' requires a matching '?'.");
                    }

                    operators.Pop();

                    while (operators.Count > 0 && Precedence.TryGetValue(operators.Peek(), out var lastPrecedence) &&
                           ShouldPopOperator(lastPrecedence, "?:"))
                    {
                        output.Add(operators.Pop());
                    }

                    operators.Push("?:");
                    break;
                }
                default:
                {
                    if (Precedence.ContainsKey(token))
                    {
                        var isUnarySign = token is "-" or "+" &&
                                          (previous.Length == 0 || previous is "(" or "," or "?" or ":" || Precedence.ContainsKey(previous));
                        var operatorToken = isUnarySign ? $"u{token}" : token;

                        if (!isUnarySign &&
                            (previous.Length == 0 || previous == "(" || previous == "," || previous == "?" || Precedence.ContainsKey(previous)))
                        {
                            throw new ExpressionException("InvalidExpression.OperatorRequiresLeftOperand", $"Operator '{token}' requires a left operand.", Args(("token", token)));
                        }

                        while (operators.Count > 0 && Precedence.TryGetValue(operators.Peek(), out var lastPrecedence) &&
                               ShouldPopOperator(lastPrecedence, operatorToken))
                        {
                            output.Add(operators.Pop());
                        }

                        operators.Push(operatorToken);
                    }
                    else
                    {
                        throw new ExpressionException("InvalidExpression.UnsupportedToken", $"Unsupported token '{token}'.", Args(("token", token)));
                    }

                    break;
                }
            }

            previous = token;
        }

        if (previous == ",")
        {
            throw new ExpressionException("InvalidExpression.MisplacedComma", "Misplaced comma.");
        }

        if (previous == ":")
        {
            throw new ExpressionException("InvalidExpression.InsufficientOperands", "Operator ':' requires a false expression.", Args(("token", previous)));
        }

        if (Precedence.ContainsKey(previous))
        {
            throw new ExpressionException("InvalidExpression.InsufficientOperands", $"Token '{previous}' requires more operands.", Args(("token", previous)));
        }

        while (operators.Count > 0)
        {
            var token = operators.Pop();
            if (token is "(" or "?")
            {
                throw token == "("
                    ? new ExpressionException("InvalidExpression.UnbalancedParentheses", "Unbalanced parentheses.")
                    : new ExpressionException("InvalidExpression.TernaryUnmatchedQuestion", "Operator '?' requires a matching ':'.");
            }

            output.Add(token);
        }

        return output;
    }

    private static bool ShouldPopOperator(int previousPrecedence, string currentOperator)
    {
        return previousPrecedence > Precedence[currentOperator] ||
            (previousPrecedence == Precedence[currentOperator] && !RightAssociativeOperators.Contains(currentOperator));
    }

    private static void ApplyOperator(string op, Stack<decimal> stack)
    {
        switch (op)
        {
            case "u+" or "u-":
            {
                var value = Pop(stack, op);
                stack.Push(op == "u-" ? -value : value);
                return;
            }
            case "?:":
            {
                var falseValue = Pop(stack, op);
                var trueValue = Pop(stack, op);
                var condition = Pop(stack, op);
                stack.Push(condition == 0 ? falseValue : trueValue);
                return;
            }
        }

        var rhs = Pop(stack, op);
        var lhs = Pop(stack, op);
        stack.Push(op switch
        {
            "+" => lhs + rhs,
            "-" => lhs - rhs,
            "*" => lhs * rhs,
            "/" => lhs / rhs,
            "%" => lhs % rhs,
            "^" => (decimal)Math.Pow((double)lhs, (double)rhs),
            ">" => lhs > rhs ? 1 : 0,
            "<" => lhs < rhs ? 1 : 0,
            ">=" => lhs >= rhs ? 1 : 0,
            "<=" => lhs <= rhs ? 1 : 0,
            _ => throw new ExpressionException("InvalidExpression.UnsupportedOperator", $"Unsupported operator '{op}'.", Args(("token", op)))
        });
    }

    private static void ApplyFunction(string function, Stack<decimal> stack)
    {
        stack.Push(function switch
        {
            "abs" => Math.Abs(Pop(stack, function)),
            "acos" => Unary(Math.Acos),
            "asin" => Unary(Math.Asin),
            "atan" => Unary(Math.Atan),
            "atan2" => Binary(Math.Atan2),
            "cos" => Unary(Math.Cos),
            "sin" => Unary(Math.Sin),
            "tan" => Unary(Math.Tan),
            "cosh" => Unary(Math.Cosh),
            "sinh" => Unary(Math.Sinh),
            "tanh" => Unary(Math.Tanh),
            "exp" => Unary(Math.Exp),
            "log" => Unary(Math.Log),
            "log10" => Unary(Math.Log10),
            "sqrt" => Unary(Math.Sqrt),
            "ceil" => Math.Ceiling(Pop(stack, function)),
            "floor" => Math.Floor(Pop(stack, function)),
            "round" => Math.Round(Pop(stack, function)),
            "int" => Math.Truncate(Pop(stack, function)),
            "sign" => Math.Sign(Pop(stack, function)),
            "pow" => Binary(Math.Pow),
            "max" => Max(PopPair(stack, function)),
            "min" => Min(PopPair(stack, function)),
            _ => throw new ExpressionException("InvalidExpression.UnsupportedFunction", $"Unsupported function '{function}'.", Args(("function", function)))
        });
        return;

        decimal Binary(Func<double, double, double> func)
        {
            var rhs = Pop(stack, function);
            var lhs = Pop(stack, function);
            return (decimal)func((double)lhs, (double)rhs);
        }

        decimal Unary(Func<double, double> func) => (decimal)func((double)Pop(stack, function));
    }

    private static decimal Max((decimal Left, decimal Right) pair) => Math.Max(pair.Left, pair.Right);

    private static decimal Min((decimal Left, decimal Right) pair) => Math.Min(pair.Left, pair.Right);

    private static (decimal Left, decimal Right) PopPair(Stack<decimal> stack, string token)
    {
        var rhs = Pop(stack, token);
        var lhs = Pop(stack, token);
        return (lhs, rhs);
    }

    private static decimal Pop(Stack<decimal> stack, string token)
    {
        if (!stack.TryPop(out var value))
        {
            throw new ExpressionException("InvalidExpression.InsufficientOperands", $"Token '{token}' requires more operands.", Args(("token", token)));
        }

        return value;
    }
}
