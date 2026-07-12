using Avalonia.Media;
using ChapterTool.Avalonia.Views.Controls.Expression;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests.Views;

public sealed class ExpressionThemeBrushesTests
{
    [Fact]
    public void Token_kind_brushes_resolve_without_private_hard_coded_only_palette()
    {
        // When Application resources are unavailable (unit host), fallbacks still produce brushes.
        // Production colors resolve from ChapterTool.Expression.* theme resources in App.axaml.
        foreach (var kind in Enum.GetValues<ExpressionTokenKind>())
        {
            var brush = ExpressionThemeBrushes.ForTokenKind(kind);
            Assert.NotNull(brush);
            Assert.IsAssignableFrom<IBrush>(brush);
        }

        Assert.NotNull(ExpressionThemeBrushes.DiagnosticUnderline);
        Assert.NotNull(ExpressionThemeBrushes.EditorForeground);
    }

    [Fact]
    public void Completion_presentation_uses_theme_aware_brushes()
    {
        var presentation = new ExpressionCompletionPresentation();
        var brush = presentation.Convert(ExpressionTokenKind.Function, typeof(IBrush), parameter: null, culture: System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsAssignableFrom<IBrush>(brush);
        var icon = presentation.Convert(ExpressionTokenKind.Function, typeof(string), parameter: "icon", culture: System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("ƒ", icon);
    }
}
