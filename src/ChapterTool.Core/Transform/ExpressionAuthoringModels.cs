using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Transform;

/// <summary>
/// Identifies the kind of token recognized by expression authoring.
/// </summary>
public enum ExpressionTokenKind
{
    /// <summary>
    /// Identifies the Unknown value.
    /// </summary>
    Unknown,
    /// <summary>
    /// Identifies the Number value.
    /// </summary>
    Number,
    /// <summary>
    /// Identifies the Variable value.
    /// </summary>
    Variable,
    /// <summary>
    /// Identifies the Constant value.
    /// </summary>
    Constant,
    /// <summary>
    /// Identifies the Function value.
    /// </summary>
    Function,
    /// <summary>
    /// Identifies the Keyword value.
    /// </summary>
    Keyword,
    /// <summary>
    /// Identifies the Snippet value.
    /// </summary>
    Snippet,
    /// <summary>
    /// Identifies the String value.
    /// </summary>
    String,
    /// <summary>
    /// Identifies the Operator value.
    /// </summary>
    Operator,
    /// <summary>
    /// Identifies the Punctuation value.
    /// </summary>
    Punctuation,
    /// <summary>
    /// Identifies the Comment value.
    /// </summary>
    Comment
}

/// <summary>
/// Describes one token span in an expression.
/// </summary>
/// <param name="Start">The zero-based start position of the token in the expression text.</param>
/// <param name="Length">The token length in characters.</param>
/// <param name="Text">The token text.</param>
/// <param name="Kind">The token classification.</param>
public sealed record ExpressionTokenSpan(
    int Start,
    int Length,
    string Text,
    ExpressionTokenKind Kind);

/// <summary>
/// Describes an expression symbol available to authoring tools.
/// </summary>
/// <param name="Text">The symbol text shown in authoring tools.</param>
/// <param name="Kind">The symbol classification.</param>
/// <param name="Description">The symbol description shown to users.</param>
/// <param name="Arity">The number of arguments accepted by a function symbol, when applicable.</param>
/// <param name="InsertText">The text inserted when the symbol is selected.</param>
public sealed record ExpressionSymbol(
    string Text,
    ExpressionTokenKind Kind,
    string Description,
    int? Arity = null,
    string InsertText = "");

/// <summary>
/// Describes a completion item for expression authoring.
/// </summary>
/// <param name="Text">The completion text shown in the completion list.</param>
/// <param name="Kind">The completion classification.</param>
/// <param name="Description">The completion description shown to users.</param>
/// <param name="ReplacementStart">The zero-based start position of the text replaced by the completion.</param>
/// <param name="ReplacementLength">The number of characters replaced by the completion.</param>
/// <param name="InsertText">The text inserted when the completion is accepted.</param>
public sealed record ExpressionCompletion(
    string Text,
    ExpressionTokenKind Kind,
    string Description,
    int ReplacementStart,
    int ReplacementLength,
    string InsertText)
{
    /// <summary>
    /// Gets the KindLabel value.
    /// </summary>
    public string KindLabel => Kind switch
    {
        ExpressionTokenKind.Variable => "VAR",
        ExpressionTokenKind.Constant => "CONST",
        ExpressionTokenKind.Function => "FUNC",
        ExpressionTokenKind.Keyword => "KEY",
        ExpressionTokenKind.Snippet => "PRESET",
        ExpressionTokenKind.String => "STR",
        ExpressionTokenKind.Number => "NUM",
        _ => Kind.ToString().ToUpperInvariant()
    };
}


/// <summary>
/// Describes a suggested fix for an expression diagnostic.
/// </summary>
/// <param name="Code">The stable suggestion code.</param>
/// <param name="Message">The suggestion message shown to users.</param>
public sealed record ExpressionDiagnosticSuggestion(
    string Code,
    string Message);

/// <summary>
/// Describes an expression diagnostic and its source span.
/// </summary>
/// <param name="Diagnostic">The diagnostic raised for the expression.</param>
/// <param name="Suggestion">The suggested fix associated with the diagnostic.</param>
/// <param name="Start">The zero-based start position of the diagnostic span.</param>
/// <param name="Length">The diagnostic span length in characters.</param>
public sealed record ExpressionAuthoringDiagnostic(
    ChapterDiagnostic Diagnostic,
    ExpressionDiagnosticSuggestion Suggestion,
    int Start,
    int Length);

/// <summary>
/// Represents token, completion, and diagnostic analysis for an expression.
/// </summary>
/// <param name="Spans">The token spans recognized in the expression.</param>
/// <param name="Completions">The completion items available at the caret position.</param>
/// <param name="Diagnostics">Expression diagnostics and suggested fixes.</param>
public sealed record ExpressionAnalysisResult(
    IReadOnlyList<ExpressionTokenSpan> Spans,
    IReadOnlyList<ExpressionCompletion> Completions,
    IReadOnlyList<ExpressionAuthoringDiagnostic> Diagnostics);

/// <summary>
/// Defines expression authoring analysis operations.
/// </summary>
public interface IExpressionAuthoringService
{
    /// <summary>
    /// Gets symbols available to expression authoring.
    /// </summary>
    IReadOnlyList<ExpressionSymbol> Symbols { get; }

    /// <summary>
    /// Analyzes expression text for tokens, completions, and diagnostics.
    /// </summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="caretIndex">The caret index in the expression.</param>
    /// <param name="timeSeconds">The chapter time in seconds.</param>
    /// <param name="framesPerSecond">The frame rate in frames per second.</param>
    /// <returns>The expression analysis result.</returns>
    ExpressionAnalysisResult Analyze(string expression, int caretIndex, decimal timeSeconds = 0, decimal framesPerSecond = 24);
}
