using System.Collections.Immutable;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class HistoryInspectionViewModelTests
{
    [Fact]
    public async Task Selecting_a_retained_node_only_inspects_and_preserves_pending_preview()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot) { HasPendingPreview = true };
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;

        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;

        Assert.Equal(tree.Current, history.Snapshot!.CursorId);
        Assert.True(viewModel.HasPendingPreview);
        Assert.Equal(tree.Left, viewModel.SelectedEntry!.Id);
        Assert.Empty(history.NavigatedTo);
        Assert.Equal(tree.Left, history.Snapshot!.Nodes.Single(node => node.Id == tree.Root).PreferredChildId);
    }

    [Fact]
    public async Task Same_cursor_state_change_preserves_inspected_selection_and_loaded_details()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot) { HasPendingPreview = true };
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Right);
        await history.LastInspection;
        var loadedDetails = viewModel.Details;

        history.HasPendingPreview = false;
        history.RaiseStateChanged();

        Assert.Equal(tree.Current, history.Snapshot!.CursorId);
        Assert.Equal(tree.Right, viewModel.SelectedEntry!.Id);
        Assert.Same(loadedDetails, viewModel.Details);
        Assert.False(viewModel.HasPendingPreview);
    }

    [Fact]
    public async Task Collapsing_a_native_tree_node_preserves_children_and_locate_current_expands_its_path()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;

        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Right);
        await history.LastInspection;
        var beforeCollapseCursor = history.Snapshot!.CursorId;
        Assert.True(viewModel.Entries.Single(entry => entry.Id == tree.Root).IsExpanded);

        await viewModel.ToggleExpansionCommand.ExecuteAsync(tree.Root);

        Assert.Equal(beforeCollapseCursor, history.Snapshot!.CursorId);
        Assert.Equal(tree.Root, viewModel.SelectedEntry!.Id);
        var root = Assert.Single(viewModel.TreeRoots);
        Assert.False(root.IsExpanded);
        Assert.Equal(2, root.Children.Count);

        await viewModel.LocateCurrentCommand.ExecuteAsync();

        Assert.Equal(tree.Current, viewModel.SelectedEntry!.Id);
        Assert.Contains(viewModel.Entries, entry => entry.Id == tree.Current && entry.IsCurrent && entry.IsCurrentPath);
        Assert.Equal(tree.Current, history.Snapshot!.CursorId);
    }

    [Fact]
    public async Task Long_linear_history_is_a_hierarchical_tree_with_stable_children()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray();
        var nodes = ids.Select((id, index) => new SessionHistoryNodeSnapshot(
            id,
            index == 0 ? null : ids[index - 1],
            index + 1 == ids.Length ? [] : [ids[index + 1]],
            index + 1 == ids.Length ? null : ids[index + 1],
            $"Node {index}")).ToImmutableArray();
        var history = new FakeHistoryPort(new SessionHistorySnapshot(ids[0], ids[^1], nodes));
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;

        Assert.Equal(1000, viewModel.Entries.Count);
        var node = Assert.Single(viewModel.TreeRoots);
        for (var index = 0; index < ids.Length - 1; index++)
        {
            Assert.Equal(ids[index], node.Id);
            node = Assert.Single(node.Children);
        }
        Assert.Equal(ids[^1], node.Id);
        Assert.Empty(node.Children);
        Assert.Equal(ids[^1], viewModel.Entries[^1].Id);
        Assert.True(viewModel.Entries[^1].IsCurrent);
    }

    [Fact]
    public async Task Obsolete_detail_completion_is_discarded_after_a_quick_selection_change()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        history.QueuePendingInspection();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        var obsolete = history.WaitForInspectionAsync(0);
        history.QueuePendingInspection();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Right);
        var current = history.WaitForInspectionAsync(1);

        obsolete.SetResult(FakeHistoryPort.Available(tree.Root, tree.Left, "obsolete"));
        current.SetResult(FakeHistoryPort.Available(tree.Root, tree.Right, "current"));
        await WaitForAsync(() => viewModel.Details?.NodeId == tree.Right);

        Assert.Equal(tree.Right, viewModel.SelectedEntry!.Id);
        Assert.Equal(tree.Right, viewModel.Details!.NodeId);
        Assert.Equal("current", viewModel.Details.Description);
    }

    [Fact]
    public async Task Inspection_error_can_be_retried_without_disabling_restore()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        history.EnqueueOutcome(new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.ResourceFailure, null, []));
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;

        Assert.Equal("ResourceFailure", viewModel.DetailError);
        Assert.True(viewModel.RetryCommand.CanExecute());
        Assert.True(viewModel.RestoreCommand.CanExecute(tree.Left));
        await viewModel.RetryCommand.ExecuteAsync();

        Assert.Null(viewModel.DetailError);
        Assert.Equal(tree.Left, viewModel.Details!.NodeId);
    }

    [Fact]
    public async Task Restore_is_explicit_selects_the_result_and_preserves_selection_on_failure()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        Assert.False(viewModel.RestoreCommand.CanExecute(tree.Current));
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;

        Assert.True(viewModel.RestoreCommand.CanExecute(tree.Left));
        await viewModel.RestoreCommand.ExecuteAsync(tree.Left);

        Assert.Equal(tree.Left, history.Snapshot!.CursorId);
        Assert.Equal(tree.Left, viewModel.SelectedEntry!.Id);
        Assert.Equal(new[] { tree.Left }, history.NavigatedTo);

        history.ReplaceSnapshot(tree.Snapshot);
        history.RaiseStateChanged();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;
        history.NavigationFailure = new InvalidOperationException("navigation failed");
        await viewModel.RestoreCommand.ExecuteAsync(tree.Left);

        Assert.Equal(tree.Current, history.Snapshot!.CursorId);
        Assert.Equal(tree.Left, viewModel.SelectedEntry!.Id);
        Assert.NotNull(viewModel.NavigationStatus);
    }

    [Fact]
    public async Task Redo_choices_are_direct_children_and_inspection_does_not_change_preferred_edge()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot with { CursorId = tree.Root });
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        var preferred = Assert.Single(viewModel.RedoChoices, choice => choice.IsPreferred);
        Assert.Equal(tree.Left, preferred.Id);
        Assert.Equal(2, viewModel.RedoChoices.Count);
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Right);
        await history.LastInspection;

        Assert.Equal(tree.Left, history.Snapshot!.Nodes.Single(node => node.Id == tree.Root).PreferredChildId);
        Assert.True(viewModel.ChooseRedoBranchCommand.CanExecute(tree.Right));
        await viewModel.ChooseRedoBranchCommand.ExecuteAsync(tree.Right);

        Assert.Equal(tree.Right, history.Snapshot!.CursorId);
        Assert.Equal(tree.Right, viewModel.SelectedEntry!.Id);
    }

    [Fact]
    public async Task Locale_changes_relabel_rows_and_details_without_changing_node_identity()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        var localizer = new AppLocalizationManager("en-US");
        history.SetDetails(tree.Left, new HistoryNodeDetails(tree.Root, tree.Left, tree.Root, "left",
            new HistoryOperationDescriptor("chapter.cell-edit", [new HistoryOperationParameter("frameDisplay", "Round")]),
            DateTimeOffset.UnixEpoch, new HistoryNodeSummary(0, 0, 1, 0, 0),
            [Change("Name", new HistoryFieldValue(true, "Before"), new HistoryFieldValue(true, "After"))], null));
        using var viewModel = new HistoryToolViewModel(history, localizer);
        await history.LastInspection;
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;
        var selectedId = viewModel.SelectedEntry!.Id;
        var englishTitle = viewModel.Entries.Single(entry => entry.Id == tree.Left).Title;
        var englishField = Assert.Single(viewModel.VisibleChanges).Field;
        var englishParameter = Assert.Single(viewModel.Parameters).Name;

        localizer.SetCulture("zh-CN");

        Assert.Equal(selectedId, viewModel.SelectedEntry!.Id);
        Assert.Equal(tree.Current, viewModel.Entries.Single(entry => entry.IsCurrent).Id);
        Assert.NotEqual(englishTitle, viewModel.Entries.Single(entry => entry.Id == tree.Left).Title);
        Assert.NotEqual(englishField, Assert.Single(viewModel.VisibleChanges).Field);
        Assert.NotEqual(englishParameter, Assert.Single(viewModel.Parameters).Name);
        Assert.Equal(localizer.GetString("History.Filter.All"), viewModel.SelectedChangeFilter);
    }

    [Fact]
    public async Task Inspector_formats_presence_time_delta_enum_values_and_typed_parameters()
    {
        var tree = ForkedTree();
        var changes = ImmutableArray.Create(
            Change("StartTicks", new HistoryFieldValue(true, 0L), new HistoryFieldValue(true, TimeSpan.TicksPerSecond + 5L)),
            Change("Name", new HistoryFieldValue(false, null), new HistoryFieldValue(true, string.Empty)),
            Change("Kind", new HistoryFieldValue(true, ChapterKind.Marker), new HistoryFieldValue(true, ChapterKind.Separator)),
            Change("FrameAccuracy", new HistoryFieldValue(true, FrameAccuracy.Neutral), new HistoryFieldValue(true, FrameAccuracy.Inexact)),
            Change("ImportFormat", new HistoryFieldValue(true, ChapterImportFormat.Ogm), new HistoryFieldValue(true, ChapterImportFormat.WebVtt)));
        var parameters = ImmutableArray.Create(new HistoryOperationParameter("frameDisplay", "DecimalPlaces"));
        var history = new FakeHistoryPort(tree.Snapshot);
        history.SetDetails(tree.Left, new HistoryNodeDetails(tree.Root, tree.Left, tree.Root, "edit",
            new HistoryOperationDescriptor("chapter.cell-edit", parameters), DateTimeOffset.UnixEpoch,
            new HistoryNodeSummary(0, 0, 1, 0, 0), changes, null));
        var localizer = new AppLocalizationManager("en-US");
        using var viewModel = new HistoryToolViewModel(history, localizer);
        await history.LastInspection;
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;

        var rows = viewModel.VisibleChanges.ToDictionary(row => row.Field, StringComparer.Ordinal);
        Assert.Equal("00:00:00.000", rows["Start time"].BeforeText);
        Assert.Equal("+00:00:01.000", rows["Start time"].DeltaText);
        Assert.Equal("Not present", rows["Name"].BeforeText);
        Assert.Equal("Empty", rows["Name"].AfterText);
        Assert.Equal("Marker", rows["Type"].BeforeText);
        Assert.Equal("Separator", rows["Type"].AfterText);
        Assert.Equal("Not calculated", rows["Frame accuracy"].BeforeText);
        Assert.Equal("Between frames", rows["Frame accuracy"].AfterText);
        Assert.Equal("OGM", rows["Import format"].BeforeText);
        Assert.Equal("WebVTT", rows["Import format"].AfterText);
        Assert.Equal("Decimal frames", Assert.Single(viewModel.Parameters).Value);

        viewModel.SelectedChangeFilter = localizer.GetString("History.Filter.Removed");
        Assert.True(viewModel.HasChanges);
        Assert.False(viewModel.HasVisibleChanges);
        Assert.True(viewModel.HasNoVisibleChanges);
        viewModel.SelectedChangeFilter = localizer.GetString("History.Filter.All");
        Assert.True(viewModel.HasVisibleChanges);
        Assert.Equal(tree.Current, history.Snapshot!.CursorId);
    }

    [Theory]
    [InlineData("Frame")]
    [InlineData("StartTime")]
    public async Task Inspector_keeps_captured_display_rate_for_frame_and_time_edits_only(string field)
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        history.SetDetails(tree.Left, CellEditDetails(tree.Root, tree.Left, field, "30"));
        history.SetDetails(tree.Right, CellEditDetails(tree.Root, tree.Right, "Name", "30"));
        var localizer = new AppLocalizationManager("en-US");
        using var viewModel = new HistoryToolViewModel(history, localizer);
        await history.LastInspection;

        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        await history.LastInspection;
        var displayRate = Assert.Single(viewModel.Parameters);
        Assert.Equal(localizer.GetString("History.Parameter.displayFps"), displayRate.Name);
        Assert.Equal("30", displayRate.Value);

        var capturedDetails = viewModel.Details;
        history.HasPendingPreview = true;
        history.RaiseStateChanged();
        Assert.Same(capturedDetails, viewModel.Details);
        Assert.Equal("30", Assert.Single(viewModel.Parameters).Value);

        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Right);
        await history.LastInspection;
        Assert.Empty(viewModel.Parameters);
    }

    private static HistoryNodeDetails CellEditDetails(Guid rootId, Guid nodeId, string field, string displayRate) =>
        new(rootId, nodeId, rootId, "Edit chapter",
            new HistoryOperationDescriptor("chapter.cell-edit",
            [
                new HistoryOperationParameter("field", field),
                new HistoryOperationParameter("value", "captured value"),
                new HistoryOperationParameter("trackName", "Track"),
                new HistoryOperationParameter("displayFps", displayRate, "decimal")
            ]), DateTimeOffset.UnixEpoch, new HistoryNodeSummary(0, 0, 0, 0, 0), [], null);

    [Fact]
    public async Task Root_inspection_exposes_ordered_read_only_chapter_values()
    {
        var tree = ForkedTree();
        var document = CreateDocument("First", "Second");
        var history = new FakeHistoryPort(tree.Snapshot with { CursorId = tree.Root });
        history.SetDetails(tree.Root, new HistoryNodeDetails(tree.Root, tree.Root, null, "initial",
            null, null, new HistoryNodeSummary(0, 0, 0, 0, 0), [], document));
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;

        Assert.True(viewModel.IsRootDetails);
        Assert.Equal(new[] { "First", "Second" }, viewModel.RootChapterRows.Select(row => row.Name).ToArray());
        Assert.Equal(new[] { 1, 2 }, viewModel.RootChapterRows.Select(row => row.DisplayNumber).ToArray());
        Assert.All(viewModel.RootChapterRows, row => Assert.Equal("Track", row.TrackName));
        Assert.False(viewModel.RestoreCommand.CanExecute(tree.Root));
    }

    [Fact]
    public async Task Session_end_and_disposal_discard_pending_detail_results()
    {
        var tree = ForkedTree();
        var history = new FakeHistoryPort(tree.Snapshot);
        var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        history.QueuePendingInspection();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        var pending = history.WaitForInspectionAsync(0);

        history.IsEnded = true;
        history.ReplaceSnapshot(null);
        history.RaiseStateChanged();
        pending.SetResult(FakeHistoryPort.Available(tree.Root, tree.Left, "old session"));
        await history.LastInspection;

        Assert.Null(viewModel.SelectedEntry);
        Assert.Null(viewModel.Details);
        Assert.Empty(viewModel.Entries);

        history.IsEnded = false;
        history.ReplaceSnapshot(tree.Snapshot);
        history.RaiseStateChanged();
        history.QueuePendingInspection();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == tree.Left);
        var disposedRequest = history.WaitForInspectionAsync(1);
        viewModel.Dispose();
        disposedRequest.SetResult(FakeHistoryPort.Available(tree.Root, tree.Left, "after dispose"));
        await history.LastInspection;

        Assert.Null(viewModel.Details);
    }

    [Fact]
    public async Task Detail_from_previous_root_is_discarded_when_the_session_root_changes()
    {
        var oldTree = ForkedTree();
        var newTree = ForkedTree();
        var history = new FakeHistoryPort(oldTree.Snapshot);
        using var viewModel = new HistoryToolViewModel(history, new AppLocalizationManager("en-US"));
        await history.LastInspection;
        history.QueuePendingInspection();
        viewModel.SelectedEntry = viewModel.Entries.Single(entry => entry.Id == oldTree.Left);
        var oldRequest = history.WaitForInspectionAsync(0);

        history.ReplaceSnapshot(newTree.Snapshot);
        history.RaiseStateChanged();
        oldRequest.SetResult(FakeHistoryPort.Available(oldTree.Root, oldTree.Left, "previous root"));
        await history.LastInspection;

        Assert.Equal(newTree.Current, viewModel.SelectedEntry!.Id);
        Assert.Equal(newTree.Current, viewModel.Details!.NodeId);
        Assert.NotEqual("previous root", viewModel.Details.Description);
    }

    private static HistoryFieldChange Change(string field, HistoryFieldValue before, HistoryFieldValue after) =>
        new("Chapter", "chapter-1", field, before, after, "Chapter 1");

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 1000 && !condition(); attempt++)
        {
            await Task.Yield();
        }
        Assert.True(condition(), "The asynchronous ViewModel update did not complete.");
    }

    private static EditableChapterDocument CreateDocument(params string[] names)
    {
        var chapters = names.Select((name, index) => new EditableChapter(
            ChapterId.New(), index + 1, index * TimeSpan.TicksPerSecond, name)).ToArray();
        return new EditableChapterDocument(ChapterDocumentId.New(), "Title", "source.txt", ChapterImportFormat.Ogm,
            ChapterDuration.Unknown, new ChapterFrameRate(24, 1), [new EditableChapterTrack(ChapterTrackId.New(), "Track", chapters)]);
    }

    private static (SessionHistorySnapshot Snapshot, Guid Root, Guid Left, Guid Right, Guid Current) ForkedTree()
    {
        var root = Guid.NewGuid();
        var left = Guid.NewGuid();
        var right = Guid.NewGuid();
        var current = Guid.NewGuid();
        var nodes = ImmutableArray.Create(
            new SessionHistoryNodeSnapshot(root, null, [left, right], left, "root", new HistoryOperationDescriptor("load", [])),
            new SessionHistoryNodeSnapshot(left, root, [current], current, "left", new HistoryOperationDescriptor("chapter.cell-edit", [])),
            new SessionHistoryNodeSnapshot(current, left, [], null, "current", new HistoryOperationDescriptor("chapter.frame-shift", [])),
            new SessionHistoryNodeSnapshot(right, root, [], null, "right", new HistoryOperationDescriptor("workspace.merge", [])));
        return (new SessionHistorySnapshot(root, current, nodes), root, left, right, current);
    }

    private sealed class FakeHistoryPort : IHistorySessionPort
    {
        private readonly List<TaskCompletionSource<HistoryInspectionOutcome>> pending = [];
        private readonly Queue<HistoryInspectionOutcome> outcomes = new();
        private readonly Dictionary<Guid, HistoryNodeDetails> details = [];
        private readonly Dictionary<Guid, HistoryNodeDetails> configuredDetails = [];
        private int nextPendingInspection;
        private Task lastInspection = Task.CompletedTask;

        public IReadOnlyList<HistoryEntryViewModel> Entries => [];

        public bool CanUndo => Snapshot?.CursorId != Snapshot?.RootId;

        public bool CanRedo => Snapshot is { } snapshot
            && snapshot.Nodes.FirstOrDefault(node => node.Id == snapshot.CursorId)?.ChildIds.Length > 0;

        public string UndoDescription => "Undo";

        public string RedoDescription => "Redo";

        public UiCommand UndoCommand { get; } = new((_, _) => ValueTask.CompletedTask);

        public UiCommand RedoCommand { get; } = new((_, _) => ValueTask.CompletedTask);

        public UiCommand NavigateCommand { get; }

        public SessionHistorySnapshot? Snapshot { get; private set; }

        public bool HasPendingPreview { get; set; }

        public bool IsEnded { get; set; }

        public string NavigationStatus { get; set; } = string.Empty;

        public List<Guid> NavigatedTo { get; } = [];

        public Exception? NavigationFailure { get; set; }

        public Task LastInspection => lastInspection;

        public event EventHandler? StateChanged;

        public FakeHistoryPort(SessionHistorySnapshot? snapshot = null)
        {
            Snapshot = snapshot;
            NavigateCommand = new UiCommand(NavigateAsync, _ => !IsEnded);
        }

        public ValueTask<HistoryInspectionOutcome> InspectAsync(Guid nodeId, CancellationToken cancellationToken)
        {
            if (nextPendingInspection < pending.Count)
            {
                var request = pending[nextPendingInspection++];
                lastInspection = request.Task;
                return new ValueTask<HistoryInspectionOutcome>(request.Task);
            }

            var result = outcomes.Count > 0
                ? outcomes.Dequeue()
                : HistoryInspectionOutcomeFor(nodeId);
            lastInspection = Task.CompletedTask;
            return ValueTask.FromResult(result);
        }

        public void QueuePendingInspection() => pending.Add(new TaskCompletionSource<HistoryInspectionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));

        public TaskCompletionSource<HistoryInspectionOutcome> WaitForInspectionAsync(int index) => pending[index];

        public void EnqueueOutcome(HistoryInspectionOutcome outcome) => outcomes.Enqueue(outcome);

        public void SetDetails(Guid id, HistoryNodeDetails detail) => configuredDetails[id] = detail;

        public void ReplaceSnapshot(SessionHistorySnapshot? value)
        {
            Snapshot = value;
        }

        public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private HistoryInspectionOutcome HistoryInspectionOutcomeFor(Guid nodeId)
        {
            if (configuredDetails.TryGetValue(nodeId, out var configured))
            {
                return new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.Available, configured, []);
            }
            if (details.TryGetValue(nodeId, out var existing))
            {
                return new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.Available, existing, []);
            }
            var rootId = Snapshot?.RootId ?? Guid.Empty;
            var node = Snapshot?.Nodes.FirstOrDefault(item => item.Id == nodeId);
            if (node is null)
            {
                return new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.NotFound, null, []);
            }
            var fields = node.ParentId is null
                ? ImmutableArray<HistoryFieldChange>.Empty
                : ImmutableArray.Create(Change("Name", new HistoryFieldValue(true, "Before"), new HistoryFieldValue(true, "After")));
            var detail = new HistoryNodeDetails(rootId, nodeId, node.ParentId, node.Description, node.Operation,
                node.PublishedAt, new HistoryNodeSummary(0, 0, fields.Length, 0, 0), fields, null);
            details[nodeId] = detail;
            return new HistoryInspectionOutcome(HistoryInspectionOutcomeKind.Available, detail, []);
        }

        private ValueTask NavigateAsync(object? parameter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NavigationFailure is not null)
            {
                return ValueTask.FromException(NavigationFailure);
            }
            if (parameter is Guid target && Snapshot is { } current)
            {
                NavigatedTo.Add(target);
                Snapshot = current with { CursorId = target };
                RaiseStateChanged();
            }
            return ValueTask.CompletedTask;
        }

        public static HistoryInspectionOutcome Available(Guid rootId, Guid nodeId, string description) =>
            new(HistoryInspectionOutcomeKind.Available,
                new HistoryNodeDetails(rootId, nodeId, null, description, null, null,
                    new HistoryNodeSummary(0, 0, 0, 0, 0), [], null), []);
    }
}
