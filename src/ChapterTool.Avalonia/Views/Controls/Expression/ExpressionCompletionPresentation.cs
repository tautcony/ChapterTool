using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Views.Controls.Expression;

/// <summary>Maps completion categories to theme-aware brushes and icons.</summary>
public class ExpressionCompletionPresentation : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value is ExpressionTokenKind tokenKind ? tokenKind : ExpressionTokenKind.Unknown;
        return string.Equals(parameter?.ToString(), "icon", StringComparison.OrdinalIgnoreCase)
            ? Icon(kind)
            : ExpressionThemeBrushes.ForTokenKind(kind);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string Icon(ExpressionTokenKind kind) => kind switch
    {
        ExpressionTokenKind.Variable => "◈",
        ExpressionTokenKind.Constant => "◆",
        ExpressionTokenKind.Function => "ƒ",
        ExpressionTokenKind.Keyword => "K",
        ExpressionTokenKind.Snippet => "◇",
        ExpressionTokenKind.String => "S",
        ExpressionTokenKind.Number => "#",
        _ => "•"
    };
}
