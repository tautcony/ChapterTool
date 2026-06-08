using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using ChapterTool.Avalonia.ViewModels;

namespace ChapterTool.Avalonia.Views.Tools;

public sealed partial class TextToolView : UserControl
{
    public TextToolView()
    {
        InitializeComponent();
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not TextToolViewModel viewModel)
        {
            return;
        }

        var window = TopLevel.GetTopLevel(this);
        if (window?.Clipboard is not null)
        {
            await window.Clipboard.SetTextAsync(viewModel.Text);
        }
    }
}
