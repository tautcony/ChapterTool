using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

public enum ExpressionTokenKind
{
    Unknown,
    Number,
    Variable,
    Constant,
    Function,
    Operator,
    Punctuation,
    Comment
}

public sealed record ExpressionTokenSpan(
    int Start,
    int Length,
    string Text,
    ExpressionTokenKind Kind);

public sealed record ExpressionSymbol(
    string Text,
    ExpressionTokenKind Kind,
    string Description,
    int? Arity = null,
    string InsertText = "");

public sealed record ExpressionCompletion(
    string Text,
    ExpressionTokenKind Kind,
    string Description,
    int ReplacementStart,
    int ReplacementLength,
    string InsertText);

public sealed record ExpressionDiagnosticSuggestion(
    string Code,
    string Message);

public sealed record ExpressionAuthoringDiagnostic(
    ChapterDiagnostic Diagnostic,
    ExpressionDiagnosticSuggestion Suggestion,
    int Start,
    int Length);

public sealed record ExpressionAnalysisResult(
    IReadOnlyList<ExpressionTokenSpan> Spans,
    IReadOnlyList<ExpressionCompletion> Completions,
    IReadOnlyList<ExpressionAuthoringDiagnostic> Diagnostics);

public interface IExpressionAuthoringService
{
    IReadOnlyList<ExpressionSymbol> Symbols { get; }

    ExpressionAnalysisResult Analyze(string expression, int caretIndex, decimal timeSeconds = 0, decimal framesPerSecond = 24);
}
