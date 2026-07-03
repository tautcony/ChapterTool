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
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            using var host = new MainWindowHeadlessTestHost(
                localizer: new AppLocalizationManager(culture),
                appSettings: new AppSettings(Language: culture));
            var viewModel = new SettingsToolViewModel(host.ViewModel, host.AppSettingsStore, host.ThemeSettingsStore, host.Localizer);
            await viewModel.LoadAsync(TestContext.Current.CancellationToken);

            foreach (var (name, width, height) in new[]
            {
                ("default", 760d, 520d),
                ("wide", 1040d, 620d),
                ("narrow", 560d, 640d)
            })
            {
                var window = new Window
                {
                    Content = new SettingsToolView { DataContext = viewModel },
                    Width = width,
                    Height = height
                };

                try
                {
                    window.Show();
                    var layoutManager = window.GetLayoutManager()
                        ?? throw new InvalidOperationException("Settings window layout manager was not available.");
                    layoutManager.ExecuteInitialLayoutPass();
                    layoutManager.ExecuteLayoutPass();
                    var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
                    Assert.Equal("Top", tabControl.TabStripPlacement.ToString());
                    if (culture == "en-US")
                    {
                        var tabHeaders = tabControl.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Where(block => block.Classes.Contains("tabHeader"))
                            .ToArray();
                        var generalWidth = tabHeaders.Single(block => block.Text == "General").Bounds.Width;
                        var externalToolsWidth = tabHeaders.Single(block => block.Text == "External Tools").Bounds.Width;
                        var outputDefaultsWidth = tabHeaders.Single(block => block.Text == "Output Defaults").Bounds.Width;
                        Assert.True(externalToolsWidth > generalWidth);
                        Assert.True(outputDefaultsWidth > externalToolsWidth);
                    }

                    Assert.All(
                        window.GetVisualDescendants()
                            .OfType<ScrollViewer>()
                            .Where(scrollViewer => scrollViewer.Classes.Contains("settingsPageScroller")),
                        scrollViewer => Assert.Equal("Disabled", scrollViewer.HorizontalScrollBarVisibility.ToString()));
                    tabControl.SelectedIndex = 2;
                    layoutManager.ExecuteLayoutPass();

                    var saveFormatCombo = window.GetVisualDescendants()
                        .OfType<ComboBox>()
                        .Single(comboBox => comboBox.Name == "DefaultSaveFormatCombo");
                    var xmlLanguageCombo = window.GetVisualDescendants()
                        .OfType<ComboBox>()
                        .Single(comboBox => comboBox.Name == "DefaultXmlLanguageCombo");
                    Assert.Equal(xmlLanguageCombo.Bounds.Width, saveFormatCombo.Bounds.Width, precision: 1);
                    Assert.True(saveFormatCombo.Bounds.Width <= 180.5);

                    var artifactPath = Path.Combine(RepositoryRoot(), "artifacts", $"settings-panel-{culture.ToLowerInvariant()}-{name}.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                    var bitmap = window.CaptureRenderedFrame()
                        ?? throw new InvalidOperationException($"Settings panel frame '{culture}-{name}' was not rendered.");
                    await using (var stream = File.Create(artifactPath))
                    {
                        bitmap.Save(stream);
                    }

                    Assert.True(File.Exists(artifactPath));
                    Assert.True(new FileInfo(artifactPath).Length > 0);
                }
                finally
                {
                    window.Close();
                }
            }
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
