using Avalonia.Controls;
using Avalonia.Input;
using ChapterTool.Avalonia.UI.ViewModels.Tools;

namespace ChapterTool.Avalonia.UI.Views.Tools;

public partial class HistoryToolView : UserControl
{
    public HistoryToolView() => InitializeComponent();

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || DataContext is not HistoryToolViewModel viewModel)
        {
            return;
        }

        args.Handled = true;
        _ = viewModel.CloseCommand.ExecuteAsync();
    }
}
