using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChapterTool.Util;
using ChapterTool.Util.ChapterData;
using ChapterTool.Avalonia.Views;

namespace ChapterTool.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _windowTitle = "ChapterTool - Modern Edition";

    [ObservableProperty]
    private bool _autoGenName = false;

    [ObservableProperty]
    private int _selectedExportFormat = 0;

    [ObservableProperty]
    private string _expressionText = "x";

    public ObservableCollection<ChapterViewModel> Chapters { get; } = new();
    public ObservableCollection<string> ExportFormats { get; } = new()
    {
        "OGM Text (.txt)",
        "Matroska XML (.xml)",
        "QPFile (.qpf)",
        "JSON (.json)",
        "CUE Sheet (.cue)"
    };

    private ChapterInfo? _currentChapterInfo;
    private Window? _mainWindow;

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    [RelayCommand]
    private async Task LoadFile()
    {
        if (_mainWindow == null) return;

        try
        {
            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Open Chapter File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("All Supported Files")
                    {
                        Patterns = new[] { "*.mpls", "*.xml", "*.txt", "*.ifo", "*.mkv", "*.mka", 
                                          "*.tak", "*.flac", "*.cue", "*.xpl", "*.mp4", "*.m4a", 
                                          "*.m4v", "*.vtt" }
                    },
                    new FilePickerFileType("Blu-ray Playlist") { Patterns = new[] { "*.mpls" } },
                    new FilePickerFileType("XML Chapter") { Patterns = new[] { "*.xml" } },
                    new FilePickerFileType("OGM Text") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("DVD IFO") { Patterns = new[] { "*.ifo" } },
                    new FilePickerFileType("Matroska") { Patterns = new[] { "*.mkv", "*.mka" } },
                    new FilePickerFileType("Audio with CUE") { Patterns = new[] { "*.tak", "*.flac", "*.cue" } },
                    new FilePickerFileType("HD DVD XPL") { Patterns = new[] { "*.xpl" } },
                    new FilePickerFileType("MP4 Files") { Patterns = new[] { "*.mp4", "*.m4a", "*.m4v" } },
                    new FilePickerFileType("WebVTT") { Patterns = new[] { "*.vtt" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var files = await _mainWindow.StorageProvider.OpenFilePickerAsync(filePickerOptions);
            if (files == null || files.Count == 0) return;

            var file = files[0];
            FilePath = file.Path.LocalPath;
            
            StatusMessage = "Loading file...";
            Logger.Log($"Loading file: {FilePath}");

            await LoadChapterFile(FilePath);
            
            StatusMessage = $"Loaded: {Path.GetFileName(FilePath)} - {Chapters.Count} chapters";
            WindowTitle = $"ChapterTool - {Path.GetFileName(FilePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Logger.Log($"Error loading file: {ex.Message}");
        }
    }

    private async Task LoadChapterFile(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                var extension = Path.GetExtension(filePath)?.ToLowerInvariant().TrimStart('.');
                ChapterInfo? chapterInfo = null;

                switch (extension)
                {
                    case "mpls":
                        var mplsData = new MplsData(filePath);
                        var mplsChapters = mplsData.GetChapters();
                        chapterInfo = mplsChapters.FirstOrDefault();
                        break;
                        
                    case "xml":
                        var xmlDoc = new XmlDocument();
                        xmlDoc.Load(filePath);
                        chapterInfo = XmlData.ParseXml(xmlDoc).FirstOrDefault();
                        break;
                        
                    case "txt":
                        var txtContent = File.ReadAllBytes(filePath).GetUTFString();
                        chapterInfo = OgmData.GetChapterInfo(txtContent ?? string.Empty);
                        break;
                        
                    case "ifo":
                        chapterInfo = IfoData.GetStreams(filePath).FirstOrDefault();
                        break;
                        
                    case "mkv":
                    case "mka":
                        var mkvData = new MatroskaData();
                        var mkvXml = mkvData.GetXml(filePath);
                        chapterInfo = XmlData.ParseXml(mkvXml).FirstOrDefault();
                        break;
                        
                    case "cue":
                    case "tak":
                    case "flac":
                        var cueSheet = new CueData(filePath, Logger.Log);
                        chapterInfo = cueSheet.Chapter;
                        break;
                        
                    case "xpl":
                        chapterInfo = XplData.GetStreams(filePath).FirstOrDefault();
                        break;
                        
                    case "mp4":
                    case "m4a":
                    case "m4v":
                        var mp4Data = new Mp4Data(filePath);
                        chapterInfo = mp4Data.Chapter;
                        break;
                        
                    case "vtt":
                        var vttContent = File.ReadAllBytes(filePath).GetUTFString();
                        chapterInfo = VTTData.GetChapterInfo(vttContent ?? string.Empty);
                        break;
                        
                    default:
                        throw new Exception($"Unsupported file format: {extension}");
                }

                if (chapterInfo != null && chapterInfo.Chapters.Count > 0)
                {
                    _currentChapterInfo = chapterInfo;
                    UpdateChapterDisplay();
                }
                else
                {
                    throw new Exception("No chapters found in file");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing file: {ex.Message}");
                throw;
            }
        });
    }

    private void UpdateChapterDisplay()
    {
        Chapters.Clear();
        
        if (_currentChapterInfo == null) return;

        var nameGenerator = ChapterName.GetChapterName();
        int index = 1;

        foreach (var chapter in _currentChapterInfo.Chapters.Where(c => c.Time != TimeSpan.MinValue))
        {
            Chapters.Add(new ChapterViewModel
            {
                Number = chapter.Number > 0 ? chapter.Number : index,
                Time = chapter.Time,
                TimeString = chapter.Time2String(_currentChapterInfo),
                Name = AutoGenName ? nameGenerator() : chapter.Name,
                FramesInfo = chapter.FramesInfo
            });
            index++;
        }
    }

    [RelayCommand]
    private async Task ExportChapters()
    {
        if (_mainWindow == null || _currentChapterInfo == null)
        {
            StatusMessage = "No chapters loaded";
            return;
        }

        try
        {
            var suggestedName = Path.GetFileNameWithoutExtension(FilePath);
            var extension = SelectedExportFormat switch
            {
                0 => ".txt",
                1 => ".xml",
                2 => ".qpf",
                3 => ".json",
                4 => ".cue",
                _ => ".txt"
            };

            var filePickerOptions = new FilePickerSaveOptions
            {
                Title = "Save Chapter File",
                SuggestedFileName = $"{suggestedName}_chapters{extension}",
                DefaultExtension = extension.TrimStart('.'),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(ExportFormats[SelectedExportFormat])
                    {
                        Patterns = new[] { $"*{extension}" }
                    }
                }
            };

            var file = await _mainWindow.StorageProvider.SaveFilePickerAsync(filePickerOptions);
            if (file == null) return;

            var savePath = file.Path.LocalPath;
            StatusMessage = "Exporting chapters...";
            Logger.Log($"Exporting to: {savePath}");

            await Task.Run(() =>
            {
                switch (SelectedExportFormat)
                {
                    case 0: // OGM Text
                        var text = _currentChapterInfo.GetText(AutoGenName);
                        File.WriteAllText(savePath, text, new System.Text.UTF8Encoding(true));
                        break;
                    case 1: // XML
                        _currentChapterInfo.SaveXml(savePath, "und", AutoGenName);
                        break;
                    case 2: // QPFile
                        var qpfile = _currentChapterInfo.GetQpfile();
                        File.WriteAllLines(savePath, qpfile);
                        break;
                    case 3: // JSON
                        var json = _currentChapterInfo.GetJson(AutoGenName);
                        File.WriteAllText(savePath, json.ToString());
                        break;
                    case 4: // CUE
                        var cue = _currentChapterInfo.GetCue(Path.GetFileName(FilePath), AutoGenName);
                        File.WriteAllText(savePath, cue.ToString(), new System.Text.UTF8Encoding(false));
                        break;
                }
            });

            StatusMessage = $"Exported to: {Path.GetFileName(savePath)}";
            Logger.Log($"Export completed successfully");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            Logger.Log($"Export error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplyExpression()
    {
        if (_currentChapterInfo == null || string.IsNullOrWhiteSpace(ExpressionText))
        {
            StatusMessage = "No expression to apply";
            return;
        }

        try
        {
            _currentChapterInfo.Expr = new Expression(ExpressionText);
            UpdateChapterDisplay();
            StatusMessage = $"Expression applied: {ExpressionText}";
            Logger.Log($"Applied expression: {ExpressionText}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Expression error: {ex.Message}";
            Logger.Log($"Expression error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowLog()
    {
        // Open log viewer window
        var logWindow = new LogWindow
        {
            DataContext = new LogWindowViewModel()
        };
        logWindow.Show();
        StatusMessage = "Log viewer opened";
    }

    [RelayCommand]
    private void ShowAbout()
    {
        // Open about dialog
        if (_mainWindow != null)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog(_mainWindow);
        }
        StatusMessage = "ChapterTool - Modern Edition | .NET 8 + Avalonia UI";
    }

    public async void HandleFileDrop(string[] files)
    {
        if (files == null || files.Length == 0) return;

        var filePath = files[0]; // Take first file
        FilePath = filePath;
        
        StatusMessage = "Loading dropped file...";
        Logger.Log($"File dropped: {filePath}");

        try
        {
            await LoadChapterFile(filePath);
            StatusMessage = $"Loaded: {Path.GetFileName(filePath)} - {Chapters.Count} chapters";
            WindowTitle = $"ChapterTool - {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Logger.Log($"Error loading dropped file: {ex.Message}");
        }
    }

    partial void OnAutoGenNameChanged(bool value)
    {
        UpdateChapterDisplay();
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
    private TimeSpan _time;

    [ObservableProperty]
    private string _timeString = "00:00:00.000";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _framesInfo = string.Empty;
}
