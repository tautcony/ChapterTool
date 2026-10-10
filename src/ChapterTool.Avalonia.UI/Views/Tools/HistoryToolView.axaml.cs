using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;

namespace ChapterTool.Avalonia.UI.Views.Tools;

public partial class HistoryToolView : UserControl
{
    public HistoryToolView() => InitializeComponent();

    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (DataContext is HistoryToolViewModel viewModel)
        {
            viewModel.UpdateAvailableWidth(args.NewSize.Width);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender is TreeView tree && tree.SelectedItem is HistoryEntryViewModel entry)
        {
            if (DataContext is HistoryToolViewModel viewModel && viewModel.SelectedEntry?.Id != entry.Id)
            {
                viewModel.SelectedEntry = entry;
            }
        }
    }

    private void OnLocateCurrent(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not HistoryToolViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsNarrowLayout)
        {
            viewModel.ShowHistoryPage();
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var tree = this.FindControl<TreeView>(viewModel.IsNarrowLayout ? "NarrowHistoryEntries" : "HistoryEntries");
            var selectedId = viewModel.SelectedEntry?.Id;
            if (tree is null || selectedId is null)
            {
                return;
            }

            if (tree.GetVisualDescendants().OfType<TreeViewItem>()
                .FirstOrDefault(item => item.DataContext is HistoryEntryViewModel entry && entry.Id == selectedId) is { } selectedItem)
            {
                selectedItem.BringIntoView();
            }
        });
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not HistoryToolViewModel viewModel)
        {
            return;
        }

        if (args.Key == Key.Escape)
        {
            args.Handled = true;
            _ = viewModel.CloseCommand.ExecuteAsync();
            return;
        }

        var tree = viewModel.IsNarrowLayout ? this.FindControl<TreeView>("NarrowHistoryEntries") : this.FindControl<TreeView>("HistoryEntries");
        if (tree?.IsKeyboardFocusWithin != true)
        {
            return;
        }
        if (args.Key == Key.Enter)
        {
            if (viewModel.IsNarrowLayout)
            {
                viewModel.ShowDetailsPage();
            }
            this.FindControl<Control>(viewModel.IsNarrowLayout ? "NarrowHistoryDetails" : "HistoryDetails")?.Focus();
            args.Handled = true;
        }
    }
}
