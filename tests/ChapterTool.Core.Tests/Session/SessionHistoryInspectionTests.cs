using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Core.Tests.Session;

public sealed class SessionHistoryInspectionTests
{
    [Fact]
    public async Task Inspecting_alternate_branch_is_parent_relative_and_does_not_change_history_navigation_state()
    {
        var document = CreateDocument();
        var session = new SessionState(document);
        var root = session.GetHistorySnapshot();
        var first = await CommitAsync(session, value => Rename(value, "First"));
        var firstId = session.GetHistorySnapshot().CursorId;
        _ = await session.UndoAsync();
        var alternate = await CommitAsync(session, value => Rename(value, "Alternate"));
        var alternateId = session.GetHistorySnapshot().CursorId;

        _ = await session.UndoAsync();
        var beforeInspection = session.GetHistorySnapshot();
        var current = session.Snapshot;
        var inspected = await session.InspectHistoryNodeAsync(firstId);

        Assert.Equal(HistoryInspectionOutcomeKind.Available, inspected.Kind);
        var details = Assert.IsType<HistoryNodeDetails>(inspected.Details);
        Assert.Equal(root.RootId, details.RootId);
        Assert.Equal(firstId, details.NodeId);
        Assert.Equal(root.RootId, details.ParentId);
        Assert.Equal("Start", Assert.Single(details.Changes.Where(change => change.Group == "Chapter" && change.Field == "Name")).Before.Value);
        Assert.Equal("First", Assert.Single(details.Changes.Where(change => change.Group == "Chapter" && change.Field == "Name")).After.Value);
        Assert.Equal(1, details.Summary.ModifiedChapters);
        Assert.Equal(current, session.Snapshot);

        var afterInspection = session.GetHistorySnapshot();
        Assert.Equal(beforeInspection.RootId, afterInspection.RootId);
        Assert.Equal(beforeInspection.CursorId, afterInspection.CursorId);
        Assert.Equal(beforeInspection.Nodes, afterInspection.Nodes);

        var notFound = await session.InspectHistoryNodeAsync(Guid.NewGuid());
        Assert.Equal(HistoryInspectionOutcomeKind.NotFound, notFound.Kind);
        Assert.Null(notFound.Details);
        Assert.Contains(afterInspection.Nodes, node => node.Id == alternateId);
        Assert.Equal("Alternate", alternate.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal("First", first.Snapshot.Document.Tracks[0].Chapters[0].Name);
    }

    [Fact]
    public async Task Inspection_distinguishes_chapter_add_remove_move_and_order_without_double_counting()
    {
        var sourceTrackId = ChapterTrackId.New();
        var destinationTrackId = ChapterTrackId.New();
        var moved = Chapter(1, "Move me");
        var removed = Chapter(2, "Remove me");
        var retained = Chapter(3, "Retained");
        var added = Chapter(4, "Added") with { StartTicks = 0 };
        var before = new EditableChapterDocument(ChapterDocumentId.New(), "Title", "source", ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null,
            [new EditableChapterTrack(sourceTrackId, "Source", [moved, removed, retained]), new EditableChapterTrack(destinationTrackId, "Destination", [])]);
        var session = new SessionState(before);
        var outcome = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title, current.SourceName,
            current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(sourceTrackId, "Source", [retained]),
             new EditableChapterTrack(destinationTrackId, "Destination", [added with { DisplayNumber = 9 }, moved])]));
        Assert.Equal(TransactionOutcomeKind.Committed, outcome.Kind);

        var detailsOutcome = await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId);
        Assert.Equal(HistoryInspectionOutcomeKind.Available, detailsOutcome.Kind);
        var details = detailsOutcome.Details!;
        Assert.Equal(1, details.Summary.AddedChapters);
        Assert.Equal(1, details.Summary.RemovedChapters);
        Assert.Equal(2, details.Summary.ModifiedChapters); // moved and reordered retained chapters count once each
        Assert.Contains(details.Changes, change => change.Group == "Chapter" && change.ObjectId == moved.Id.Value.ToString("D") && change.Field == "Track");
        var movedTrack = Assert.Single(details.Changes.Where(change => change.Group == "Chapter" && change.ObjectId == moved.Id.Value.ToString("D") && change.Field == "Track"));
        Assert.Equal(new HistoryTrackReference(sourceTrackId, "Source"), movedTrack.Before.Value);
        Assert.Equal(new HistoryTrackReference(destinationTrackId, "Destination"), movedTrack.After.Value);
        Assert.Contains(details.Changes, change => change.Group == "Chapter" && change.ObjectId == moved.Id.Value.ToString("D") && change.Field == "Position");
        Assert.Contains(details.Changes, change => change.Group == "Chapter" && change.ObjectId == removed.Id.Value.ToString("D") && change.Field == "Presence" && change.Before.IsPresent && !change.After.IsPresent);
        Assert.Contains(details.Changes, change => change.Group == "Chapter" && change.ObjectId == added.Id.Value.ToString("D") && change.Field == "Presence" && !change.Before.IsPresent && change.After.IsPresent);
        Assert.Equal(2, details.Summary.ChangedTracks);
        Assert.Same(outcome.Snapshot.Document, session.Snapshot.Document);
    }

    [Fact]
    public async Task Inspection_preserves_exact_submillisecond_ticks_and_captured_frame_rate_context()
    {
        var id = ChapterId.New();
        var trackId = ChapterTrackId.New();
        var chapter = new EditableChapter(id, 1, 1_234, "A");
        var original = new EditableChapterDocument(ChapterDocumentId.New(), "Title", null, ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, new ChapterFrameRate(24, 1),
            [new EditableChapterTrack(trackId, "Track", [chapter])]);
        var session = new SessionState(original);
        var commit = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title,
            current.SourceName, current.ImportFormat, current.Duration, new ChapterFrameRate(25, 1),
            [new EditableChapterTrack(trackId, "Track",
                [current.Tracks[0].Chapters[0] with { StartTicks = 2_345 }])]));
        Assert.Equal(TransactionOutcomeKind.Committed, commit.Kind);

        var details = (await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId)).Details!;
        var ticks = Assert.Single(details.Changes.Where(change => change.Group == "Chapter" && change.ObjectId == id.Value.ToString("D") && change.Field == "StartTicks"));
        Assert.Equal(1_234L, ticks.Before.Value);
        Assert.Equal(2_345L, ticks.After.Value);
        Assert.Equal(new ChapterFrameRate(24, 1), ticks.BeforeFrameRate);
        Assert.Equal(new ChapterFrameRate(25, 1), ticks.AfterFrameRate);
        var frameRate = Assert.Single(details.Changes.Where(change => change.Group == "Document" && change.Field == "FrameRate"));
        Assert.Equal(new ChapterFrameRate(24, 1), frameRate.Before.Value);
        Assert.Equal(new ChapterFrameRate(25, 1), frameRate.After.Value);
        Assert.Equal("Track", ticks.Context);
    }

    [Fact]
    public async Task Inspection_reconstruction_resource_failure_preserves_published_state_and_history()
    {
        var armed = false;
        var session = new SessionState(CreateDocument(), point =>
        {
            if (armed && point == SessionHistoryFailurePoint.InspectionReconstruction)
            {
                armed = false;
                throw new SessionResourceFailureException("Injected history inspection reconstruction failure.");
            }
        });
        var rootId = session.GetHistorySnapshot().RootId;
        var commit = await CommitAsync(session, document => Rename(document, "Changed"));
        Assert.Equal(TransactionOutcomeKind.Committed, commit.Kind);
        var before = session.Snapshot;
        var historyBefore = session.GetHistorySnapshot();
        var rootPreferredChild = historyBefore.Nodes.Single(node => node.Id == rootId).PreferredChildId;
        armed = true;

        var failed = await session.InspectHistoryNodeAsync(historyBefore.CursorId);

        Assert.Equal(HistoryInspectionOutcomeKind.ResourceFailure, failed.Kind);
        Assert.Null(failed.Details);
        Assert.Same(before.Document, session.Snapshot.Document);
        Assert.Equal(before.StateIdentity, session.Snapshot.StateIdentity);
        Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);
        var historyAfter = session.GetHistorySnapshot();
        Assert.Equal(historyBefore.RootId, historyAfter.RootId);
        Assert.Equal(historyBefore.CursorId, historyAfter.CursorId);
        Assert.Equal(historyBefore.Nodes, historyAfter.Nodes);
        Assert.Equal(rootPreferredChild, historyAfter.Nodes.Single(node => node.Id == rootId).PreferredChildId);
    }

    [Fact]
    public async Task Inspection_matches_chapter_moves_by_track_identity_when_track_names_match()
    {
        var sourceId = ChapterTrackId.New();
        var destinationId = ChapterTrackId.New();
        var chapter = Chapter(1, "Move");
        var document = new EditableChapterDocument(ChapterDocumentId.New(), "Title", null, ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null,
            [new EditableChapterTrack(sourceId, "Same name", [chapter]), new EditableChapterTrack(destinationId, "Same name", [])]);
        var session = new SessionState(document);
        var outcome = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title,
            current.SourceName, current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(sourceId, "Same name", []),
             new EditableChapterTrack(destinationId, "Same name", [chapter])]));
        Assert.Equal(TransactionOutcomeKind.Committed, outcome.Kind);

        var details = (await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId)).Details!;
        Assert.Equal(0, details.Summary.AddedChapters);
        Assert.Equal(0, details.Summary.RemovedChapters);
        Assert.Equal(1, details.Summary.ModifiedChapters);
        var trackChange = Assert.Single(details.Changes.Where(change => change.Group == "Chapter" &&
            change.ObjectId == chapter.Id.Value.ToString("D") && change.Field == "Track"));
        Assert.Equal(new HistoryTrackReference(sourceId, "Same name"), trackChange.Before.Value);
        Assert.Equal(new HistoryTrackReference(destinationId, "Same name"), trackChange.After.Value);
        Assert.DoesNotContain(details.Changes, change => change.Group == "Chapter" &&
            change.ObjectId == chapter.Id.Value.ToString("D") && change.Field == "Presence");
    }

    [Fact]
    public async Task Inspection_tracks_segment_membership_by_identity_across_renames_and_same_name_moves()
    {
        var sourceId = ChapterTrackId.New();
        var destinationId = ChapterTrackId.New();
        var segment = EditableChapterSegment.Create(sourceId, "entry", "Clip", 0, ChapterDuration.FromTicks(100),
            new ChapterFrameRate(24, 1), ChapterImportFormat.Unknown, "media");
        var document = new EditableChapterDocument(ChapterDocumentId.New(), "Title", null, ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null,
            [new EditableChapterTrack(sourceId, "Source", [], [segment]), new EditableChapterTrack(destinationId, "Destination", [])]);
        var session = new SessionState(document);

        var renamed = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title,
            current.SourceName, current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(sourceId, "Renamed source", [], [current.Tracks[0].Segments[0]]), current.Tracks[1]]));
        Assert.Equal(TransactionOutcomeKind.Committed, renamed.Kind);
        var renameDetails = (await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId)).Details!;
        Assert.Contains(renameDetails.Changes, change => change.Group == "Track" && change.ObjectId == sourceId.Value.ToString("D") && change.Field == "Name");
        Assert.DoesNotContain(renameDetails.Changes, change => change.Group == "Segment" && change.ObjectId == segment.Id.ToString("D") && change.Field == "Track");

        var restoredName = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title,
            current.SourceName, current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(sourceId, "Same name", [], [current.Tracks[0].Segments[0]]), current.Tracks[1]]));
        Assert.Equal(TransactionOutcomeKind.Committed, restoredName.Kind);

        var moved = await CommitAsync(session, current => new EditableChapterDocument(current.Id, current.Title,
            current.SourceName, current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(sourceId, "Same name", [], []),
             new EditableChapterTrack(destinationId, "Same name", [], [current.Tracks[0].Segments[0]])]));
        Assert.Equal(TransactionOutcomeKind.Committed, moved.Kind);
        var moveDetails = (await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId)).Details!;
        var membership = Assert.Single(moveDetails.Changes.Where(change => change.Group == "Segment" &&
            change.ObjectId == segment.Id.ToString("D") && change.Field == "Track"));
        Assert.Equal(new HistoryTrackReference(sourceId, "Same name"), membership.Before.Value);
        Assert.Equal(new HistoryTrackReference(destinationId, "Same name"), membership.After.Value);
    }

    [Fact]
    public async Task Inspection_reports_metadata_only_deltas_and_keeps_null_distinct_from_empty()
    {
        var trackId = ChapterTrackId.New();
        var chapter = Chapter(1, "A");
        var segment = EditableChapterSegment.Create(trackId, "source-entry", "Clip", 0, ChapterDuration.FromTicks(100),
            new ChapterFrameRate(24, 1), ChapterImportFormat.Unknown, "media");
        var original = new EditableChapterDocument(ChapterDocumentId.New(), "Title", null, ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null, [new EditableChapterTrack(trackId, "Track", [chapter], [segment])]);
        var session = new SessionState(original);
        var committed = await CommitAsync(session, current => new EditableChapterDocument(current.Id, "Renamed", string.Empty,
            current.ImportFormat, current.Duration, current.FrameRate,
            [new EditableChapterTrack(trackId, "Renamed track", current.Tracks[0].Chapters,
                [current.Tracks[0].Segments[0] with { SourceName = string.Empty, Name = "Renamed clip" }])]));

        var inspected = await session.InspectHistoryNodeAsync(session.GetHistorySnapshot().CursorId);
        Assert.Equal(HistoryInspectionOutcomeKind.Available, inspected.Kind);
        var details = inspected.Details!;
        Assert.Equal(0, details.Summary.AddedChapters);
        Assert.Equal(0, details.Summary.RemovedChapters);
        Assert.Equal(0, details.Summary.ModifiedChapters);
        Assert.Equal(1, details.Summary.ChangedTracks);
        Assert.Equal(1, details.Summary.ChangedSegments);
        AssertChange(details, "Document", original.Id.Value.ToString("D"), "Title", "Title", "Renamed");
        AssertChange(details, "Document", original.Id.Value.ToString("D"), "SourceName", null, string.Empty);
        AssertChange(details, "Track", trackId.Value.ToString("D"), "Name", "Track", "Renamed track");
        AssertChange(details, "Segment", segment.Id.ToString("D"), "Name", "Clip", "Renamed clip");
        AssertChange(details, "Segment", segment.Id.ToString("D"), "SourceName", "media", string.Empty);
        Assert.Equal("Renamed", committed.Snapshot.Document.Title);
    }

    [Fact]
    public async Task Root_inspection_returns_initial_ordered_values_and_ended_session_is_distinct()
    {
        var initial = CreateDocument();
        var session = new SessionState(initial);
        var rootId = session.GetHistorySnapshot().RootId;
        var root = await session.InspectHistoryNodeAsync(rootId);
        Assert.Equal(HistoryInspectionOutcomeKind.Available, root.Kind);
        Assert.Same(initial, root.Details!.RootDocument);
        Assert.Empty(root.Details.Changes);
        Assert.Equal(new HistoryNodeSummary(0, 0, 0, 0, 0), root.Details.Summary);

        session.EndSession();
        var ended = await session.InspectHistoryNodeAsync(rootId);
        Assert.Equal(HistoryInspectionOutcomeKind.Ended, ended.Kind);
        Assert.Null(ended.Details);
    }

    private static async Task<TransactionOutcome> CommitAsync(SessionState session, Func<EditableChapterDocument, EditableChapterDocument> edit)
    {
        var before = session.Snapshot;
        return await session.ExecuteAsync(before.BaseToken, Guid.NewGuid(), Guid.NewGuid().ToString("N"),
            (document, _) => ValueTask.FromResult(edit(document)));
    }

    private static EditableChapterDocument CreateDocument()
    {
        var track = new EditableChapterTrack(ChapterTrackId.New(), "Track", [Chapter(1, "Start")]);
        return new EditableChapterDocument(ChapterDocumentId.New(), "Title", "source", ChapterImportFormat.Unknown,
            ChapterDuration.Unknown, null, [track]);
    }

    private static EditableChapter Chapter(int number, string name) => new(ChapterId.New(), number, number * TimeSpan.TicksPerSecond, name);

    private static EditableChapterDocument Rename(EditableChapterDocument document, string name)
    {
        var track = document.Tracks[0];
        return new EditableChapterDocument(document.Id, document.Title, document.SourceName, document.ImportFormat,
            document.Duration, document.FrameRate, [new EditableChapterTrack(track.Id, track.Name,
                [track.Chapters[0] with { Name = name }], track.Segments)]);
    }

    private static void AssertChange(HistoryNodeDetails details, string group, string id, string field, object? before, object? after)
    {
        var change = Assert.Single(details.Changes.Where(item => item.Group == group && item.ObjectId == id && item.Field == field));
        Assert.True(change.Before.IsPresent);
        Assert.True(change.After.IsPresent);
        Assert.Equal(before, change.Before.Value);
        Assert.Equal(after, change.After.Value);
    }
}
