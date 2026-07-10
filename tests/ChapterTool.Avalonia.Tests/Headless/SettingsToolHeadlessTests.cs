using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
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

    [AvaloniaFact]
    public async Task Font_selections_refresh_existing_ui_editor_and_text_preview_then_save_or_discard()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var fontService = host.FontApplicationService;
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer,
            fontSettingsStore: host.FontSettingsStore,
            fontFamilyCatalog: host.FontFamilyCatalog,
            fontApplicationService: fontService,
            autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var settingsWindow = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 760,
            Height = 620
        };
        var textTool = new TextToolView { DataContext = new TextToolViewModel(() => "00:00:00.000 Intro") };
        var textWindow = new Window { Content = textTool, Width = 620, Height = 360 };

        try
        {
            settingsWindow.Show();
            textWindow.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(textWindow);
            var tabControl = settingsWindow.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 3;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);

            var uiCombo = settingsWindow.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "UiFontFamilyCombo");
            var monoCombo = settingsWindow.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "MonospaceFontFamilyCombo");
            var themeCombo = settingsWindow.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "ThemePresetCombo");
            var uiPreview = settingsWindow.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "UiFontPreview");
            var monoPreview = settingsWindow.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "MonospaceFontPreview");
            var editor = host.Window.GetVisualDescendants().OfType<TextEditor>().Single();
            var chapterGrid = host.RequiredControl<DataGrid>("ChapterGrid");
            var orderShiftLabel = host.RequiredControl<TextBlock>("OrderShiftLabel");
            var orderShiftBox = host.RequiredControl<NumericUpDown>("OrderShiftBox");
            Assert.NotEmpty(host.ViewModel.Rows);
            chapterGrid.ScrollIntoView(host.ViewModel.Rows[0], chapterGrid.Columns[0]);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            var cells = host.Window.GetVisualDescendants().OfType<DataGridCell>().ToArray();
            var headers = host.Window.GetVisualDescendants().OfType<DataGridColumnHeader>().ToArray();
            Assert.NotEmpty(cells);
            Assert.NotEmpty(headers);
            Assert.Equal(Left(themeCombo, settingsWindow), Left(uiCombo, settingsWindow), precision: 3);
            Assert.Equal(Left(themeCombo, settingsWindow), Left(monoCombo, settingsWindow), precision: 3);
            var normalText = settingsWindow.GetVisualDescendants().OfType<TextBlock>()
                .First(block => string.Equals(block.Text, "Appearance", StringComparison.Ordinal));
            var previewText = textTool.FindControl<SelectableTextBlock>("ContentText")
                ?? throw new InvalidOperationException("Text preview content was not found.");

            uiCombo.SelectedIndex = viewModel.UiFontFamilies.ToList().FindIndex(option => option.FamilyName == "ChapterTool UI Test");
            monoCombo.SelectedIndex = viewModel.MonospaceFontFamilies.ToList().FindIndex(option => option.FamilyName == "ChapterTool Mono Test");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(textWindow);

            Assert.Equal("ChapterTool UI Test", ResourceFont(AvaloniaFontApplicationService.UiFontFamilyKey));
            Assert.Equal("ChapterTool Mono Test", ResourceFont(AvaloniaFontApplicationService.MonospaceFontFamilyKey));
            Assert.Equal("ChapterTool UI Test", normalText.FontFamily.Name);
            Assert.Equal("ChapterTool Mono Test", editor.FontFamily.Name);
            Assert.Equal("ChapterTool Mono Test", previewText.FontFamily.Name);
            Assert.All(cells, cell => Assert.Equal("ChapterTool Mono Test", cell.FontFamily.Name));
            Assert.All(headers, header => Assert.Equal("ChapterTool UI Test", header.FontFamily.Name));
            Assert.Equal("ChapterTool UI Test", orderShiftLabel.FontFamily.Name);
            Assert.Equal("ChapterTool Mono Test", orderShiftBox.FontFamily.Name);
            Assert.All(
                orderShiftBox.GetVisualDescendants().OfType<TextBox>(),
                editorBox => Assert.Equal("ChapterTool Mono Test", editorBox.FontFamily.Name));
            Assert.Contains("ChapterTool UI Test", AutomationProperties.GetName(uiPreview), StringComparison.Ordinal);
            Assert.Contains("ChapterTool Mono Test", AutomationProperties.GetName(monoPreview), StringComparison.Ordinal);
            Assert.All(
                settingsWindow.GetVisualDescendants().OfType<Control>().Where(control => control.GetType().Name == "Icon"),
                icon => Assert.True(icon.IsVisible));
            Assert.Equal(FontSettings.Default, host.FontSettingsStore.Current);

            await viewModel.SaveCommand.ExecuteAsync();
            Assert.Equal(new FontSettings("ChapterTool UI Test", "ChapterTool Mono Test"), host.FontSettingsStore.Current);
            viewModel.SelectedUiFontFamilyIndex = 0;
            viewModel.SelectedMonospaceFontFamilyIndex = 0;
            viewModel.DiscardUnsavedAppearanceChanges();
            Assert.Equal("ChapterTool UI Test", ResourceFont(AvaloniaFontApplicationService.UiFontFamilyKey));
            Assert.Equal("ChapterTool Mono Test", ResourceFont(AvaloniaFontApplicationService.MonospaceFontFamilyKey));

            host.Localizer.SetCulture("zh-CN");
            Assert.Contains("界面字体预览", AutomationProperties.GetName(uiPreview), StringComparison.Ordinal);
            Assert.Contains("章节字幕", viewModel.FontPreviewText, StringComparison.Ordinal);
        }
        finally
        {
            fontService.Apply(FontSettings.Default);
            textWindow.Close();
            settingsWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task Font_selector_renders_realized_items_in_their_own_family_without_realizing_the_full_catalog()
    {
        using var host = new MainWindowHeadlessTestHost();
        var familyNames = Enumerable.Range(1, 160).Select(index => $"ChapterTool Font {index:000}").ToArray();
        var catalog = new AvaloniaFontFamilyCatalog(familyNames);
        var fontService = new AvaloniaFontApplicationService(catalog);
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer,
            fontSettingsStore: host.FontSettingsStore,
            fontFamilyCatalog: catalog,
            fontApplicationService: fontService,
            autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var window = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 760,
            Height = 620
        };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            window.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 3;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var combo = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "UiFontFamilyCombo");

            combo.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var realized = Enumerable.Range(0, viewModel.UiFontFamilies.Count)
                .Select(combo.ContainerFromIndex)
                .Where(static container => container is not null)
                .ToArray();
            Assert.NotEmpty(realized);
            Assert.True(realized.Length < viewModel.UiFontFamilies.Count);
            var fontItem = realized
                .SelectMany(static container => container!.GetVisualDescendants().OfType<TextBlock>())
                .First(block => block.Text?.StartsWith("ChapterTool Font ", StringComparison.Ordinal) == true);
            Assert.Equal(fontItem.Text, fontItem.FontFamily.Name);
        }
        finally
        {
            fontService.Apply(FontSettings.Default);
            window.Close();
        }
    }

    private static Color ResourceColor(string key) =>
        BrushColor(Assert.IsAssignableFrom<IBrush>(Application.Current!.Resources[key]));

    private static string ResourceFont(string key) =>
        Assert.IsType<FontFamily>(Application.Current!.Resources[key]).Name;

    private static double Left(Control control, Window window) =>
        control.TranslatePoint(default, window)?.X
        ?? throw new InvalidOperationException($"Could not translate {control.Name} bounds.");

    private static Color BrushColor(IBrush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;
}
