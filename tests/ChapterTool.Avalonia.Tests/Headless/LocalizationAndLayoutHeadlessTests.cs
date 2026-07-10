using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
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

    private static bool ContainsEncodingArtifact(string text) =>
        text.Contains('\uFFFD', StringComparison.Ordinal) ||
        InvalidTextControlCharacterRegex().IsMatch(text);

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTextControlCharacterRegex();
}
