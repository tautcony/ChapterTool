using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowEditingUxScreenshotHeadlessTests
{
    [AvaloniaFact]
    public async Task Capture_main_history_and_preview_surfaces_when_requested()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CHAPTERTOOL_MAIN_WINDOW_UX_SCREENSHOTS")))
        {
            return;
        }

        var names = Enumerable.Range(1, 30).Select(index => $"Chapter {index:D2}").ToArray();
        var sizes = new (string Name, double Width, double Height)[]
        {
            ("default", 760, 600),
            ("wide", 1280, 800),
            ("narrow", 760, 520)
        };
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", names)));
        await host.LoadAsync("movie.txt");

        foreach (var culture in new[] { "en-US", "zh-CN" })
        {
            host.Localizer.SetCulture(culture);
            foreach (var (name, width, height) in sizes)
            {
                await host.LayoutAsync(width, height);
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    host.Window,
                    Path.Combine("artifacts", "main-window-editing-ux", culture, $"main-{name}.png"));
            }

            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            foreach (var (name, width, height) in sizes)
            {
                await host.LayoutAsync(width, height);
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    host.Window,
                    Path.Combine("artifacts", "main-window-editing-ux", culture, $"preview-{name}.png"));
            }

            using var historyViewModel = new HistoryToolViewModel(host.ViewModel.ToolSession.History, host.Localizer);
            var historyView = new HistoryToolView { DataContext = historyViewModel };
            foreach (var (name, width, height) in new (string Name, double Width, double Height)[]
                     {
                         ("default", 520, 560),
                         ("wide", 760, 700),
                         ("narrow", 420, 320)
                     })
            {
                var historyWindow = await MainWindowHeadlessTestHost.RenderToolAsync(historyView, historyViewModel, width, height);
                try
                {
                    MainWindowHeadlessTestHost.CaptureRenderedFrame(
                        historyWindow,
                        Path.Combine("artifacts", "main-window-editing-ux", culture, $"history-{name}.png"));
                }
                finally
                {
                    await MainWindowHeadlessTestHost.CloseWindowAsync(historyWindow);
                }
            }
        }
    }
}
