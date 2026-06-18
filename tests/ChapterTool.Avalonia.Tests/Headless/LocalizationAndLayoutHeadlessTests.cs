using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Headless;

public sealed class LocalizationAndLayoutHeadlessTests
{
    [AvaloniaFact]
    public async Task Runtime_language_switch_refreshes_main_window_and_tool_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var host = new MainWindowHeadlessTestHost(localizer: localizer);

        await host.LoadAsync("movie.txt");

        Assert.True(host.ContainsRenderedText("Load"));
        Assert.True(host.ContainsRenderedText("Save"));
        Assert.Equal("Loaded 1 chapters", host.ViewModel.StatusText);

        localizer.SetCulture("ja-JP");
        await host.LayoutAsync();

        Assert.True(host.ContainsRenderedText("読み込み"));
        Assert.True(host.ContainsRenderedText("保存"));
        Assert.Equal("1 個のチャプターを読み込みました", host.ViewModel.StatusText);

        var languageWindow = await MainWindowHeadlessTestHost.RenderToolAsync(new LanguageToolView(), new LanguageToolViewModel(host.ViewModel));
        try
        {
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(languageWindow, "言語"));
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(languageWindow, "適用"));
        }
        finally
        {
            languageWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task Unsupported_or_blank_language_falls_back_without_keys_or_mojibake()
    {
        using var host = new MainWindowHeadlessTestHost(appSettings: new AppSettings(Language: "xx-YY"));

        await host.LayoutAsync();

        Assert.Equal("zh-CN", host.ViewModel.UiLanguage);
        Assert.True(host.ContainsRenderedText("载入"));
        var rendered = host.RenderedTexts(host.Window);
        Assert.DoesNotContain(rendered, text => text.StartsWith("Main.", StringComparison.Ordinal) || text.StartsWith("Common.", StringComparison.Ordinal));
        Assert.DoesNotContain(rendered, text => text.Contains("杞藉叆", StringComparison.Ordinal) || text.Contains("淇濆瓨", StringComparison.Ordinal));
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
                Assert.True(MainWindowHeadlessTestHost.Descendants<Button>(window).All(button => button.Bounds.Height == 0 || button.Bounds.Height >= 24));
            }
        }
        finally
        {
            window.Close();
        }
    }
}
