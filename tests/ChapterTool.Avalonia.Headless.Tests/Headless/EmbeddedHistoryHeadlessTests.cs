using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class EmbeddedHistoryHeadlessTests
{
    [AvaloniaTheory]
    [InlineData(420)]
    [InlineData(600)]
    public async Task Embedded_history_inspection_keeps_cursor_and_close_detaches_and_disposes_content(double height)
    {
        using var app = new MainWindowHeadlessTestHost();
        await app.LoadAsync("movie.txt");
        await app.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited intro"));

        var presenter = new EmbeddedToolPresenter();
        HistoryToolViewModel? viewModel = null;
        var toolId = ToolIds.History;
        var descriptor = new ToolDescriptor(toolId, "History.Title", new ToolSizeConstraints(), ToolRefreshPolicy.Reuse, context =>
        {
            viewModel = new HistoryToolViewModel(context.Session.History, context.Localizer, context.CloseTool);
            return new HistoryToolView { DataContext = viewModel };
        });
        using var embeddedHost = new EmbeddedAuxiliaryToolHost(
            new ToolCatalog([descriptor]),
            presenter,
            request => new ToolCreationContext(
                request.Session,
                request.Localizer,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                string.Empty,
                null!,
                null!,
                CloseTool: request.CloseTool));

        var request = new AuxiliaryToolRequest(
            app.ViewModel.ToolSession,
            app.Localizer,
            new RuntimeCapabilities(
                RuntimeSourceMode.LocalPath,
                RuntimeOutputMode.Directory,
                RuntimeSecondarySurfaceMode.InView,
                CanReadClipboard: false,
                CanWriteClipboard: false,
                CanConfigureExternalTools: false,
                CanRunExternalProcesses: false,
                CanOpenLocalPaths: false));
        var opened = await embeddedHost.OpenAsync(toolId, request, CancellationToken.None);

        Assert.Equal(AuxiliaryToolResultKind.Opened, opened.Kind);
        var view = Assert.IsType<HistoryToolView>(presenter.Content);
        var historyViewModel = Assert.IsType<HistoryToolViewModel>(viewModel);
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, historyViewModel, 520, height);
        try
        {
            Assert.True(historyViewModel.IsNarrowLayout);
            var root = historyViewModel.TreeRoots.Single();
            var cursorBeforeInspection = app.ViewModel.ToolSession.History.Snapshot!.CursorId;
            var tree = Assert.IsType<TreeView>(view.FindControl<TreeView>("NarrowHistoryEntries"));
            Assert.True(tree.IsVisible && tree.IsEnabled);
            tree.SelectedItem = root;
            Dispatcher.UIThread.RunJobs();
            await historyViewModel.RetryDetailsAsync();
            historyViewModel.ShowDetailsPage();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal(cursorBeforeInspection, app.ViewModel.ToolSession.History.Snapshot!.CursorId);
            Assert.Equal(root.Id, historyViewModel.Details!.NodeId);
            Assert.True(Assert.IsType<ListBox>(view.FindControl<ListBox>("NarrowRootChapterList")).IsVisible);

            var restore = Assert.IsType<Button>(view.FindControl<Button>("NarrowRestore"));
            var close = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button =>
                button.Content?.ToString() == app.Localizer.GetString("History.Close")
                && button.GetVisualAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible));
            AssertFooterButtonWithinViewport(restore, window);
            AssertFooterButtonWithinViewport(close, window);

            await historyViewModel.CloseCommand.ExecuteAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.Null(presenter.Content);
            Assert.Null(view.DataContext);
            Assert.Null(historyViewModel.SelectedEntry);
            Assert.Null(historyViewModel.Details);
            Assert.Empty(historyViewModel.Entries);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void AssertFooterButtonWithinViewport(Control button, Window window)
    {
        Assert.True(button.IsVisible);
        var origin = button.TranslatePoint(new Point(0, 0), window);
        Assert.NotNull(origin);
        Assert.True(origin!.Value.X >= 0);
        Assert.True(origin.Value.Y >= 0);
        Assert.True(origin.Value.X + button.Bounds.Width <= window.ClientSize.Width);
        Assert.True(origin.Value.Y + button.Bounds.Height <= window.ClientSize.Height);
    }
}
