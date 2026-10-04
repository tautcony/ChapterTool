using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.ViewModels.Tools;

namespace ChapterTool.Avalonia.UI.Views.Tools;

/// <summary>Provides the expression tool view.</summary>
public sealed partial class ExpressionToolView : UserControl
{
    public ExpressionToolView()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            foreach (var grid in this.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Classes.Contains("comparisonRow")))
            {
                ArrangeComparison(grid);
            }
        };
    }

    private void OnComparisonAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Grid grid)
        {
            ArrangeComparison(grid);
        }
    }

    private void ArrangeComparison(Grid grid)
    {
        var compact = Bounds.Width < 640;
        grid.ColumnDefinitions = new ColumnDefinitions(compact ? "*,*" : "1.1*,*,*,*");
        grid.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
        foreach (var child in grid.Children)
        {
            var (row, column, span) = child.Classes switch
            {
                var classes when classes.Contains("beforeValue") => compact ? (1, 0, 1) : (0, 1, 1),
                var classes when classes.Contains("afterValue") => compact ? (1, 1, 1) : (0, 2, 1),
                var classes when classes.Contains("deltaValue") => compact ? (2, 0, 2) : (0, 3, 1),
                var classes when classes.Contains("frameDetails") => compact ? (3, 0, 2) : (1, 0, 4),
                _ => (0, 0, compact ? 2 : 1)
            };
            Grid.SetColumn(child, column);
            Grid.SetRow(child, row);
            Grid.SetColumnSpan(child, span);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || args.Key != Key.Escape || DataContext is not ExpressionToolViewModel viewModel || viewModel.IsApplying)
        {
            return;
        }

        args.Handled = true;
        _ = viewModel.CancelPreviewCommand.ExecuteAsync();
    }
}
