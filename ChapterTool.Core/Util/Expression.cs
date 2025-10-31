// ****************************************************************************
//
// Copyright (C) 2014-2016 TautCony (TautCony@vcb-s.com)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************
namespace ChapterTool.Util
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    public class Expression
    {
        private IEnumerable<Token> PostExpression { get; set; }

        private bool EvalAble { get; set; } = true;

        public static Expression Empty
        {
            get
            {
                var ret = new Expression
                {
                    PostExpression = new List<Token> { new Token { TokenType = Token.Symbol.Variable, Value = "t" } },
                };
                return ret;
            }
        }

        private Expression()
        {
        }

        public Expression(string expr)
        {
            PostExpression = BuildPostExpressionStack(expr);
        }

        public Expression(IEnumerable<string> tokens)
        {
            PostExpression = tokens.TakeWhile(token => !token.StartsWith("//")).Where(token => !string.IsNullOrEmpty(token)).Reverse().Select(ToToken);
        }

        private static Token ToToken(string token)
        {
            var ret = new Token { Value = token, TokenType = Token.Symbol.Variable };
            if (token.Length == 1 && OperatorTokens.Contains(token.First()))
            {
                if (token == "(" || token == ")")
                    ret.TokenType = Token.Symbol.Bracket;
                else
                    ret.TokenType = Token.Symbol.Operator;
            }
            else if (FunctionTokens.ContainsKey(token))
            {
                ret.TokenType = Token.Symbol.Function;
            }
            else if (IsDigit(token.First()))
            {
                ret.TokenType = Token.Symbol.Number;
                ret.Number = decimal.Parse(token);
            }
            return ret;
        }

        public override string ToString()
        {
            return PostExpression.Aggregate(string.Empty, (word, token) => $"{token.Value} {word}").TrimEnd();
        }

        private static bool IsDigit(char c) => (c >= '0' && c <= '9') || c == '.';

        private static bool IsAlpha(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

        private static bool IsSpace(char c) => SpaceCharacter.Contains(c);

        private const string SpaceCharacter = " \t\n\v\f\r";

        private const string OperatorTokens = "\0(\0)\0+\0-\0*\0/\0%\0^\0,\0>\0<\0<=\0>=\0and\0or\0xor\0";

        private static readonly Dictionary<string, int> FunctionTokens = new Dictionary<string, int>
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
            ["rand"] = 0,
            ["dup"] = 0,
            ["int"] = 1,
            ["sign"] = 1,
            ["pow"] = 2,
            ["max"] = 2,
            ["min"] = 2,
        };

        private static readonly Dictionary<string, decimal> MathDefines = new Dictionary<string, decimal>
        {
            ["M_E"] = 2.71828182845904523536M, // e
            ["M_LOG2E"] = 1.44269504088896340736M, // log2(e)
            ["M_LOG10E"] = 0.43429448190325182765M, // log10(e)
            ["M_LN2"] = 0.69314718055994530942M, // ln(2)
            ["M_LN10"] = 2.30258509299404568402M, // ln(10)
            ["M_PI"] = 3.14159265358979323846M, // pi
            ["M_PI_2"] = 1.57079632679489661923M, // pi/2
            ["M_PI_4"] = 0.78539816339744830962M, // pi/4
            ["M_1_PI"] = 0.31830988618379067154M, // 1/pi
            ["M_2_PI"] = 0.63661977236758134308M, // 2/pi
            ["M_2_SQRTPI"] = 1.12837916709551257390M, // 2/sqrt(pi)
            ["M_SQRT2"] = 1.41421356237309504880M, // sqrt(2)
            ["M_SQRT1_2"] = 0.70710678118654752440M,  // 1/sqrt(2)
        };

        private static readonly Random Rnd = new Random();

        private static Token EvalCMath(Token func, Token value, Token value2 = null)
        {
            if (func.ParaCount == 2) return EvalCMathTwoToken(func, value, value2);
            if (!FunctionTokens.ContainsKey(func.Value))
                throw new Exception($"There is no function named {func.Value}");
            var ret = new Token { TokenType = Token.Symbol.Number };
            switch (func.Value)
            {
                case "abs": ret.Number = Math.Abs(value.Number); break;
                case "acos": ret.Number = (decimal)Math.Acos((double)value.Number); break;
                case "asin": ret.Number = (decimal)Math.Asin((double)value.Number); break;
                case "atan": ret.Number = (decimal)Math.Atan((double)value.Number); break;
                case "cos": ret.Number = (decimal)Math.Cos((double)value.Number); break;
                case "sin": ret.Number = (decimal)Math.Sin((double)value.Number); break;
                case "tan": ret.Number = (decimal)Math.Tan((double)value.Number); break;
                case "cosh": ret.Number = (decimal)Math.Cosh((double)value.Number); break;
                case "sinh": ret.Number = (decimal)Math.Sinh((double)value.Number); break;
                case "tanh": ret.Number = (decimal)Math.Tanh((double)value.Number); break;
                case "exp": ret.Number = (decimal)Math.Exp((double)value.Number); break;
                case "log": ret.Number = (decimal)Math.Log((double)value.Number); break;
                case "log10": ret.Number = (decimal)Math.Log10((double)value.Number); break;
                case "sqrt": ret.Number = (decimal)Math.Sqrt((double)value.Number); break;
                case "ceil": ret.Number = Math.Ceiling(value.Number); break;
                case "floor": ret.Number = Math.Floor(value.Number); break;
                case "round": ret.Number = Math.Round(value.Number); break;
                case "rand": ret.Number = (decimal)Rnd.NextDouble(); break;
                case "int": ret.Number = Math.Truncate(value.Number); break;
                case "sign": ret.Number = Math.Sign(value.Number); break;
            }
            return ret;
        }

        private static Token EvalCMathTwoToken(Token func, Token value, Token value2)
        {
            if (!FunctionTokens.ContainsKey(func.Value) && !OperatorTokens.Contains(func.Value))
                throw new Exception($"There is no function/operator named {func.Value}");
            var ret = new Token { TokenType = Token.Symbol.Number };
            if (value2 == null) throw new NullReferenceException(nameof(value2));
            switch (func.Value)
            {
                case "pow": ret.Number = (decimal)Math.Pow((double)value.Number, (double)value2.Number); break;
                case "max": ret.Number = Math.Max(value.Number, value2.Number); break;
                case "min": ret.Number = Math.Min(value.Number, value2.Number); break;
                case "atan2": ret.Number = (decimal)Math.Atan2((double)value.Number, (double)value2.Number); break;
                case "+": ret.Number = value.Number + value2.Number; break;
                case "-": ret.Number = value.Number - value2.Number; break;
                case "*": ret.Number = value.Number * value2.Number; break;
                case "/": ret.Number = value.Number / value2.Number; break;
                case "%": ret.Number = value.Number % value2.Number; break;
                case "^": ret.Number = (decimal)Math.Pow((double)value.Number, (double)value2.Number); break;
                case ">": ret.Number = value.Number > value2.Number ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case "<": ret.Number = value.Number < value2.Number ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case ">=": ret.Number = value.Number >= value2.Number ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case "<=": ret.Number = value.Number <= value2.Number ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case "and": ret.Number = (value.Number != 0M) && (value2.Number != 0M) ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case "or": ret.Number = (value.Number != 0M) || (value2.Number != 0M) ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
                case "xor": var t1 = value.Number != 0; var t2 = value.Number != 0; ret.Number = t1 ^ t2 ? 1 : 0; ret.TokenType = Token.Symbol.Boolean; break;
            }
            return ret;
        }

        private static Token GetToken(string expr, ref int pos)
        {
            var varRet = new StringBuilder();
            var i = pos;
            for (; i < expr.Length; i++)
            {
                if (IsSpace(expr[i]))
                {
                    if (varRet.Length != 0)
                        break;
                    continue;
                }
                if (IsDigit(expr[i]) || IsAlpha(expr[i]))
                {
                    varRet.Append(expr[i]);
                    continue;
                }

                if (varRet.Length != 0) break;

                if (!OperatorTokens.Contains(expr[i])) continue;

                pos = i + 1;
                if (pos < expr.Length && expr[pos] == '=' &&
                    (expr[pos - 1] == '>' || expr[pos - 1] == '<'))
                {
                    ++pos;
                    return new Token($"{expr[i]}=", Token.Symbol.Operator) { ParaCount = 2 };
                }
                switch (expr[i])
                {
                    case '(':
                    case ')':
                        return new Token($"{expr[i]}", Token.Symbol.Bracket);
                    case ',':
                        return new Token($"{expr[i]}", Token.Symbol.Comma);
                    default:
                        return new Token($"{expr[i]}", Token.Symbol.Operator) { ParaCount = 2 };
                }
            }
            pos = i;
            var variable = varRet.ToString();
            if (IsDigit(varRet[0]))
            {
                if (!decimal.TryParse(variable, out decimal number))
                    throw new Exception($"Invalid number token [{variable}]");
                return new Token(number) { Value = variable };
            }
            if (FunctionTokens.ContainsKey(variable))
                return new Token(variable, Token.Symbol.Function) { ParaCount = FunctionTokens[variable] };
            if (OperatorTokens.Contains($"\0{variable}\0"))
                return new Token(variable, Token.Symbol.Operator) { ParaCount = 2 };
            if (MathDefines.ContainsKey(variable))
                return new Token(MathDefines[variable]) { Value = variable };
            return new Token(variable, Token.Symbol.Variable);
        }

        private static int GetPriority(Token token)
        {
            var precedence = new Dictionary<string, int>
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
            };
            if (string.IsNullOrEmpty(token.Value) || token.TokenType == Token.Symbol.Blank) return -2;
            if (!precedence.ContainsKey(token.Value))
                throw new Exception($"Invalid operator [{token.Value}]");
            return precedence[token.Value];
        }

        public static IEnumerable<Token> BuildPostExpressionStack(string expr)
        {
            var retStack = new Stack<Token>();
            var stack = new Stack<Token>();
            var funcStack = new Stack<Token>();
            stack.Push(Token.End);
            var pos = 0;
            var preToken = Token.End;
            var comment = false;
            while (pos < expr.Length && !comment)
            {
                var token = GetToken(expr, ref pos);
                switch (token.TokenType)
                {
                    case Token.Symbol.Function:
                        funcStack.Push(token);
                        break;
                    case Token.Symbol.Comma:
                        while (stack.Peek().Value != "(")
                        {
                            retStack.Push(stack.Peek());
                            stack.Pop();
                        }
                        break;
                    case Token.Symbol.Bracket:
                        switch (token.Value)
                        {
                            case "(": stack.Push(token); break;
                            case ")":
                                while (stack.Peek().Value != "(")
                                {
                                    retStack.Push(stack.Peek());
                                    stack.Pop();
                                }
                                if (stack.Peek().Value == "(") stack.Pop();
                                if (funcStack.Count != 0)
                                {
                                    retStack.Push(funcStack.Peek());
                                    funcStack.Pop();
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException($"Invalid bracket token {token.Value}");
                        }
                        preToken = token;
                        break;

                    case Token.Symbol.Operator:
                        var lastToken = stack.Peek();
                        switch (lastToken.TokenType)
                        {
                            case Token.Symbol.Blank:
                            case Token.Symbol.Bracket:
                                if (preToken.Value == "(" && token.Value == "-")
                                    retStack.Push(Token.Zero);
                                stack.Push(token);
                                break;

                            case Token.Symbol.Operator:
                                if (token.Value == "/" && preToken.Value == "/")
                                {
                                    stack.Pop();
                                    comment = true;
                                    break;
                                }
                                if (token.Value == "-" && preToken.TokenType == Token.Symbol.Operator)
                                {
                                    retStack.Push(Token.Zero);
                                }
                                else
                                {
                                    while (lastToken.TokenType != Token.Symbol.Bracket &&
                                        GetPriority(lastToken) >= GetPriority(token))
                                    {
                                        retStack.Push(lastToken);
                                        stack.Pop();
                                        lastToken = stack.Peek();
                                    }
                                }

                                stack.Push(token);
                                break;
                            default:
                                throw new Exception($"Unexpected token type: {token.Value} => {token.TokenType}");
                        }
                        preToken = token;
                        break;
                    default:
                        preToken = token;
                        retStack.Push(token);
                        break;
                }
            }

            while (stack.Peek().Value != string.Empty)
            {
                retStack.Push(stack.Peek());
                stack.Pop();
            }
            return retStack;
        }

        public static decimal Eval(IEnumerable<Token> postfix, Dictionary<string, decimal> values)
        {
            var stack = new Stack<Token>();
            foreach (var token in postfix.Reverse())
            {
                switch (token.TokenType)
                {
                    case Token.Symbol.Number: stack.Push(token); break;
                    case Token.Symbol.Variable: stack.Push(new Token(values[token.Value])); break;
                    case Token.Symbol.Operator:
                        var rhs = stack.Peek(); stack.Pop();
                        var lhs = stack.Peek(); stack.Pop();
                        stack.Push(EvalCMath(token, lhs, rhs));
                        break;
                    case Token.Symbol.Function:
                        switch (token.ParaCount)
                        {
                            case 0:
                                switch (token.Value)
                                {
                                    case "rand": stack.Push(EvalCMath(token, Token.Zero)); break;
                                    case "dup": stack.Push(stack.Peek()); break;
                                }
                                break;
                            case 1:
                                var para = stack.Peek(); stack.Pop();
                                stack.Push(EvalCMath(token, para));
                                break;
                            case 2:
                                rhs = stack.Peek(); stack.Pop();
                                lhs = stack.Peek(); stack.Pop();
                                stack.Push(EvalCMath(token, lhs, rhs));
                                break;
                            case 3:
                                var expr2 = stack.Peek(); stack.Pop();
                                var expr1 = stack.Peek(); stack.Pop();
                                var condition = stack.Peek(); stack.Pop();
                                if (condition.TokenType == Token.Symbol.Boolean ||
                                    condition.TokenType == Token.Symbol.Number)
                                {
                                    stack.Push(condition.Number == 0 ? expr2 : expr1);
                                }
                                break;
                        }
                        break;
                }
            }
            return stack.Peek().Number;
        }

        public decimal Eval(Dictionary<string, decimal> values) => Eval(PostExpression, values);

        public decimal Eval(double time, decimal fps)
        {
            if (!EvalAble) return (decimal)time;
            try
            {
                if (fps < 1e-5M)
                {
                    return Eval(new Dictionary<string, decimal>
                    {
                        ["t"] = (decimal)time,
                    });
                }
                return Eval(new Dictionary<string, decimal>
                {
                    ["t"] = (decimal)time,
                    ["fps"] = fps,
                });
            }
            catch (Exception exception)
            {
                EvalAble = false;
                Console.WriteLine($@"Eval Failed: {exception.Message}");
                return (decimal)time;
            }
        }

        public decimal Eval()
        {
            if (!EvalAble) return 0;
            try
            {
                return Eval(new Dictionary<string, decimal>());
            }
            catch (Exception exception)
            {
                EvalAble = false;
                Console.WriteLine($@"Eval Failed: {exception.Message}");
                return 0;
            }
        }

        public static explicit operator decimal(Expression expr) => expr.Eval();

        private static string RemoveBrackets(string x)
        {
            if (x.First() == '(' && x.Last() == ')')
            {
                var p = 1;
                foreach (var c in x.Skip(1).Take(x.Length - 2))
                {
                    if (c == '(') ++p;
                    else if (c == ')') --p;
                    if (p == 0) break;
                }
                if (p == 1) return x.Substring(1, x.Length - 2);
            }
            return x;
        }

        public static string Postfix2Infix(string expr)
        {
            const string funcName = "Postfix2Infix";
            var op1 = new HashSet<string> { "exp", "log", "sqrt", "abs", "not", "dup" };
            var op2 = new HashSet<string> { "+", "-", "*", "/", "max", "min", ">", "<", "=", ">=", "<=", "and", "or", "xor", "swap", "pow" };
            var op3 = new HashSet<string> { "?" };

            var exprList = expr.Split();

            var stack = new Stack<string>();
            foreach (var item in exprList)
            {
                if (op1.Contains(item))
                {
                    string operand1;
                    try
                    {
                        operand1 = stack.Peek();
                        stack.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new Exception($"{funcName}: Invalid expression, require operands.");
                    }
                    if (item == "dup")
                    {
                        stack.Push(operand1);
                        stack.Push(operand1);
                    }
                    else
                    {
                        stack.Push($"{item}({RemoveBrackets(operand1)})");
                    }
                }
                else if (op2.Contains(item))
                {
                    string operand2, operand1;
                    try
                    {
                        operand2 = stack.Peek();
                        stack.Pop();
                        operand1 = stack.Peek();
                        stack.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new Exception($"{funcName}: Invalid expression, require operands.");
                    }
                    stack.Push($"({operand1} {item} {operand2})");
                }
                else if (op3.Contains(item))
                {
                    string operand3, operand2, operand1;
                    try
                    {
                        operand3 = stack.Peek();
                        stack.Pop();
                        operand2 = stack.Peek();
                        stack.Pop();
                        operand1 = stack.Peek();
                        stack.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                        throw new Exception($"{funcName}: Invalid expression, require operands.");
                    }
                    stack.Push($"({operand1} {item} {operand2} {":"} {operand3})");
                }
                else
                {
                    stack.Push(item);
                }
            }

            if (stack.Count > 1)
                throw new Exception($"{funcName}: Invalid expression, require operators.");
            return RemoveBrackets(stack.Peek());
        }

        public class Token
        {
            public string Value { get; set; } = string.Empty;

            public Symbol TokenType { get; set; } = Symbol.Blank;

            public decimal Number { get; set; }

            public int ParaCount { get; set; }

            public static Token End => new Token(string.Empty, Symbol.Blank);

            public static Token Zero => new Token("0", Symbol.Number);

            public Token()
            {
            }

            public Token(string value, Symbol type)
            {
                Value = value;
                TokenType = type;
            }

            public Token(decimal number)
            {
                Number = number;
                TokenType = Symbol.Number;
            }

            public enum Symbol
            {
                Blank,
                Number,
                Variable,
                Operator,
                Bracket,
                Function,
                Comma,
                Boolean,
            }

            public override string ToString()
            {
                if (TokenType == Symbol.Boolean)
                    return Number == 0 ? "False" : "True";
                if (TokenType == Symbol.Number)
                    return $"{TokenType} [{Number}]";
                return $"{TokenType} [{Value}]";
            }
        }
    }
}
