using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
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
}
