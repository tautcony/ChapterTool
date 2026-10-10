using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
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
        var historySizes = new (string Name, double Width, double Height)[]
        {
            ("default", 1000, 700),
            ("wide", 1280, 800),
            ("narrow", 520, 600)
        };
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", names)));
        await host.LoadAsync("movie.txt");

        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "First branch"));
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(1, "Nested first branch"));
        await host.ViewModel.UndoCommand.ExecuteAsync();
        const string shortChapterName = "Alternate branch";
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, shortChapterName));
        var alternateNodeId = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        await host.ViewModel.UndoCommand.ExecuteAsync();
        var longChapterName = $"Long alternate {new string('x', 240)}";
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, longChapterName));
        var longValueNodeId = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        await host.ViewModel.UndoCommand.ExecuteAsync();
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            host.Localizer.SetCulture(culture);
            using var historyViewModel = new HistoryToolViewModel(host.ViewModel.ToolSession.History, host.Localizer);
            Assert.True(historyViewModel.HasAlternateRedoChoices,
                $"Expected a fork at the cursor {host.ViewModel.ToolSession.History.Snapshot!.CursorId}. History nodes: {string.Join("; ", host.ViewModel.ToolSession.History.Snapshot.Nodes.Select(node => $"{node.Id}:{node.ParentId}:{node.Description}"))}");
            historyViewModel.SelectedEntry = historyViewModel.Entries.Single(entry => entry.Id == alternateNodeId);
            await historyViewModel.RetryDetailsAsync();
            Assert.NotNull(historyViewModel.Details);
            Assert.Contains(historyViewModel.VisibleChanges, row => row.AfterText == shortChapterName);
            var historyView = new HistoryToolView { DataContext = historyViewModel };
            foreach (var (name, width, height) in historySizes)
            {
                var historyWindow = await MainWindowHeadlessTestHost.RenderToolAsync(historyView, historyViewModel, width, height);
                try
                {
                    Assert.Equal(name == "narrow", historyViewModel.IsNarrowLayout);
                    Assert.Equal(width, historyWindow.Bounds.Width);
                    Assert.Equal(height, historyWindow.Bounds.Height);
                    MainWindowHeadlessTestHost.CaptureRenderedFrame(
                        historyWindow,
                        Path.Combine("artifacts", "edit-history-tree-details", culture, $"history-{name}-branch-details.png"));
                    if (name == "default")
                    {
                        historyViewModel.SelectedEntry = historyViewModel.Entries.Single(entry => entry.Id == longValueNodeId);
                        await historyViewModel.RetryDetailsAsync();
                        Dispatcher.UIThread.RunJobs();
                        Assert.Contains(historyViewModel.VisibleChanges, row => row.AfterText == longChapterName);
                        MainWindowHeadlessTestHost.CaptureRenderedFrame(
                            historyWindow,
                            Path.Combine("artifacts", "edit-history-tree-details", culture, "history-default-long-value.png"));
                        historyViewModel.SelectedEntry = historyViewModel.Entries.Single(entry => entry.Id == alternateNodeId);
                        await historyViewModel.RetryDetailsAsync();
                        Dispatcher.UIThread.RunJobs();
                        var application = global::Avalonia.Application.Current!;
                        var originalThemeVariant = application.RequestedThemeVariant;
                        var themeResourceKeys = AvaloniaThemeApplicationService.ImportedThemeColorKeys
                            .Concat(new[]
                            {
                                AvaloniaThemeApplicationService.FrameNeutralBrushKey,
                                AvaloniaThemeApplicationService.FrameAccurateBrushKey,
                                AvaloniaThemeApplicationService.FrameInexactBrushKey,
                                AvaloniaThemeApplicationService.DiagnosticErrorBrushKey,
                                AvaloniaThemeApplicationService.LogInformationBrushKey,
                                AvaloniaThemeApplicationService.LogWarningBrushKey,
                                AvaloniaThemeApplicationService.LogErrorBrushKey,
                                "ChapterTool.FontSize.Small",
                                "ChapterTool.FontSize.Default",
                                "ChapterTool.FontSize.Large"
                            })
                            .ToArray();
                        var originalThemeResources = themeResourceKeys.ToDictionary(key => key, key => application.Resources[key]);
                        try
                        {
                            new AvaloniaThemeApplicationService().Apply(new ThemeSettings("ayu-dark"));
                            application.Resources["ChapterTool.FontSize.Small"] = 18d;
                            application.Resources["ChapterTool.FontSize.Default"] = 20d;
                            application.Resources["ChapterTool.FontSize.Large"] = 22d;
                            historyView.FontSize = 20;
                            Dispatcher.UIThread.RunJobs();
                            Assert.Contains(historyView.GetVisualDescendants().OfType<TextBlock>(), text => text.IsVisible && text.FontSize >= 20);
                            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                                historyWindow,
                                Path.Combine("artifacts", "edit-history-tree-details", culture, "history-default-dark-large-text.png"));
                        }
                        finally
                        {
                            foreach (var (key, value) in originalThemeResources)
                            {
                                application.Resources[key] = value;
                            }
                            application.RequestedThemeVariant = originalThemeVariant;
                            historyView.FontSize = 13;
                            Dispatcher.UIThread.RunJobs();
                        }
                    }
                    if (name == "narrow")
                    {
                        historyViewModel.ShowDetailsPage();
                        Dispatcher.UIThread.RunJobs();
                        Assert.True(historyView.FindControl<Button>("NarrowRestore")?.IsVisible);
                        MainWindowHeadlessTestHost.CaptureRenderedFrame(
                            historyWindow,
                            Path.Combine("artifacts", "edit-history-tree-details", culture, "history-narrow-details.png"));
                    }
                }
                finally
                {
                    await MainWindowHeadlessTestHost.CloseWindowAsync(historyWindow);
                }
            }
            historyViewModel.SelectedEntry = historyViewModel.Entries.Single(entry => entry.ParentId is null);
            await historyViewModel.RetryDetailsAsync();
            Assert.Equal(30, historyViewModel.RootChapterRows.Count);
            historyViewModel.UpdateAvailableWidth(1000);
            var rootView = new HistoryToolView { DataContext = historyViewModel };
            var rootWindow = await MainWindowHeadlessTestHost.RenderToolAsync(rootView, historyViewModel, 1000, 700);
            try
            {
                var rootList = Assert.IsType<ListBox>(rootView.FindControl<ListBox>("RootChapterList"));
                rootList.ScrollIntoView(historyViewModel.RootChapterRows[^1]);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(rootList.ContainerFromIndex(historyViewModel.RootChapterRows.Count - 1));
                MainWindowHeadlessTestHost.CaptureRenderedFrame(rootWindow,
                    Path.Combine("artifacts", "edit-history-tree-details", culture, "history-root-overview.png"));
            }
            finally
            {
                await MainWindowHeadlessTestHost.CloseWindowAsync(rootWindow);
            }
        }
    }
}
