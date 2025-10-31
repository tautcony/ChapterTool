using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChapterTool.Util;

namespace ChapterTool.Avalonia.ViewModels;

public partial class LogWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private int _lineCount = 0;

    private IClipboard? _clipboard;

    public LogWindowViewModel()
    {
        RefreshLog();
        
        // Subscribe to log updates
        Logger.LogLineAdded += OnLogLineAdded;
    }

    public void SetClipboard(IClipboard clipboard)
    {
        _clipboard = clipboard;
    }

    private void OnLogLineAdded(string line, DateTime timestamp)
    {
        RefreshLog();
    }

    public void RefreshLog()
    {
        LogText = Logger.LogText;
        LineCount = LogText.Split('\n').Length;
    }

    [RelayCommand]
    private void ClearLog()
    {
        // Note: Logger doesn't have a clear method, so we just show empty
        LogText = "Log cleared (in-memory log still retained)";
        LineCount = 1;
    }

    [RelayCommand]
    private async Task CopyLog()
    {
        try
        {
            if (_clipboard != null)
            {
                await _clipboard.SetTextAsync(LogText);
            }
        }
        catch
        {
            // Silently fail if clipboard access is not available
        }
    }
}
