using Avalonia.Controls;
using Avalonia.Input;
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

        // Enable drag and drop
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        // Only allow files
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var filePaths = new System.Collections.Generic.List<string>();
                foreach (var file in files)
                {
                    filePaths.Add(file.Path.LocalPath);
                }

                if (DataContext is MainWindowViewModel viewModel && filePaths.Count > 0)
                {
                    viewModel.HandleFileDrop(filePaths.ToArray());
                }
            }
        }
    }
}

