using Avalonia.Controls;
using ChapterTool.Avalonia.ViewModels;

namespace ChapterTool.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Set the window reference in the ViewModel after initialization
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetMainWindow(this);
        }
    }
}
