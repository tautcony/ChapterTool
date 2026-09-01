using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
using Optris.Icons.Avalonia;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

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
        using var viewModel = new SettingsToolViewModel(host.ViewModel.ToolSession.Preferences, host.SettingsStore, host.Localizer, autoLoad: false);
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
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Icon_only_settings_buttons_have_accessible_names()
    {
        using var host = new MainWindowHeadlessTestHost();
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
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
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task External_tool_labels_align_with_their_input_boxes()
    {
        using var host = new MainWindowHeadlessTestHost();
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
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
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabControl.SelectedIndex = 1;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var mkvLabel = FindNamed<TextBlock>(window, "MkvToolnixLabel");
            var mkvInput = FindNamed<TextBox>(window, "MkvToolnixTextBox");
            var ffprobeLabel = FindNamed<TextBlock>(window, "FfprobeLabel");
            var ffprobeInput = FindNamed<TextBox>(window, "FfprobeTextBox");

            Assert.InRange(Math.Abs(CenterY(mkvLabel, window) - CenterY(mkvInput, window)), 0, 1);
            Assert.InRange(Math.Abs(CenterY(ffprobeLabel, window) - CenterY(ffprobeInput, window)), 0, 1);
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Settings_footer_opens_the_configured_settings_folder_from_the_leftmost_button()
    {
        var shellService = new MainWindowHeadlessTestHost.FakeShellService();
        using var host = new MainWindowHeadlessTestHost(shellService: shellService);
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
            host.Localizer,
            shellService: shellService,
            settingsDirectory: settingsDirectory,
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
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var openFolderButton = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "OpenSettingsFolderButton");
            var resetButton = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, viewModel.ResetCommand));

            Assert.Equal("Open settings folder", AutomationProperties.GetName(openFolderButton));
            Assert.True(Left(openFolderButton, window) < Left(resetButton, window));
            Assert.InRange(Math.Abs(Top(resetButton, window) - Top(openFolderButton, window)), 0, 4);

            openFolderButton.Command!.Execute(openFolderButton.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(settingsDirectory, Assert.Single(shellService.Opened));
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Settings_inputs_and_footer_remain_compact_and_aligned_at_minimum_width()
    {
        using var host = new MainWindowHeadlessTestHost();
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
            host.Localizer,
            autoLoad: false);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        var window = new Window
        {
            Content = new SettingsToolView { DataContext = viewModel },
            Width = 600,
            Height = 420
        };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var saveDirectory = FindNamed<TextBox>(window, "SaveDirectoryTextBox");
            var language = FindNamed<ComboBox>(window, "GeneralLanguageCombo");
            var browse = FindNamed<Button>(window, "BrowseSaveDirectoryButton");
            var clear = FindNamed<Button>(window, "ClearSaveDirectoryButton");
            var footer = FindNamed<Border>(window, "SettingsFooter");
            var folder = FindNamed<Button>(window, "OpenSettingsFolderButton");
            var reset = FindNamed<Button>(window, "ResetSettingsButton");
            var save = FindNamed<Button>(window, "SaveSettingsButton");
            var browseIcon = browse.GetVisualDescendants().OfType<Icon>().Single();
            var formEditorLeft = Left(saveDirectory, window);
            var formEditorRight = Right(saveDirectory, window);

            Assert.Equal(32, saveDirectory.Bounds.Height);
            Assert.Equal(32, language.Bounds.Height);
            Assert.Contains(saveDirectory, browse.GetVisualAncestors());
            Assert.Contains(saveDirectory, clear.GetVisualAncestors());
            Assert.True(browse.IsEffectivelyVisible);
            Assert.Equal(30, browse.Bounds.Width);
            Assert.Equal(15, browseIcon.Bounds.Width);
            Assert.NotEqual(0, Assert.IsType<SolidColorBrush>(browseIcon.Foreground).Color.A);
            Assert.True(Right(browse, window) <= Right(saveDirectory, window));
            Assert.True(Right(clear, window) <= Right(saveDirectory, window));

            Assert.Equal(48, footer.Bounds.Height);
            Assert.Equal(reset.Bounds.Height, save.Bounds.Height);
            Assert.True(Right(folder, window) < Left(reset, window));
            Assert.True(Right(reset, window) < Left(save, window));
            Assert.True(Right(save, window) <= window.ClientSize.Width);
            Assert.True(Top(reset, window) >= Top(footer, window));
            Assert.True(Top(save, window) >= Top(footer, window));

            Assert.InRange(formEditorLeft / window.ClientSize.Width, 0.18, 0.30);
            Assert.True(saveDirectory.Bounds.Width >= window.ClientSize.Width * 0.60);

            SelectAppearanceTab(window);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var themePreset = FindNamed<ComboBox>(window, "ThemePresetCombo");
            Assert.InRange(Math.Abs(Left(themePreset, window) - formEditorLeft), 0, 1);
            Assert.InRange(Math.Abs(Right(themePreset, window) - formEditorRight), 0, 1);
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Preset_selection_updates_preview_runtime_theme_and_existing_grid_headers()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();
        var themeService = new AvaloniaThemeApplicationService();
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
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
            SelectAppearanceTab(settingsWindow);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);
            var combo = settingsWindow.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "ThemePresetCombo");
            var preview = settingsWindow.GetVisualDescendants().OfType<ItemsControl>().Single(control => control.Name == "ThemePalettePreview");
            var headers = host.Window.GetVisualDescendants().OfType<DataGridColumnHeader>().ToArray();
            Assert.NotEmpty(headers);

            combo.SelectedIndex = viewModel.Appearance.ThemePresets.ToList().FindIndex(option => option.Id == "ayu-dark");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(settingsWindow);

            var dark = ThemePresetCatalog.Resolve("ayu-dark");
            var darkTokens = AvaloniaThemeApplicationService.ComputeImportedThemeColors(dark);
            Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
            Assert.Equal(8, preview.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("themeSwatch")));
            Assert.Contains("Ayu Dark", AutomationProperties.GetName(preview), StringComparison.Ordinal);
            Assert.All(headers, header =>
            {
                Assert.Equal(Color.Parse(darkTokens["Color.Contents"]), BrushColor(header.Background));
                Assert.Equal(Color.Parse(darkTokens["Color.FG1"]), BrushColor(header.Foreground));
                Assert.Equal(Color.Parse(darkTokens["Color.Border1"]), BrushColor(header.BorderBrush));
            });
            Assert.Equal(Color.Parse(darkTokens["Color.Hover"]), ColorResource("Color.Hover"));
            Assert.Equal(Color.Parse(darkTokens["Color.Active"]), ColorResource("Color.Active"));
            Assert.Equal(ThemeSettings.Default, host.SettingsStore.Current.Theme);

            combo.SelectedIndex = viewModel.Appearance.ThemePresets.ToList().FindIndex(option => option.Id == "solarized-light");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            var lightTokens = AvaloniaThemeApplicationService.ComputeImportedThemeColors(ThemePresetCatalog.Resolve("solarized-light"));
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            Assert.All(headers, header => Assert.Equal(Color.Parse(lightTokens["Color.Contents"]), BrushColor(header.Background)));
        }
        finally
        {
            themeService.Apply(ThemeSettings.Default);
            await MainWindowHeadlessTestHost.CloseWindowAsync(settingsWindow);
        }
    }

    [AvaloniaFact]
    public async Task Font_selections_refresh_existing_ui_editor_and_text_preview_then_save_or_discard()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var fontService = host.FontApplicationService;
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
            host.Localizer,
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
            SelectAppearanceTab(settingsWindow);
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

            uiCombo.SelectedIndex = viewModel.Appearance.UiFontFamilies.ToList().FindIndex(option => option.FamilyName == "ChapterTool UI Test");
            monoCombo.SelectedIndex = viewModel.Appearance.MonospaceFontFamilies.ToList().FindIndex(option => option.FamilyName == "ChapterTool Mono Test");
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
            Assert.Equal(FontSettings.Default, host.SettingsStore.Current.Font);

            await viewModel.SaveCommand.ExecuteAsync();
            Assert.Equal(new FontSettings("ChapterTool UI Test", "ChapterTool Mono Test"), host.SettingsStore.Current.Font);
            viewModel.Appearance.SelectedUiFontFamilyIndex = 0;
            viewModel.Appearance.SelectedMonospaceFontFamilyIndex = 0;
            viewModel.DiscardUnsavedAppearanceChanges();
            Assert.Equal("ChapterTool UI Test", ResourceFont(AvaloniaFontApplicationService.UiFontFamilyKey));
            Assert.Equal("ChapterTool Mono Test", ResourceFont(AvaloniaFontApplicationService.MonospaceFontFamilyKey));

            host.Localizer.SetCulture("zh-CN");
            Assert.Contains("界面字体预览", AutomationProperties.GetName(uiPreview), StringComparison.Ordinal);
            Assert.Contains("章节字幕", viewModel.Appearance.FontPreviewText, StringComparison.Ordinal);
        }
        finally
        {
            fontService.Apply(FontSettings.Default);
            await MainWindowHeadlessTestHost.CloseWindowAsync(textWindow);
            await MainWindowHeadlessTestHost.CloseWindowAsync(settingsWindow);
        }
    }

    [AvaloniaFact]
    public async Task Font_selector_renders_realized_items_in_their_own_family_without_realizing_the_full_catalog()
    {
        using var host = new MainWindowHeadlessTestHost();
        var familyNames = Enumerable.Range(1, 160).Select(index => $"ChapterTool Font {index:000}").ToArray();
        var catalog = new AvaloniaFontFamilyCatalog(familyNames);
        var fontService = new AvaloniaFontApplicationService(catalog);
        using var viewModel = new SettingsToolViewModel(
            host.ViewModel.ToolSession.Preferences,
            host.SettingsStore,
            host.Localizer,
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
        ComboBox? combo = null;

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            SelectAppearanceTab(window);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            combo = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "UiFontFamilyCombo");

            combo.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var realized = Enumerable.Range(0, viewModel.Appearance.UiFontFamilies.Count)
                .Select(combo.ContainerFromIndex)
                .Where(static container => container is not null)
                .ToArray();
            Assert.NotEmpty(realized);
            Assert.True(realized.Length < viewModel.Appearance.UiFontFamilies.Count);
            var fontItem = realized
                .SelectMany(static container => container!.GetVisualDescendants().OfType<TextBlock>())
                .First(block => block.Text?.StartsWith("ChapterTool Font ", StringComparison.Ordinal) == true);
            Assert.Equal(fontItem.Text, fontItem.FontFamily.Name);
        }
        finally
        {
            combo?.IsDropDownOpen = false;
            Dispatcher.UIThread.RunJobs();
            fontService.Apply(FontSettings.Default);
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    private static Color ResourceColor(string key) =>
        BrushColor(Assert.IsType<IBrush>(Application.Current!.Resources[key], exactMatch: false));

    private static Color ColorResource(string key) =>
        Assert.IsType<Color>(Application.Current!.Resources[key]);

    private static string ResourceFont(string key) =>
        Assert.IsType<FontFamily>(Application.Current!.Resources[key], exactMatch: false).Name;

    private static double Left(Control control, Window window) =>
        control.TranslatePoint(default, window)?.X
        ?? throw new InvalidOperationException($"Could not translate {control.Name} bounds.");

    private static double Top(Control control, Window window) =>
        control.TranslatePoint(default, window)?.Y
        ?? throw new InvalidOperationException($"Could not translate {control.Name} bounds.");

    private static double CenterY(Control control, Window window) => Top(control, window) + control.Bounds.Height / 2;

    private static double Right(Control control, Window window) => Left(control, window) + control.Bounds.Width;

    private static T FindNamed<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void SelectAppearanceTab(Window window)
    {
        var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
        var tab = tabControl.ItemsView.OfType<TabItem>().Single(item =>
            item.Header is TextBlock { Text: "外观" or "Appearance" or "外観" });
        tabControl.SelectedItem = tab;
    }

    private static Color BrushColor(IBrush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;
}
