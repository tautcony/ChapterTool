using Avalonia;
using Avalonia.Media;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls.Expression;

/// <summary>Resolves expression category/chrome brushes from application theme resources.</summary>
public static class ExpressionThemeBrushes
{
    public static IBrush Resolve(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var value) == true
            && value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    public static IBrush ForTokenKind(ExpressionTokenKind kind) => kind switch
    {
        ExpressionTokenKind.Variable => Resolve("ChapterTool.Expression.VariableBrush", "#0550ae"),
        ExpressionTokenKind.Constant => Resolve("ChapterTool.Expression.ConstantBrush", "#8250df"),
        ExpressionTokenKind.Function => Resolve("ChapterTool.Expression.FunctionBrush", "#953800"),
        ExpressionTokenKind.Keyword => Resolve("ChapterTool.Expression.KeywordBrush", "#cf222e"),
        ExpressionTokenKind.Snippet => Resolve("ChapterTool.Expression.SnippetBrush", "#6f42c1"),
        ExpressionTokenKind.String => Resolve("ChapterTool.Expression.StringBrush", "#0a3069"),
        ExpressionTokenKind.Operator => Resolve("ChapterTool.Expression.OperatorBrush", "#cf222e"),
        ExpressionTokenKind.Punctuation => Resolve("ChapterTool.Expression.PunctuationBrush", "#57606a"),
        ExpressionTokenKind.Number => Resolve("ChapterTool.Expression.NumberBrush", "#116329"),
        ExpressionTokenKind.Comment => Resolve("ChapterTool.Expression.CommentBrush", "#6e7781"),
        ExpressionTokenKind.Unknown => Resolve("ChapterTool.Expression.UnknownBrush", "#b42318"),
        _ => Resolve("ChapterTool.ControlForegroundBrush", "#24292f")
    };

    public static IBrush DiagnosticUnderline =>
        Resolve("ChapterTool.DiagnosticErrorBrush", "#cf222e");

    public static IBrush EditorForeground =>
        Resolve("ChapterTool.ControlForegroundBrush", "#24292f");
}
