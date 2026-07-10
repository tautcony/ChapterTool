using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class SettingsToolHeadlessTests
{
    [AvaloniaFact]
    public async Task Xml_language_selection_remains_visible_after_runtime_language_switch()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var host = new MainWindowHeadlessTestHost(
            localizer: localizer,
            appSettings: new AppSettings(Language: "en-US", DefaultXmlLanguage: "jpn"));
        var viewModel = new SettingsToolViewModel(host.ViewModel, host.AppSettingsStore, host.ThemeSettingsStore, host.Localizer, autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var window = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 760,
            Height = 520
        };

        try
        {
            window.Show();
            var layoutManager = window.GetLayoutManager()
                ?? throw new InvalidOperationException("Settings window layout manager was not available.");
            layoutManager.ExecuteInitialLayoutPass();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 2;
            layoutManager.ExecuteLayoutPass();

            var xmlLanguageCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => comboBox.Name == "DefaultXmlLanguageCombo");
            Assert.Equal("jpn（Japanese）", xmlLanguageCombo.SelectionBoxItem?.ToString());

            localizer.SetCulture("zh-CN");
            layoutManager.ExecuteLayoutPass();

            Assert.Equal(viewModel.DefaultXmlLanguageIndex, xmlLanguageCombo.SelectedIndex);
            Assert.False(string.IsNullOrWhiteSpace(xmlLanguageCombo.SelectionBoxItem?.ToString()));
            Assert.StartsWith("jpn（", xmlLanguageCombo.SelectionBoxItem?.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Icon_only_settings_buttons_have_accessible_names()
    {
        using var host = new MainWindowHeadlessTestHost();
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer,
            autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var window = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 760,
            Height = 520
        };

        try
        {
            window.Show();
            var layoutManager = window.GetLayoutManager()
                ?? throw new InvalidOperationException("Settings window layout manager was not available.");
            layoutManager.ExecuteInitialLayoutPass();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 1;
            layoutManager.ExecuteLayoutPass();

            var iconButtons = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("compact"))
                .ToArray();

            Assert.NotEmpty(iconButtons);
            Assert.All(iconButtons, button => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Preset_selection_updates_preview_runtime_theme_and_existing_grid_headers()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();
        var themeService = new AvaloniaThemeApplicationService();
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer,
            themeApplicationService: themeService,
            autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var settingsWindow = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 760,
            Height = 520
        };

        try
        {
            settingsWindow.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);
            var tabControl = settingsWindow.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 3;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);
            var combo = settingsWindow.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "ThemePresetCombo");
            var preview = settingsWindow.GetVisualDescendants().OfType<ItemsControl>().Single(control => control.Name == "ThemePalettePreview");
            var headers = host.Window.GetVisualDescendants().OfType<DataGridColumnHeader>().ToArray();
            Assert.NotEmpty(headers);

            combo.SelectedIndex = viewModel.ThemePresets.ToList().FindIndex(option => option.Id == "ayu-dark");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);

            var dark = ThemePresetCatalog.Resolve("ayu-dark").Palette;
            Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
            Assert.Equal(8, preview.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("themeSwatch")));
            Assert.Contains("Ayu Dark", AutomationProperties.GetName(preview), StringComparison.Ordinal);
            Assert.All(headers, header =>
            {
                Assert.Equal(Color.Parse(dark.ControlBackground), BrushColor(header.Background));
                Assert.Equal(Color.Parse(dark.ControlForeground), BrushColor(header.Foreground));
                Assert.Equal(Color.Parse(dark.Border), BrushColor(header.BorderBrush));
            });
            Assert.Equal(Color.Parse(dark.HoverBackground), ResourceColor(AvaloniaThemeApplicationService.HoverBackgroundBrushKey));
            Assert.Equal(Color.Parse(dark.ActiveBackground), ResourceColor(AvaloniaThemeApplicationService.ActiveBackgroundBrushKey));
            Assert.Equal(ThemeSettings.Default, host.ThemeSettingsStore.Current);

            combo.SelectedIndex = viewModel.ThemePresets.ToList().FindIndex(option => option.Id == "solarized-light");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            var light = ThemePresetCatalog.Resolve("solarized-light").Palette;
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            Assert.All(headers, header => Assert.Equal(Color.Parse(light.ControlBackground), BrushColor(header.Background)));
        }
        finally
        {
            themeService.Apply(ThemeSettings.Default);
            settingsWindow.Close();
        }
    }

    private static Color ResourceColor(string key) =>
        BrushColor(Assert.IsAssignableFrom<IBrush>(Application.Current!.Resources[key]));

    private static Color BrushColor(IBrush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;
}
