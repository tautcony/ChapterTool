using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class HistoryInspectionHeadlessTests
{
    [AvaloniaFact]
    public async Task Narrow_history_renders_root_chapters_and_filtered_changes_and_restores_all_rows()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Intro", "Credits")));
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Renamed intro"));

        using var viewModel = new HistoryToolViewModel(host.ViewModel.ToolSession.History, host.Localizer);
        var view = new HistoryToolView { DataContext = viewModel };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, viewModel, 520, 600);
        try
        {
            Assert.True(viewModel.IsNarrowLayout);
            var root = viewModel.Entries.Single(entry => entry.ParentId is null);
            var historyTree = Assert.IsType<TreeView>(view.FindControl<TreeView>("NarrowHistoryEntries"));
            historyTree.SelectedItem = root;
            Dispatcher.UIThread.RunJobs();
            await viewModel.RetryDetailsAsync();
            viewModel.ShowDetailsPage();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var rootChapters = Assert.IsType<ListBox>(view.FindControl<ListBox>("NarrowRootChapterList"));
            Assert.True(rootChapters.IsVisible);
            Assert.Equal(2, viewModel.RootChapterRows.Count);
            Assert.Equal("Intro", viewModel.RootChapterRows[0].Name);
            Assert.Equal("Credits", viewModel.RootChapterRows[1].Name);

            var changed = viewModel.Entries.Single(entry => entry.IsCurrent);
            historyTree.SelectedItem = changed;
            Dispatcher.UIThread.RunJobs();
            await viewModel.RetryDetailsAsync();
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Contains(viewModel.VisibleChanges, row => row.Field == host.Localizer.GetString("History.Field.Name")
                && row.AfterText == "Renamed intro");

            viewModel.SelectedChangeFilter = viewModel.ChangeFilters.Single(filter => filter == host.Localizer.GetString("History.Filter.Added"));
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Empty(viewModel.VisibleChanges);
            Assert.True(viewModel.HasNoVisibleChanges);
            var details = Assert.IsType<StackPanel>(view.FindControl<StackPanel>("NarrowHistoryDetails"));
            var emptyHint = Assert.Single(details.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == host.Localizer.GetString("History.NoFilteredChanges"));
            Assert.True(emptyHint.IsVisible);

            viewModel.SelectedChangeFilter = viewModel.ChangeFilters.Single(filter => filter == host.Localizer.GetString("History.Filter.All"));
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Single(viewModel.VisibleChanges);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.IsVisible && block.Text == host.Localizer.GetString("History.Field.Name"));

            historyTree.SelectedItem = root;
            Dispatcher.UIThread.RunJobs();
            await viewModel.RetryDetailsAsync();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var locate = Assert.IsType<Button>(view.FindControl<Button>("NarrowLocateCurrent"));
            await viewModel.LocateCurrentCommand.ExecuteAsync();
            locate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Equal(0, viewModel.NarrowPageIndex);
            Assert.Equal(changed.Id, viewModel.SelectedEntry!.Id);
            Assert.Equal(changed.Id, Assert.IsType<HistoryEntryViewModel>(historyTree.SelectedItem).Id);

            root.IsExpanded = true;
            var selectedId = viewModel.SelectedEntry.Id;
            window.Width = 1000;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.False(viewModel.IsNarrowLayout);
            Assert.Equal(selectedId, viewModel.SelectedEntry!.Id);
            Assert.Equal(selectedId, viewModel.Details!.NodeId);

            window.Width = 520;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.True(viewModel.IsNarrowLayout);
            Assert.True(root.IsExpanded);
            Assert.Equal(0, viewModel.NarrowPageIndex);
            Assert.Equal(selectedId, Assert.IsType<HistoryEntryViewModel>(historyTree.SelectedItem).Id);
        }
        finally
        {
            window.Close();
            await DrainUiAsync();
        }
    }

    [AvaloniaFact]
    public async Task Inspecting_an_alternate_node_preserves_pending_preview_and_explicit_redo_uses_its_choice()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "First branch"));
        var firstBranchId = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Nested first branch"));
        var nestedBranchId = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        await host.ViewModel.UndoCommand.ExecuteAsync();
        await host.ViewModel.UndoCommand.ExecuteAsync();
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Alternate branch"));
        var alternateChildId = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        await host.ViewModel.UndoCommand.ExecuteAsync();

        using var viewModel = new HistoryToolViewModel(host.ViewModel.ToolSession.History, host.Localizer);
        var view = new HistoryToolView { DataContext = viewModel };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, viewModel, 1000, 700);
        try
        {
            Assert.False(viewModel.IsNarrowLayout);
            var root = viewModel.TreeRoots.Single();
            Assert.Equal(root.Id, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
            root.IsExpanded = true;
            var branch = root.Children.Single(entry => entry.Id == firstBranchId);
            branch.IsExpanded = true;
            var inspected = branch.Children.Single(entry => entry.Id == nestedBranchId);

            var historyTree = Assert.IsType<TreeView>(view.FindControl<TreeView>("HistoryEntries"));
            Assert.True(historyTree.IsVisible && historyTree.IsEnabled);
            Assert.Contains(root.Children, entry => entry.Id == alternateChildId);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            Assert.True(host.ViewModel.CanApplyContentPreview);
            historyTree.SelectedItem = inspected;
            Dispatcher.UIThread.RunJobs();
            await viewModel.RetryDetailsAsync();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(host.ViewModel.CanApplyContentPreview);
            Assert.Equal("Intro", host.ViewModel.Rows[0].Name);
            Assert.Equal(inspected.Id, viewModel.Details!.NodeId);
            Assert.Equal(root.Id, host.ViewModel.ToolSession.History.Snapshot!.CursorId);

            var choices = Assert.IsType<ComboBox>(view.FindControl<ComboBox>(viewModel.IsNarrowLayout
                ? "NarrowRedoChoices"
                : "WideRedoChoices"));
            var alternateChoice = viewModel.RedoChoices.Single(choice => choice.Id == alternateChildId);
            choices.SelectedItem = alternateChoice;
            await viewModel.ChooseRedoBranchCommand.ExecuteAsync(alternateChoice.Id);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal(alternateChildId, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
            Assert.Equal(alternateChildId, viewModel.SelectedEntry!.Id);
            Assert.True(viewModel.SelectedEntry.IsCurrent);
            Assert.Equal("Alternate branch", host.ViewModel.Rows[0].Name);
            Assert.False(host.ViewModel.IsContentPreviewPending);
        }
        finally
        {
            window.Close();
            await DrainUiAsync();
        }
    }

    [AvaloniaFact]
    public async Task Rendered_retry_and_restore_actions_keep_selection_across_failure_and_discard_preview()
    {
        var history = new FailingHistoryPort();
        var localizer = new AppLocalizationManager("en-US");
        using var viewModel = new HistoryToolViewModel(history, localizer);
        var view = new HistoryToolView { DataContext = viewModel };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, viewModel, 520, 600);
        try
        {
            var root = viewModel.TreeRoots.Single();
            root.IsExpanded = true;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var child = root.Children.Single();
            var historyTree = Assert.IsType<TreeView>(view.FindControl<TreeView>("NarrowHistoryEntries"));
            historyTree.SelectedItem = child;
            Dispatcher.UIThread.RunJobs();
            viewModel.ShowDetailsPage();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(viewModel.HasDetailError);
            var details = Assert.IsType<StackPanel>(view.FindControl<StackPanel>("NarrowHistoryDetails"));
            var retry = Assert.Single(details.GetVisualDescendants().OfType<Button>(), button =>
                ReferenceEquals(button.Command, viewModel.RetryCommand));
            Assert.True(retry.IsVisible);

            var retryCommand = Assert.IsType<UiCommand>(retry.Command);
            await retryCommand.ExecuteAsync(retry.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Equal(child.Id, viewModel.Details!.NodeId);

            history.HasPendingPreview = true;
            history.RaiseStateChanged();
            Assert.Equal(child.Id, viewModel.SelectedEntry!.Id);
            Assert.Equal(child.Id, viewModel.Details!.NodeId);
            Assert.True(viewModel.HasPendingPreview);
            Assert.True(view.FindControl<TextBlock>("NarrowPendingPreviewNotice")!.IsVisible);

            var restore = Assert.IsType<Button>(view.FindControl<Button>("NarrowRestore"));
            await Assert.IsType<UiCommand>(restore.Command).ExecuteAsync(restore.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal(history.RootId, history.Snapshot!.CursorId);
            Assert.Equal(child.Id, viewModel.SelectedEntry!.Id);
            Assert.Equal(child.Id, viewModel.Details!.NodeId);
            Assert.False(viewModel.HasPendingPreview);
            Assert.NotNull(viewModel.NavigationStatus);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.IsVisible && block.Text == viewModel.NavigationStatus);

            history.NavigationFailure = null;
            history.HasPendingPreview = true;
            history.RaiseStateChanged();
            await Assert.IsType<UiCommand>(restore.Command).ExecuteAsync(restore.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(child.Id, history.Snapshot!.CursorId);
            Assert.Equal(child.Id, viewModel.SelectedEntry!.Id);
            Assert.True(viewModel.SelectedEntry.IsCurrent);
            Assert.False(viewModel.HasPendingPreview);
        }
        finally
        {
            window.Close();
            await DrainUiAsync();
        }
    }

    private static async Task DrainUiAsync()
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FailingHistoryPort : IHistorySessionPort
    {
        private readonly Guid childId = Guid.NewGuid();
        private readonly Queue<HistoryInspectionOutcome> inspections = new();

        public Guid RootId { get; } = Guid.NewGuid();

        public SessionHistorySnapshot? Snapshot { get; private set; }

        public bool HasPendingPreview { get; set; } = true;

        public bool IsEnded => false;

        public string NavigationStatus => string.Empty;

        public bool CanUndo => Snapshot?.CursorId != RootId;

        public bool CanRedo => Snapshot is { } snapshot
            && (snapshot.Nodes.FirstOrDefault(node => node.Id == snapshot.CursorId)?.ChildIds.Length ?? 0) > 0;

        public string UndoDescription => "Undo";

        public string RedoDescription => "Redo";

        public IReadOnlyList<HistoryEntryViewModel> Entries => [];

        public UiCommand UndoCommand { get; } = new((_, _) => ValueTask.CompletedTask);

        public UiCommand RedoCommand { get; } = new((_, _) => ValueTask.CompletedTask);

        public UiCommand NavigateCommand { get; }

        public event EventHandler? StateChanged;

        public FailingHistoryPort()
        {
            Snapshot = new SessionHistorySnapshot(RootId, RootId,
            [
                new SessionHistoryNodeSnapshot(RootId, null, [childId], childId, "root", new HistoryOperationDescriptor("load", [])),
                new SessionHistoryNodeSnapshot(childId, RootId, [], null, "alternate", new HistoryOperationDescriptor("chapter.cell-edit", []))
            ]);
            inspections.Enqueue(Available(RootId, RootId, "root details"));
            inspections.Enqueue(new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.ResourceFailure, null, []));
            inspections.Enqueue(Available(RootId, childId, "child details"));
            NavigateCommand = new UiCommand(NavigateAsync, _ => true);
        }

        public ValueTask<HistoryInspectionOutcome> InspectAsync(Guid nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inspections.TryDequeue(out var outcome))
            {
                return ValueTask.FromResult(outcome);
            }
            return ValueTask.FromResult(Available(RootId, nodeId, nodeId == RootId ? "root details" : "child details"));
        }

        public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private ValueTask NavigateAsync(object? parameter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasPendingPreview = false;
            if (NavigationFailure is not null)
            {
                RaiseStateChanged();
                return ValueTask.FromException(NavigationFailure);
            }
            if (parameter is Guid target && Snapshot is { } snapshot)
            {
                Snapshot = snapshot with { CursorId = target };
            }
            RaiseStateChanged();
            return ValueTask.CompletedTask;
        }

        public Exception? NavigationFailure { get; set; } = new InvalidOperationException("Navigation failed");

        private static HistoryInspectionOutcome Available(Guid rootId, Guid nodeId, string description) =>
            new(HistoryInspectionOutcomeKind.Available,
                new HistoryNodeDetails(rootId, nodeId, nodeId == rootId ? null : rootId, description, null, null,
                    new HistoryNodeSummary(0, 0, 0, 0, 0), [], null), []);
    }
}
