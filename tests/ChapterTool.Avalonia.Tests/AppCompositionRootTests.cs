using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Services;
using System.Text;

namespace ChapterTool.Avalonia.Tests;

public sealed class AppCompositionRootTests
{
    [Fact]
    public void ResolvesPrimaryViewModelsAndServices()
    {
        var root = new AppCompositionRoot(settingsDirectory: SettingsDirectory());

        Assert.IsType<MainWindowViewModel>(root.CreateMainWindowViewModel());
        Assert.IsAssignableFrom<IWindowService>(root.CreateWindowService());
        Assert.IsAssignableFrom<IChapterLoadService>(root.CreateChapterLoadService());
        Assert.IsAssignableFrom<IChapterSaveService>(root.CreateChapterSaveService());
        Assert.IsAssignableFrom<IChapterImporterRegistry>(root.CreateChapterImporterRegistry());
    }

    [Fact]
    public void StartupAndMainWindowDelegateCompositionToRoot()
    {
        var app = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "App.axaml.cs"), Encoding.UTF8);
        var mainWindow = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("new AppCompositionRoot(startupPath)", app, StringComparison.Ordinal);
        Assert.Contains("composition.CreateMainWindow()", app, StringComparison.Ordinal);
        Assert.DoesNotContain("new MainWindow(startupPath)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsStore", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ThemeSettingsStore", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeChapterLoadService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeChapterSaveService", mainWindow, StringComparison.Ordinal);
    }

    private static string SettingsDirectory() =>
        Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Time_Shift.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
