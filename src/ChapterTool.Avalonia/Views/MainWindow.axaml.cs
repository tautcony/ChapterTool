using Avalonia.Controls;
using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Views;

/// <summary>Provides the desktop lifetime wrapper for the shared main view.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow(MainView mainView, string title)
    {
        InitializeComponent();
        Title = title;
        Width = 736;
        Height = 576;
        MinWidth = 608;
        MinHeight = 480;
        Content = mainView;
        DataContext = mainView.DataContext;
    }
}
