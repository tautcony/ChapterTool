using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using Lua;
using Lua.Standard;

namespace ChapterTool.Core.Transform.Expressions.Lua;

/// <summary>
/// Evaluates Lua scripts against chapter expression contexts.
/// </summary>
public sealed partial class LuaExpressionScriptService : IChapterExpressionEngine
{
    private static readonly Regex ReturnOrTransformPattern = ReturnOrTransformRegex();
    private static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan executionTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="LuaExpressionScriptService"/> class.
    /// </summary>
    public LuaExpressionScriptService()
        : this(DefaultExecutionTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LuaExpressionScriptService"/> class.
    /// </summary>
    /// <param name="executionTimeout">The maximum time allowed for one Lua evaluation.</param>
    public LuaExpressionScriptService(TimeSpan executionTimeout)
    {
        this.executionTimeout = executionTimeout <= TimeSpan.Zero
            ? DefaultExecutionTimeout
            : executionTimeout;
    }

    /// <summary>
    /// Gets the stable language or engine identifier.
    /// </summary>
    public string EngineId => "lua";

    /// <summary>
    /// Gets the built-in Lua expression presets.
    /// </summary>
    public IReadOnlyList<ChapterExpressionPreset> Presets { get; } =
    [
        new(
            "identity",
            "Identity",
            "Keep chapter times unchanged.",
            "t"),
        new(
            "offset-seconds",
            "Offset seconds",
            "Add a fixed number of seconds to every chapter time; edit offset_seconds as needed.",
            "local offset_seconds = 1\nreturn t + offset_seconds"),
        new(
            "round-to-frame",
            "Round to nearest frame",
            "Snap chapter time to the nearest frame for the current frame rate.",
            "return math.floor(t * fps + 0.5) / fps"),
        new(
            "half-frame-earlier",
            "Half frame earlier",
            "Move chapter time half a frame earlier for the current frame rate.",
            "return t - (0.5 / fps)")
    ];

    /// <summary>
    /// Evaluates Lua script text against a chapter expression context.
    /// </summary>
    /// <param name="sourceText">The Lua script text.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>The expression evaluation result.</returns>
    public ChapterExpressionEvaluationResult Evaluate(string sourceText, ChapterExpressionContext context)
    {
        var fallback = context.TimeSeconds;
        try
        {
            var source = NormalizeSource(sourceText);
            using var state = LuaState.Create();
            using var timeout = new CancellationTokenSource(executionTimeout);
            ConfigureState(state, context);

            var results = state.DoStringAsync(source, "chapter-expression.lua", timeout.Token)
                .GetAwaiter()
                .GetResult();

            if (results.Length > 0)
            {
                return NumericResult(results[0], fallback);
            }

            var transform = state.Environment["transform"];
            if (transform.TryRead<LuaFunction>(out _))
            {
                var callResults = state.CallAsync(transform, [state.Environment["chapter"]], timeout.Token)
                    .GetAwaiter()
                    .GetResult();
                return callResults.Length == 0
                    ? Failure(fallback, "InvalidExpression.LuaMissingReturn", "Lua transform did not return a value.")
                    : NumericResult(callResults[0], fallback);
            }

            return Failure(fallback, "InvalidExpression.LuaMissingReturn", "Lua expression did not return a value.");
        }
        catch (LuaCompileException exception)
        {
            return Failure(fallback, "InvalidExpression.LuaCompile", exception.Message);
        }
        catch (LuaRuntimeException exception)
        {
            return Failure(fallback, "InvalidExpression.LuaRuntime", exception.Message);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(fallback, "InvalidExpression.LuaCanceled", exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OverflowException)
        {
            return Failure(fallback, "InvalidExpression.Lua", exception.Message);
        }
    }

    private static string NormalizeSource(string sourceText)
    {
        var source = string.IsNullOrWhiteSpace(sourceText) ? "t" : sourceText.Trim();
        return ReturnOrTransformPattern.IsMatch(source) ? source : $"return ({source})";
    }

    private static void ConfigureState(LuaState state, ChapterExpressionContext context)
    {
        state.OpenMathLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();

        state.Environment["t"] = (double)context.TimeSeconds;
        state.Environment["fps"] = (double)context.FramesPerSecond;
        state.Environment["index"] = context.Index;
        state.Environment["count"] = context.Count;

        var chapter = new LuaTable
        {
            ["number"] = context.Chapter.DisplayNumber,
            ["time"] = (double)context.TimeSeconds,
            ["name"] = context.Chapter.Name,
            ["frames"] = context.Chapter.FramesInfo,
            ["index"] = context.Index,
            ["count"] = context.Count,
            ["fps"] = (double)context.FramesPerSecond
        };
        state.Environment["chapter"] = chapter;

        if (state.Environment["math"].TryRead<LuaTable>(out var math))
        {
            foreach (var name in new[] { "abs", "acos", "asin", "atan", "ceil", "cos", "exp", "floor", "log", "max", "min", "pow", "sin", "sqrt", "tan" })
            {
                if (math.TryGetValue(name, out var value))
                {
                    state.Environment[name] = value;
                }
            }
        }

        state.Environment["round"] = new LuaFunction((luaContext, _) =>
        {
            var value = luaContext.GetArgument<double>(0);
            return new ValueTask<int>(luaContext.Return(Math.Round(value)));
        });
        state.Environment["sign"] = new LuaFunction((luaContext, _) =>
        {
            var value = luaContext.GetArgument<double>(0);
            return new ValueTask<int>(luaContext.Return(Math.Sign(value)));
        });
    }

    private static ChapterExpressionEvaluationResult NumericResult(LuaValue value, decimal fallback)
    {
        if (!value.TryRead<double>(out var number) || double.IsNaN(number) || double.IsInfinity(number))
        {
            return Failure(fallback, "InvalidExpression.LuaInvalidReturn", $"Lua expression returned {value.TypeToString()} instead of a finite number.");
        }

        try
        {
            return new ChapterExpressionEvaluationResult(true, (decimal)number, []);
        }
        catch (OverflowException exception)
        {
            return Failure(fallback, "InvalidExpression.LuaInvalidReturn", exception.Message);
        }
    }

    private static ChapterExpressionEvaluationResult Failure(decimal fallback, string code, string message) =>
        new(
            false,
            fallback,
            [new ChapterDiagnostic(
                DiagnosticSeverity.Warning,
                code,
                message,
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = message })]);

    [GeneratedRegex(@"\breturn\b|\bfunction\s+transform\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnOrTransformRegex();
}
