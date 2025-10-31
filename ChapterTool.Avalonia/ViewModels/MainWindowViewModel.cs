using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChapterTool.Util;

namespace ChapterTool.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<ChapterViewModel> Chapters { get; } = new();

    [RelayCommand]
    private async Task LoadFile()
    {
        // TODO: Implement file loading with file dialog
        StatusMessage = "Loading file...";
        
        // Example of using Core library
        Logger.Log("File loading initiated from Avalonia UI");
        
        await Task.Delay(100); // Placeholder for async operation
        StatusMessage = "File loaded successfully";
    }

    [RelayCommand]
    private async Task ExportChapters()
    {
        // TODO: Implement chapter export functionality
        StatusMessage = "Exporting chapters...";
        await Task.Delay(100); // Placeholder for async operation
        StatusMessage = "Export completed";
    }
}

/// <summary>
/// ViewModel for a single chapter item in the list
/// </summary>
public partial class ChapterViewModel : ObservableObject
{
    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _timeString = "00:00:00.000";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _framesInfo = string.Empty;
}
