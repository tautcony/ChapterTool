using System.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Core.Tests.Session;

public sealed class SessionEditHistoryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Undo_redo_and_branch_selection_restore_exact_values_and_identities()
    {
        var initial = CreateDocument([CreateChapter(1, "A"), CreateChapter(2, "B")]);
        var firstChapterId = initial.Tracks[0].Chapters[0].Id;
        var secondChapterId = initial.Tracks[0].Chapters[1].Id;
        var session = new SessionState(initial);
        var root = session.GetHistorySnapshot();
        Assert.Single(root.Nodes);
        Assert.Equal(root.RootId, root.CursorId);
        Assert.Equal("load", root.Nodes.Single().Operation?.Kind);
        Assert.NotNull(root.Nodes.Single().PublishedAt);

        var first = await CommitAsync(session, document => ChangeChapter(document, 0, name: "First"), "Rename first");
        var firstNode = session.GetHistorySnapshot().CursorId;
        var second = await CommitAsync(session, document => ChangeChapter(document, 1, name: "Second"), "Rename second");
        var secondNode = session.GetHistorySnapshot().CursorId;
        Assert.Equal(2, second.Snapshot.MutationRevision);

        var undoSecond = await session.UndoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, undoSecond.Kind);
        AssertDocument(first.Snapshot.Document, undoSecond.Snapshot.Document);
        Assert.Equal(firstChapterId, undoSecond.Snapshot.Document.Tracks[0].Chapters[0].Id);
        Assert.Equal(secondChapterId, undoSecond.Snapshot.Document.Tracks[0].Chapters[1].Id);

        var alternate = await CommitAsync(session, document => ChangeChapter(document, 0, name: "Alternate"), "Alternate first name");
        var alternateNode = session.GetHistorySnapshot().CursorId;
        Assert.Equal(4, alternate.Snapshot.MutationRevision);

        var undoAlternate = await session.UndoAsync();
        var redoPreferred = await session.RedoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, undoAlternate.Kind);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, redoPreferred.Kind);
        Assert.Equal("Alternate", redoPreferred.Snapshot.Document.Tracks[0].Chapters[0].Name);

        _ = await session.UndoAsync();
        var selectedOriginal = await session.SelectRedoBranchAsync(secondNode);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, selectedOriginal.Kind);
        Assert.Equal("First", selectedOriginal.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal("Second", selectedOriginal.Snapshot.Document.Tracks[0].Chapters[1].Name);
        Assert.True(selectedOriginal.Snapshot.MutationRevision > alternate.Snapshot.MutationRevision);

        var tree = session.GetHistorySnapshot();
        Assert.Equal(4, tree.Nodes.Length);
        Assert.Equal(2, tree.Nodes.Single(node => node.Id == firstNode).ChildIds.Length);
        Assert.Equal("Alternate first name", tree.Nodes.Single(node => node.Id == alternateNode).Description);
        Assert.Contains(tree.Nodes, node => node.Id == alternateNode);
        Assert.Contains(tree.Nodes, node => node.Id == secondNode);
        Assert.Equal(tree.CursorId, selectedOriginal.CursorId);

        var cursorBeforeInspection = session.GetHistorySnapshot().CursorId;
        var alternateDetails = await session.InspectHistoryNodeAsync(alternateNode);
        Assert.Equal(HistoryInspectionOutcomeKind.Available, alternateDetails.Kind);
        Assert.Equal("First", Assert.Single(alternateDetails.Details!.Changes.Where(change => change.Field == "Name")).Before.Value);
        Assert.Equal("Alternate", Assert.Single(alternateDetails.Details.Changes.Where(change => change.Field == "Name")).After.Value);
        Assert.Equal(cursorBeforeInspection, session.GetHistorySnapshot().CursorId);

        var crossBranch = await session.NavigateToAsync(alternateNode);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, crossBranch.Kind);
        Assert.Equal("Alternate", crossBranch.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal("B", crossBranch.Snapshot.Document.Tracks[0].Chapters[1].Name);
    }

    [Fact]
    public async Task Inspection_returns_parent_relative_values_without_moving_the_cursor()
    {
        var initial = CreateDocument([CreateChapter(1, "A")]);
        var session = new SessionState(initial);
        var root = session.GetHistorySnapshot();
        var commit = await CommitAsync(session, document => ChangeChapter(document, 0, name: "B"), "Rename");
        var nodeId = session.GetHistorySnapshot().CursorId;
        var before = session.Snapshot;
        var preferred = session.GetHistorySnapshot().Nodes.Single(node => node.Id == root.RootId).PreferredChildId;

        var details = await session.InspectHistoryNodeAsync(nodeId);

        Assert.Equal(HistoryInspectionOutcomeKind.Available, details.Kind);
        Assert.Equal("A", Assert.Single(details.Details!.Changes.Where(change => change.Group == "Chapter" && change.Field == "Name")).Before.Value);
        Assert.Equal("B", Assert.Single(details.Details.Changes.Where(change => change.Group == "Chapter" && change.Field == "Name")).After.Value);
        Assert.Equal(1, details.Details.Summary.ModifiedChapters);
        Assert.DoesNotContain(details.Details.Changes, change => change.Field is "TrackOrder" or "ChapterOrder" or "SegmentOrder");
        Assert.Equal(before, session.Snapshot);
        Assert.Equal(preferred, session.GetHistorySnapshot().Nodes.Single(node => node.Id == root.RootId).PreferredChildId);
        AssertDocument(commit.Snapshot.Document, session.Snapshot.Document);

        var rootDetails = await session.InspectHistoryNodeAsync(root.RootId);
        Assert.Equal(initial, rootDetails.Details!.RootDocument);
        Assert.Empty(rootDetails.Details.Changes);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await session.InspectHistoryNodeAsync(nodeId, cancellation.Token);
        Assert.Equal(HistoryInspectionOutcomeKind.Cancelled, cancelled.Kind);
    }

    [Fact]
    public async Task Operation_metadata_is_published_only_with_a_committed_history_node()
    {
        var session = new SessionState(CreateDocument([CreateChapter(1, "A")]));
        var root = session.GetHistorySnapshot();
        var before = session.Snapshot;
        var noChange = await session.ExecuteAsync(before.BaseToken, Guid.NewGuid(), "No change",
            (document, _) => ValueTask.FromResult(document), operationDescription: "No change",
            operation: new HistoryOperationDescriptor("test.no-change", [new HistoryOperationParameter("value", "discarded")]));
        Assert.Equal(TransactionOutcomeKind.NoChange, noChange.Kind);
        Assert.Single(session.GetHistorySnapshot().Nodes);

        var descriptor = new HistoryOperationDescriptor("chapter.rename", [new HistoryOperationParameter("field", "Name")]);
        var committed = await session.ExecuteAsync(before.BaseToken, Guid.NewGuid(), "Rename",
            (document, _) => ValueTask.FromResult(ChangeChapter(document, 0, name: "B")),
            operationDescription: "Rename", operation: descriptor);
        Assert.Equal(TransactionOutcomeKind.Committed, committed.Kind);
        var node = session.GetHistorySnapshot().Nodes.Single(item => item.Id == session.GetHistorySnapshot().CursorId);
        Assert.Equal(descriptor, node.Operation);
        Assert.NotNull(node.PublishedAt);
        Assert.NotNull(root.Nodes.Single().PublishedAt);

        var stale = await session.ExecuteAsync(before.BaseToken, Guid.NewGuid(), "Stale",
            (document, _) => ValueTask.FromResult(ChangeChapter(document, 0, name: "C")),
            operationDescription: "Stale", operation: new HistoryOperationDescriptor("test.stale", []));
        Assert.Equal(TransactionOutcomeKind.Conflict, stale.Kind);
        Assert.Equal(2, session.GetHistorySnapshot().Nodes.Length);
    }

    [Fact]
    public async Task Generated_edits_and_navigation_preserve_every_committed_state()
    {
        var session = new SessionState(CreateDocument([CreateChapter(1, "0")]));
        var expected = new List<string> { "0" };
        var random = new Random(71423);
        for (var index = 1; index <= 80; index++)
        {
            var name = $"edit-{index}-{random.Next(100000)}";
            await CommitAsync(session, document => ChangeChapter(document, 0, name: name), $"Generated edit {index}");
            expected.Add(name);
            if (index % 7 == 0)
            {
                var undo = await session.UndoAsync();
                Assert.Equal(HistoryNavigationOutcomeKind.Committed, undo.Kind);
                Assert.Equal(expected[^2], undo.Snapshot.Document.Tracks[0].Chapters[0].Name);
                var redo = await session.RedoAsync();
                Assert.Equal(HistoryNavigationOutcomeKind.Committed, redo.Kind);
                Assert.Equal(name, redo.Snapshot.Document.Tracks[0].Chapters[0].Name);
            }
        }

        var tree = session.GetHistorySnapshot();
        Assert.Equal(expected.Count, tree.Nodes.Length);
        Assert.Equal(expected[^1], session.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal(80 + ((80 / 7) * 2), session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Deep_history_is_retained_and_navigation_uses_iterative_traversal()
    {
        const int editCount = 1200;
        var session = new SessionState(CreateDocument([CreateChapter(1, "0")]));
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        for (var index = 1; index <= editCount; index++)
        {
            await CommitAsync(session, document => ChangeChapter(document, 0, name: index.ToString()), $"Edit {index}");
        }

        var commitMilliseconds = stopwatch.ElapsedMilliseconds;
        var commitAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var retained = session.GetHistorySnapshot();
        Assert.Equal(editCount + 1, retained.Nodes.Length);
        Assert.Equal(editCount + 1, await CountByUndoAsync(session));
        stopwatch.Restart();
        var leaf = await session.NavigateToAsync(retained.Nodes[^1].Id);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, leaf.Kind);
        var allocationBeforeInspection = GC.GetTotalAllocatedBytes(precise: true);
        stopwatch.Restart();
        var leafDetails = await session.InspectHistoryNodeAsync(retained.Nodes[^1].Id);
        var inspectionTicks = stopwatch.ElapsedTicks;
        var inspectionAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocationBeforeInspection;
        Assert.Equal(HistoryInspectionOutcomeKind.Available, leafDetails.Kind);
        Assert.Equal(1, leafDetails.Details!.Summary.ModifiedChapters);
        stopwatch.Restart();
        var root = await session.NavigateToAsync(retained.RootId);
        var navigationTicks = stopwatch.ElapsedTicks;
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, root.Kind);
        Assert.Equal("0", root.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal(editCount + 1, session.GetHistorySnapshot().Nodes.Length);
        var totalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var measurement = $"History measurement: edits={editCount}; nodes={retained.Nodes.Length}; commitAllocatedBytes={commitAllocatedBytes}; fullSequenceAllocatedBytes={totalAllocatedBytes}; allocatedBytesPerCommand={totalAllocatedBytes / ((editCount * 2) + 2)}; commitMs={commitMilliseconds}; detailInspectionStopwatchTicks={inspectionTicks}; detailInspectionAllocatedBytes={inspectionAllocatedBytes}; rootNavigationStopwatchTicks={navigationTicks}; stopwatchFrequency={Stopwatch.Frequency}.";
        output.WriteLine(measurement);
    }

    [Fact]
    public async Task History_scale_measurements_report_document_size_and_retained_branches()
    {
        const int editsPerBranch = 32;
        foreach (var chapterCount in new[] { 1, 100, 1000 })
        {
            foreach (var branchCount in new[] { 1, 4, 8 })
            {
                var chapters = Enumerable.Range(0, chapterCount)
                    .Select(index => CreateChapter(index + 1, $"Chapter {index}"))
                    .ToArray();
                var session = new SessionState(CreateDocument(chapters));
                var rootId = session.GetHistorySnapshot().RootId;
                var branchLeaves = new List<Guid>(branchCount);
                var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
                var stopwatch = Stopwatch.StartNew();

                for (var branchIndex = 0; branchIndex < branchCount; branchIndex++)
                {
                    if (branchIndex > 0)
                    {
                        var root = await session.NavigateToAsync(rootId);
                        Assert.Equal(HistoryNavigationOutcomeKind.Committed, root.Kind);
                    }

                    for (var editIndex = 0; editIndex < editsPerBranch; editIndex++)
                    {
                        await CommitAsync(session,
                            document => ChangeChapter(document, 0, name: $"branch-{branchIndex}-edit-{editIndex}"),
                            $"Branch {branchIndex} edit {editIndex}");
                    }

                    branchLeaves.Add(session.GetHistorySnapshot().CursorId);
                }

                var buildMilliseconds = stopwatch.ElapsedMilliseconds;
                var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
                var retainedBytes = GC.GetTotalMemory(forceFullCollection: true);
                stopwatch.Restart();
                for (var editIndex = 0; editIndex < editsPerBranch; editIndex++)
                {
                    var undo = await session.UndoAsync();
                    Assert.Equal(HistoryNavigationOutcomeKind.Committed, undo.Kind);
                }

                var undoTicks = stopwatch.ElapsedTicks;
                stopwatch.Restart();
                for (var editIndex = 0; editIndex < editsPerBranch; editIndex++)
                {
                    var redo = await session.RedoAsync();
                    Assert.Equal(HistoryNavigationOutcomeKind.Committed, redo.Kind);
                }

                var redoTicks = stopwatch.ElapsedTicks;
                if (branchCount == 1)
                {
                    _ = await session.NavigateToAsync(rootId);
                }

                stopwatch.Restart();
                var distantBranch = await session.NavigateToAsync(branchLeaves[0]);
                var distantBranchTicks = stopwatch.ElapsedTicks;
                Assert.Equal(HistoryNavigationOutcomeKind.Committed, distantBranch.Kind);
                Assert.Equal(editsPerBranch * branchCount + 1, session.GetHistorySnapshot().Nodes.Length);

                output.WriteLine(
                    $"History scale: chapters={chapterCount}; branches={branchCount}; editsPerBranch={editsPerBranch}; retainedNodes={session.GetHistorySnapshot().Nodes.Length}; allocatedBytes={allocatedBytes}; retainedHeapBytesAfterFullGC={retainedBytes}; buildAndBranchMs={buildMilliseconds}; undo32Ticks={undoTicks}; redo32Ticks={redoTicks}; distantBranchTicks={distantBranchTicks}; stopwatchFrequency={Stopwatch.Frequency}.");
            }
        }
    }

    [Fact]
    public async Task Failure_during_change_set_or_node_creation_preserves_published_state_and_history()
    {
        foreach (var failurePoint in new[] { SessionHistoryFailurePoint.ChangeSetConstruction, SessionHistoryFailurePoint.NodePublication })
        {
            var armed = true;
            var session = new SessionState(CreateDocument([CreateChapter(1, "A")]), point =>
            {
                if (armed && point == failurePoint)
                {
                    armed = false;
                    throw new OutOfMemoryException("Injected history allocation failure.");
                }
            });
            var before = session.Snapshot;
            var historyBefore = session.GetHistorySnapshot();
            var outcome = await session.ExecuteAsync(
                before.BaseToken,
                Guid.NewGuid(),
                failurePoint.ToString(),
                (document, _) => ValueTask.FromResult(ChangeChapter(document, 0, name: "Changed")));

            Assert.Equal(TransactionOutcomeKind.ResourceFailure, outcome.Kind);
            Assert.Same(before.Document, session.Snapshot.Document);
            Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);
            var historyAfter = session.GetHistorySnapshot();
            Assert.Equal(historyBefore.RootId, historyAfter.RootId);
            Assert.Equal(historyBefore.CursorId, historyAfter.CursorId);
            Assert.Equal(historyBefore.Nodes, historyAfter.Nodes);
        }
    }

    [Fact]
    public async Task Reconstruction_failure_and_history_boundaries_leave_state_unchanged()
    {
        var armed = false;
        var session = new SessionState(CreateDocument([CreateChapter(1, "A")]), point =>
        {
            if (armed && point == SessionHistoryFailurePoint.Reconstruction)
            {
                armed = false;
                throw new OutOfMemoryException("Injected reconstruction failure.");
            }
        });
        var root = session.Snapshot;
        var noUndo = await session.UndoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.NoChange, noUndo.Kind);
        Assert.Equal(0, noUndo.Snapshot.MutationRevision);

        _ = await CommitAsync(session, document => ChangeChapter(document, 0, name: "B"), "Rename");
        var changed = session.Snapshot;
        armed = true;
        var failedUndo = await session.UndoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.ResourceFailure, failedUndo.Kind);
        Assert.Same(changed.Document, session.Snapshot.Document);
        Assert.Equal(changed.MutationRevision, session.Snapshot.MutationRevision);
        Assert.Equal(2, session.GetHistorySnapshot().Nodes.Length);

        var undo = await session.UndoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, undo.Kind);
        var leaf = await session.RedoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, leaf.Kind);
        var noRedo = await session.RedoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.NoChange, noRedo.Kind);
        Assert.Equal(3, session.Snapshot.MutationRevision);
        Assert.Equal("A", root.Document.Tracks[0].Chapters[0].Name);
    }

    [Fact]
    public async Task Structural_deltas_restore_track_and_chapter_identities_and_order()
    {
        var before = CreateDocument([CreateChapter(1, "A"), CreateChapter(2, "B")]);
        var session = new SessionState(before);
        var addedTrackId = ChapterTrackId.New();
        var addedChapter = CreateChapter(3, "C");
        var commit = await CommitAsync(session, document => new EditableChapterDocument(
            document.Id,
            "Renamed document",
            document.SourceName,
            document.ImportFormat,
            document.Duration,
            new ChapterFrameRate(24, 1),
            [
                new EditableChapterTrack(addedTrackId, "New track", [addedChapter]),
                new EditableChapterTrack(document.Tracks[0].Id, "Reordered", [document.Tracks[0].Chapters[1], document.Tracks[0].Chapters[0]])
            ]), "Structural edit");
        var after = commit.Snapshot.Document;

        var undo = await session.UndoAsync();
        AssertDocument(before, undo.Snapshot.Document);
        var redo = await session.RedoAsync();
        AssertDocument(after, redo.Snapshot.Document);
        Assert.Equal(addedTrackId, redo.Snapshot.Document.Tracks[0].Id);
        Assert.Equal(addedChapter.Id, redo.Snapshot.Document.Tracks[0].Chapters[0].Id);
    }

    [Fact]
    public async Task Duplicate_transaction_retry_adds_only_one_history_node()
    {
        var session = new SessionState(CreateDocument([CreateChapter(1, "A")]));
        var before = session.Snapshot;
        var id = Guid.NewGuid();
        var request = "rename:A-to-B";
        var first = await session.ExecuteAsync(before.BaseToken, id, request,
            (document, _) => ValueTask.FromResult(ChangeChapter(document, 0, name: "B")));
        var retry = await session.ExecuteAsync(before.BaseToken, id, request,
            (_, _) => throw new InvalidOperationException("A duplicate request must replay."));

        Assert.Same(first, retry);
        Assert.Equal(2, session.GetHistorySnapshot().Nodes.Length);
    }

    private static async Task<int> CountByUndoAsync(SessionState session)
    {
        var count = 1;
        while ((await session.UndoAsync()).Kind == HistoryNavigationOutcomeKind.Committed)
        {
            count++;
        }

        return count;
    }

    private static async Task<TransactionOutcome> CommitAsync(
        SessionState session,
        Func<EditableChapterDocument, EditableChapterDocument> edit,
        string description)
    {
        var before = session.Snapshot;
        return await session.ExecuteAsync(
            before.BaseToken,
            Guid.NewGuid(),
            $"{description}:{before.MutationRevision}",
            (document, _) => ValueTask.FromResult(edit(document)),
            operationDescription: description);
    }

    private static EditableChapterDocument CreateDocument(IEnumerable<EditableChapter> chapters)
    {
        var track = new EditableChapterTrack(ChapterTrackId.New(), "Track", chapters);
        return new EditableChapterDocument(
            ChapterDocumentId.New(), "Title", "source", ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null, [track]);
    }

    private static EditableChapter CreateChapter(int number, string name) =>
        new(ChapterId.New(), number, number * TimeSpan.TicksPerSecond, name);

    private static EditableChapterDocument ChangeChapter(EditableChapterDocument document, int index, string? name = null)
    {
        var track = document.Tracks[0];
        var chapters = track.Chapters.SetItem(index, track.Chapters[index] with { Name = name ?? track.Chapters[index].Name });
        return new EditableChapterDocument(document.Id, document.Title, document.SourceName, document.ImportFormat,
            document.Duration, document.FrameRate, [new EditableChapterTrack(track.Id, track.Name, chapters)]);
    }

    private static void AssertDocument(EditableChapterDocument expected, EditableChapterDocument actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.SourceName, actual.SourceName);
        Assert.Equal(expected.ImportFormat, actual.ImportFormat);
        Assert.Equal(expected.Duration, actual.Duration);
        Assert.Equal(expected.FrameRate, actual.FrameRate);
        Assert.Equal(expected.Tracks.Length, actual.Tracks.Length);
        for (var trackIndex = 0; trackIndex < expected.Tracks.Length; trackIndex++)
        {
            Assert.Equal(expected.Tracks[trackIndex].Id, actual.Tracks[trackIndex].Id);
            Assert.Equal(expected.Tracks[trackIndex].Name, actual.Tracks[trackIndex].Name);
            Assert.Equal(expected.Tracks[trackIndex].Chapters, actual.Tracks[trackIndex].Chapters);
        }
    }
}
