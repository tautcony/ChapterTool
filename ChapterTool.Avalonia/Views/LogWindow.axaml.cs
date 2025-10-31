using Avalonia.Controls;

namespace ChapterTool.Avalonia.Views;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        
        // Set clipboard reference if ViewModel is available
        if (DataContext is ViewModels.LogWindowViewModel viewModel && Clipboard != null)
        {
            viewModel.SetClipboard(Clipboard);
        }
    }
}
