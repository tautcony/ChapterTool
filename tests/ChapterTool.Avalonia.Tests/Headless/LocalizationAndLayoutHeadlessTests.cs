using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Configuration;
using System.Text.RegularExpressions;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed partial class LocalizationAndLayoutHeadlessTests
{
    [AvaloniaFact]
    public async Task Runtime_language_switch_refreshes_main_window_and_tool_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var host = new MainWindowHeadlessTestHost(localizer: localizer);

        await host.LoadAsync("movie.txt");

        Assert.True(host.ContainsRenderedText("Load"));
        Assert.True(host.ContainsRenderedText("Save"));
        Assert.Equal("Keep original", ChapterNameModeSelectionText(host));
        Assert.Equal("Loaded 1 chapters", host.ViewModel.StatusText);
        var xmlLanguageBox = host.RequiredControl<ComboBox>("XmlLanguageBox");
        host.ViewModel.SaveFormat = Core.Exporting.ChapterExportFormat.Xml;
        xmlLanguageBox.SelectedIndex = host.ViewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        await host.LayoutAsync();
        Assert.Equal("jpn（Japanese）", xmlLanguageBox.SelectionBoxItem?.ToString());

        localizer.SetCulture("ja-JP");
        await host.LayoutAsync();

        Assert.True(host.ContainsRenderedText("読み込み"));
        Assert.True(host.ContainsRenderedText("保存"));
        Assert.Equal("元の名前を保持", ChapterNameModeSelectionText(host));
        Assert.Equal("1 個のチャプターを読み込みました", host.ViewModel.StatusText);
        Assert.False(string.IsNullOrWhiteSpace(xmlLanguageBox.SelectionBoxItem?.ToString()));
        Assert.StartsWith("jpn（", xmlLanguageBox.SelectionBoxItem?.ToString(), StringComparison.Ordinal);

        localizer.SetCulture("zh-CN");
        await host.LayoutAsync();

        Assert.Equal("保留原名", ChapterNameModeSelectionText(host));
        Assert.False(string.IsNullOrWhiteSpace(xmlLanguageBox.SelectionBoxItem?.ToString()));
        Assert.StartsWith("jpn（", xmlLanguageBox.SelectionBoxItem?.ToString(), StringComparison.Ordinal);

        var languageWindow = await MainWindowHeadlessTestHost.RenderToolAsync(new LanguageToolView(), new LanguageToolViewModel(host.ViewModel));
        try
        {
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(languageWindow, "语言"));
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(languageWindow, "应用"));
        }
        finally
        {
            languageWindow.Close();
        }
    }

    private static string ChapterNameModeSelectionText(MainWindowHeadlessTestHost host)
    {
        var selectedItem = Assert.IsType<SelectorDisplayOption>(host.RequiredControl<ComboBox>("ChapterNameModeBox").SelectedItem);
        return selectedItem.DisplayText;
    }

    [AvaloniaFact]
    public async Task Unsupported_or_blank_language_falls_back_without_keys_or_mojibake()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "xx-YY"));

        await host.LayoutAsync();

        Assert.Equal("zh-CN", host.ViewModel.UiLanguage);
        Assert.True(host.ContainsRenderedText("载入"));
        var rendered = MainWindowHeadlessTestHost.RenderedTexts(host.Window);
        Assert.DoesNotContain(rendered, text => text.StartsWith("Main.", StringComparison.Ordinal) || text.StartsWith("Common.", StringComparison.Ordinal));
        Assert.DoesNotContain(rendered, ContainsEncodingArtifact);
    }

    [AvaloniaFact]
    public async Task English_main_window_option_labels_have_room_at_default_and_narrow_widths()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        await host.LoadAsync("movie.txt");

        foreach (var size in new[] { UiTestSize.Default, UiTestSize.Narrow })
        {
            await host.LayoutAtAsync(size);

            foreach (var groupName in new[]
            {
                "FormatOptionsGroup",
                "ChapterNameOptionsGroup",
                "OrderShiftOptionsGroup",
                "XmlLanguageOptionsGroup",
                "ExpressionOptionsGroup"
            })
            {
                var group = host.RequiredControl<Grid>(groupName);
                var label = MainWindowHeadlessTestHost.RequiredDescendant<TextBlock>(
                    group,
                    block => block.Classes.Contains("optionLabel"),
                    $"{groupName} label");

                Assert.True(label.Bounds.Width > 0, $"{groupName} label width was {label.Bounds.Width}.");
                Assert.True(label.Bounds.Height >= 14, $"{groupName} label height was {label.Bounds.Height}.");
            }

            var artifact = await host.CaptureArtifactAsync($"main-window-en-{size.ToString().ToLowerInvariant()}.png");
            Assert.True(File.Exists(artifact));
            Assert.True(new FileInfo(artifact).Length > 0);
        }
    }

    [AvaloniaFact]
    public async Task Main_window_layout_captures_default_wide_and_narrow_artifacts()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Option("OGM", "movie.txt", "Intro", "Middle", "Ending")));
        await host.LoadAsync("movie.txt");

        foreach (var size in new[] { UiTestSize.Default, UiTestSize.Wide, UiTestSize.Narrow })
        {
            await host.LayoutAtAsync(size);
            var artifact = await host.CaptureArtifactAsync($"main-window-{size.ToString().ToLowerInvariant()}.png");

            Assert.True(File.Exists(artifact));
            Assert.True(new FileInfo(artifact).Length > 0);
            Assert.True(host.RequiredControl<Button>("LoadButton").Bounds.Width > 0);
            Assert.True(host.RequiredControl<Button>("SaveButton").Bounds.Width > 0);
            Assert.True(host.RequiredControl<NumericUpDown>("OrderShiftBox").Bounds.Width >= 92);
            Assert.True(host.RequiredControl<DataGrid>("ChapterGrid").Columns.All(column => column.MinWidth > 0));
            Assert.True(host.RequiredControl<Grid>("AdvancedOptionsGrid").Bounds.Width > 0);
        }
    }

    [AvaloniaFact]
    public async Task Settings_tool_layout_captures_default_wide_and_narrow_artifacts()
    {
        using var host = new MainWindowHeadlessTestHost();
        var viewModel = new SettingsToolViewModel(host.ViewModel, host.AppSettingsStore, host.ThemeSettingsStore, host.Localizer);
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
                await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

                var artifact = await host.CaptureArtifactAsync($"settings-tool-{name}.png", window);

                Assert.True(File.Exists(artifact));
                Assert.True(new FileInfo(artifact).Length > 0);
                Assert.True(MainWindowHeadlessTestHost.Descendants<TabControl>(window).Single().Bounds.Width > 0);
                Assert.Contains(MainWindowHeadlessTestHost.Descendants<Button>(window), button => button.Command == viewModel.SaveCommand && button.Bounds.Height >= 24);
                Assert.Contains(MainWindowHeadlessTestHost.Descendants<Button>(window), button => button.Command == viewModel.ResetCommand && button.Bounds.Height >= 24);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static bool ContainsEncodingArtifact(string text) =>
        text.Contains('\uFFFD', StringComparison.Ordinal) ||
        InvalidTextControlCharacterRegex().IsMatch(text);

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTextControlCharacterRegex();
}
