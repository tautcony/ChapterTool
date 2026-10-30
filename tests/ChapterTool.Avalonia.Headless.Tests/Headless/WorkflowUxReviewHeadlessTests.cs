using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views.Controls;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class WorkflowUxReviewHeadlessTests
{
    private static readonly (string Name, double Width, double Height)[] Viewports =
    [
        ("default", 760, 600),
        ("wide", 1280, 800),
        ("narrow", 760, 520),
    ];

    [AvaloniaFact]
    [Trait("Category", "OptInUxReview")]
    public async Task Capture_desktop_workflow_states_and_check_rendered_geometry()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CHAPTERTOOL_CAPTURE_WORKFLOW_UX"), "1", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var state in Enum.GetValues<ReviewState>())
        {
            foreach (var viewport in Viewports)
            {
                using var host = CreateHost(state);
                await PrepareStateAsync(host, state, viewport.Width, viewport.Height);
                AssertRenderedGeometry(host, state, viewport.Width, viewport.Height);
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    host.Window,
                    $"artifacts/clarify-editing-workflow-ux/desktop/{state.ToString().ToLowerInvariant()}-{viewport.Name}.png");
            }
        }
    }

    private static MainWindowHeadlessTestHost CreateHost(ReviewState state)
    {
        if (state == ReviewState.Empty)
        {
            return new MainWindowHeadlessTestHost(Array.Empty<ChapterImportResult>());
        }

        var chapterNames = Enumerable.Range(1, 8)
            .Select(index => $"Chapter {index:D2} · A deliberately long chapter name for width review")
            .ToArray();
        return new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "workflow-review.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "workflow-review.txt", chapterNames)));
    }

    private static async ValueTask PrepareStateAsync(
        MainWindowHeadlessTestHost host,
        ReviewState state,
        double width,
        double height)
    {
        if (state == ReviewState.Empty)
        {
            await host.LayoutAsync(width, height);
            return;
        }

        await host.LoadAsync("workflow-review.txt");
        if (state == ReviewState.Normal)
        {
            await host.LayoutAsync(width, height);
            return;
        }

        if (state == ReviewState.Pending)
        {
            await host.LayoutAsync(width, height);
            host.ViewModel.Expression = "t + 1";
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            host.Window.GetLayoutManager()!.ExecuteLayoutPass();
            Assert.Equal("t + 1", host.RequiredControl<ExpressionEditor>("ExpressionBox").Text);
            Assert.True(host.RequiredControl<Button>("ApplyContentPreviewButton").IsVisible);
            Assert.True(host.ViewModel.IsContentPreviewPending);
            return;
        }

        if (state == ReviewState.Stale)
        {
            await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Committed before stale preview"));
        }

        host.ViewModel.Expression = state == ReviewState.Invalid ? "t + (" : "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();

        if (state == ReviewState.Stale)
        {
            var current = host.ViewModel.Workspace.CurrentChapterSet!;
            var changed = current with
            {
                Chapters = current.Chapters
                    .Select((chapter, index) => index == 0 ? chapter with { Name = "Committed after preview" } : chapter)
                    .ToArray(),
            };
            host.ViewModel.Workspace.CommitNonStructuralChapterSetResult(changed, "Review stale preview");
            host.ViewModel.RefreshRowsFromPort();
            host.ViewModel.NotifyStateChanged();
            Assert.True(host.ViewModel.CanRefreshContentPreview);
        }

        await host.LayoutAsync(width, height);
    }

    private static void AssertRenderedGeometry(
        MainWindowHeadlessTestHost host,
        ReviewState state,
        double width,
        double height)
    {
        Assert.InRange(Math.Abs(host.Window.Bounds.Width - width), 0, 1);
        Assert.InRange(Math.Abs(host.Window.Bounds.Height - height), 0, 1);

        var load = host.RequiredControl<Control>("LoadButton");
        var save = host.RequiredControl<Control>("SaveButton");
        var workspace = host.RequiredControl<Grid>("ChapterWorkspaceGrid");
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var options = host.RequiredControl<Grid>("AdvancedOptionsGrid");
        var status = host.RequiredControl<Control>("StatusBar");
        Assert.True(load.IsVisible && load.Bounds.Width > 0 && load.Bounds.Height > 0);
        Assert.True(save.IsVisible && save.Bounds.Width > 0 && save.Bounds.Height > 0);
        Assert.True(workspace.IsVisible && workspace.Bounds.Width > 0 && workspace.Bounds.Height >= 128);
        Assert.True(grid.IsVisible && grid.Bounds.Width > 0 && grid.Bounds.Height > 0);
        Assert.True(options.IsVisible && options.Bounds.Width > 0 && options.Bounds.Height > 0);
        Assert.True(status.IsVisible && status.Bounds.Width > 0 && status.Bounds.Height > 0);
        Assert.True(grid.Columns[2].MinWidth >= 128);

        if (state == ReviewState.Empty)
        {
            Assert.True(host.RequiredControl<Control>("ChapterGridEmptyImage").IsVisible);
            Assert.True(host.ViewModel.IsChapterGridEmpty);
            return;
        }

        Assert.False(host.RequiredControl<Control>("ChapterGridEmptyImage").IsVisible);
        var renderedRows = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(static row => row is { IsVisible: true, Bounds.Height: > 0 })
            .ToArray();
        Assert.NotEmpty(renderedRows);

        var row = host.ViewModel.Rows[0];
        var timeTexts = new[] { row.TimeText, row.PreviewBeforeTimeText, row.PreviewTimeText }
            .Where(static text => !string.IsNullOrEmpty(text))
            .ToHashSet(StringComparer.Ordinal);
        var nameTexts = new[] { row.Name, row.PreviewName }
            .Where(static text => !string.IsNullOrEmpty(text))
            .ToHashSet(StringComparer.Ordinal);
        var textBlocks = renderedRows[0].GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static block => block is { IsVisible: true, Bounds.Width: > 0, Bounds.Height: > 0 })
            .ToArray();
        var renderedTimes = textBlocks.Where(block => timeTexts.Contains(block.Text ?? string.Empty)).ToArray();
        var renderedNames = textBlocks.Where(block => nameTexts.Contains(block.Text ?? string.Empty)).ToArray();

        Assert.NotEmpty(renderedTimes);
        Assert.All(renderedTimes, block =>
        {
            Assert.Equal(TextWrapping.NoWrap, block.TextWrapping);
            Assert.InRange(block.TextLayout.Width, 0, block.Bounds.Width + 1);
        });
        Assert.NotEmpty(renderedNames);
        Assert.All(renderedNames, block => Assert.True(block.Bounds.Width >= 96));

        switch (state)
        {
            case ReviewState.Normal:
                Assert.False(host.ViewModel.IsContentPreviewPending);
                break;
            case ReviewState.Valid:
                Assert.True(host.ViewModel.CanApplyContentPreview);
                Assert.True(row.HasPreviewTimeChange);
                break;
            case ReviewState.Invalid:
                Assert.False(host.ViewModel.CanApplyContentPreview);
                Assert.False(row.HasPreviewTimeChange);
                Assert.NotEmpty(host.ViewModel.ExpressionPreviewText);
                break;
            case ReviewState.Pending:
                Assert.True(host.ViewModel.IsContentPreviewPending);
                Assert.False(host.ViewModel.CanApplyContentPreview);
                break;
            case ReviewState.Stale:
                Assert.True(host.ViewModel.CanRefreshContentPreview);
                Assert.False(host.ViewModel.CanApplyContentPreview);
                var refresh = host.RequiredControl<Button>("RefreshContentPreviewButton");
                Assert.True(refresh.IsVisible);
                var origin = refresh.TranslatePoint(default, host.Window);
                Assert.NotNull(origin);
                Assert.InRange(origin.Value.X + refresh.Bounds.Width, 0, width);
                break;
        }
    }

    private enum ReviewState
    {
        Empty,
        Normal,
        Valid,
        Invalid,
        Pending,
        Stale,
    }
}
