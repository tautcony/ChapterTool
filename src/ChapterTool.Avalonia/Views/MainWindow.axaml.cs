using Avalonia.Controls;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Views;

/// <summary>Provides the desktop lifetime wrapper for the shared main view.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainView mainView, string title)
        : this()
    {
        Title = title;
        Width = 760;
        Height = 600;
        MinWidth = 760;
        MinHeight = 520;
        Content = mainView;
        DataContext = mainView.DataContext;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.EndDocumentSession();
        }
    }
}
