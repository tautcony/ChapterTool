using Avalonia.Controls;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Views;

/// <summary>Provides the desktop lifetime wrapper for the shared main view.</summary>
public sealed partial class MainWindow : Window
{
    private bool closeConfirmed;
    private bool closeConfirmationPending;

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
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (closeConfirmed || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        args.Cancel = true;
        if (closeConfirmationPending)
        {
            return;
        }

        closeConfirmationPending = true;
        try
        {
            if (!viewModel.RequiresSessionLossConfirmation
                || await viewModel.ConfirmSessionLossAsync(CancellationToken.None))
            {
                viewModel.EndDocumentSession();
                closeConfirmed = true;
                Close();
            }
        }
        finally
        {
            closeConfirmationPending = false;
        }
    }
}
