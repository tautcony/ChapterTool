using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Headless;

public sealed class SettingsToolHeadlessTests
{
    [AvaloniaFact]
    public async Task Appearance_tab_renders_color_pickers_for_theme_slots()
    {
        using var host = new MainWindowHeadlessTestHost();
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var view = new SettingsToolView { DataContext = viewModel };
        var window = new Window
        {
            Content = view,
            Width = 760,
            Height = 520
        };

        try
        {
            window.Show();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 3;
            var layoutManager = window.GetLayoutManager()
                ?? throw new InvalidOperationException("Settings window layout manager was not available.");
            layoutManager.ExecuteInitialLayoutPass();
            layoutManager.ExecuteLayoutPass();

            var colorPickers = window.GetVisualDescendants().OfType<ColorPicker>().ToArray();

            Assert.Equal(ThemeColorSettings.Default.OrderedSlots.Count, colorPickers.Length);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Settings_panel_renders_and_captures_screenshot_artifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var host = new MainWindowHeadlessTestHost();
        var appStore = new AppSettingsStore(root, [root]);
        var themeStore = new ThemeSettingsStore(root, [root]);
        var localizer = new AppLocalizationManager("en-US");
        var viewModel = new SettingsToolViewModel(host.ViewModel, appStore, themeStore, localizer);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var window = new Window();

        try
        {
            foreach (var (name, width, height) in new[]
            {
                ("default", 760d, 520d),
                ("wide", 1040d, 620d),
                ("narrow", 560d, 640d)
            })
            {
                window.Content = new SettingsToolView { DataContext = viewModel };
                window.Width = width;
                window.Height = height;
                window.Show();
                var layoutManager = window.GetLayoutManager()
                    ?? throw new InvalidOperationException("Settings window layout manager was not available.");
                layoutManager.ExecuteInitialLayoutPass();
                layoutManager.ExecuteLayoutPass();

                var artifactPath = Path.Combine(RepositoryRoot(), "artifacts", $"settings-panel-{name}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                var bitmap = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException($"Settings panel frame '{name}' was not rendered.");
                await using (var stream = File.Create(artifactPath))
                {
                    bitmap.Save(stream);
                }

                Assert.True(File.Exists(artifactPath));
                Assert.True(new FileInfo(artifactPath).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Directory.Delete(root, recursive: true);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.Avalonia.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
