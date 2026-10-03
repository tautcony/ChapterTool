using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ChapterClipCandidateBuilderTests
{
    [Fact]
    public void Merge_and_split_use_current_values_and_preserve_chapter_identity_and_explicit_ends()
    {
        var source = CreateDocument(
            Segment("part-a", "Part A", 0, 10),
            Segment("part-b", "Part B", 0, 10),
            TrackChapters(
                [Chapter(0, "Edited A", 4), Chapter(8, "B", 9)],
                [Chapter(2, "Edited B", 7)]));
        var originalIds = source.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id).ToArray();

        var merged = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));

        Assert.True(merged.IsValid, string.Join("; ", merged.Errors));
        Assert.Single(merged.Candidate.Tracks);
        var mergedTrack = merged.Candidate.Tracks[0];
        Assert.NotEqual(source.Tracks[0].Id, mergedTrack.Id);
        Assert.Equal(originalIds, mergedTrack.Chapters.Select(static chapter => chapter.Id));
        Assert.Equal([0L, TimeSpan.FromSeconds(8).Ticks, TimeSpan.FromSeconds(12).Ticks], mergedTrack.Chapters.Select(static chapter => chapter.StartTicks));
        Assert.Equal("Edited A", mergedTrack.Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, mergedTrack.Chapters[0].EndTicks);
        Assert.Equal("Edited B", mergedTrack.Chapters[2].Name);
        Assert.Equal(TimeSpan.FromSeconds(17).Ticks, mergedTrack.Chapters[2].EndTicks);

        var split = ChapterClipCandidateBuilder.SplitByBoundaries(merged.Candidate, mergedTrack.Id);

        Assert.True(split.IsValid, string.Join("; ", split.Errors));
        Assert.Equal(2, split.Candidate.Tracks.Length);
        Assert.Equal(originalIds, split.Candidate.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id));
        Assert.Equal("Edited A", split.Candidate.Tracks[0].Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, split.Candidate.Tracks[0].Chapters[0].EndTicks);
        Assert.Equal("Edited B", split.Candidate.Tracks[1].Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(7).Ticks, split.Candidate.Tracks[1].Chapters[0].EndTicks);
    }

    [Fact]
    public void Split_routes_boundary_chapters_forward_and_total_duration_marker_to_last_segment()
    {
        var source = CreateDocument(
            Segment("a", "A", 0, 10),
            Segment("b", "B", 0, 10),
            TrackChapters(
                [Chapter(0, "A")],
                [Chapter(0, "Boundary"), Chapter(10, "End marker")]));
        var merged = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));

        Assert.True(merged.IsValid, string.Join("; ", merged.Errors));
        var split = ChapterClipCandidateBuilder.SplitByBoundaries(merged.Candidate, merged.Candidate.Tracks[0].Id);

        Assert.True(split.IsValid, string.Join("; ", split.Errors));
        Assert.Empty(split.Candidate.Tracks[0].Chapters.Where(static chapter => chapter.Name == "Boundary"));
        Assert.Contains(split.Candidate.Tracks[1].Chapters, static chapter => chapter.Name == "Boundary" && chapter.StartTicks == 0);
        Assert.Contains(split.Candidate.Tracks[1].Chapters, static chapter => chapter.Name == "End marker" && chapter.StartTicks == TimeSpan.FromSeconds(10).Ticks);
    }

    [Fact]
    public async Task Split_reports_cross_segment_explicit_end_without_truncating_or_committing()
    {
        var source = CreateDocument(
            Segment("a", "A", 0, 10),
            Segment("b", "B", 0, 10),
            TrackChapters(
                [Chapter(8, "Crossing", 11)],
                [Chapter(1, "B")]));
        var merged = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));
        Assert.True(merged.IsValid, string.Join("; ", merged.Errors));
        var session = new SessionState(merged.Candidate);
        var before = session.Snapshot;

        var preview = ChapterContentOperationSession.Prepare(session, "Split by boundaries", document =>
            ChapterClipCandidateBuilder.SplitByBoundaries(document, document.Tracks[0].Id));
        var outcome = await ChapterContentOperationSession.ApplyAsync(session, preview);

        Assert.False(preview.IsValid);
        Assert.Contains(preview.Errors, static error => error.Contains("crosses a segment boundary", StringComparison.Ordinal));
        Assert.Equal(TransactionOutcomeKind.Invalid, outcome.Kind);
        Assert.Equal(before.StateIdentity, session.Snapshot.StateIdentity);
        Assert.Equal(TimeSpan.FromSeconds(11).Ticks, session.Snapshot.Document.Tracks[0].Chapters[0].EndTicks);
    }

    [Fact]
    public void Nested_boundaries_flatten_on_merge_and_split_back_to_each_source_segment()
    {
        var first = TrackWithSegments("nested", "Nested", [
            Segment("a", "A", 0, 4),
            Segment("b", "B", 4, 6)],
            [Chapter(1, "A"), Chapter(5, "B")]);
        var second = TrackWithSegments("c", "C", [Segment("c", "C", 0, 5)], [Chapter(2, "C")]);
        var source = CreateDocument([first, second], 15);

        var merged = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));

        Assert.True(merged.IsValid, string.Join("; ", merged.Errors));
        Assert.Equal(3, merged.Candidate.Tracks[0].Segments.Length);
        Assert.Equal([0L, TimeSpan.FromSeconds(4).Ticks, TimeSpan.FromSeconds(10).Ticks], merged.Candidate.Tracks[0].Segments.Select(static segment => segment.StartTicks));
        var split = ChapterClipCandidateBuilder.SplitByBoundaries(merged.Candidate, merged.Candidate.Tracks[0].Id);

        Assert.True(split.IsValid, string.Join("; ", split.Errors));
        Assert.Equal(3, split.Candidate.Tracks.Length);
        Assert.Equal(["A", "B", "C"], split.Candidate.Tracks.Select(static track => track.Name));
        Assert.Equal([TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(2).Ticks],
            split.Candidate.Tracks.Select(static track => track.Chapters.Single().StartTicks));
    }

    [Fact]
    public void Merge_rejects_incompatible_frame_rates()
    {
        var source = CreateDocument(
            Segment("a", "A", 0, 10, new ChapterFrameRate(24, 1)),
            Segment("b", "B", 0, 10, new ChapterFrameRate(25, 1)),
            TrackChapters([Chapter(1, "A")], [Chapter(1, "B")]));

        var result = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static error => error.Contains("different effective frame rates", StringComparison.Ordinal));
    }

    [Fact]
    public void Merged_fps_conversion_keeps_source_rates_and_uses_effective_rate_after_split()
    {
        var source = CreateDocument(
            Segment("a", "A", 0, 10, new ChapterFrameRate(25, 1)),
            Segment("b", "B", 0, 10, new ChapterFrameRate(25, 1)),
            TrackChapters([Chapter(5, "A")], [Chapter(2, "B")]));
        var merged = ChapterClipCandidateBuilder.MergeTracks(source, source.Tracks.Select(static track => track.Id));
        Assert.True(merged.IsValid, string.Join("; ", merged.Errors));

        var legacy = EditableChapterDocumentAdapter.ToChapterSet(merged.Candidate);
        var changed = ChapterFpsTransformService.ChangeFps(legacy, 25m, 50m);
        var converted = EditableChapterDocumentAdapter.ApplyFrameRateChangeResult(merged.Candidate, changed.Info, 25m, 50m);
        var split = ChapterClipCandidateBuilder.SplitByBoundaries(converted, converted.Tracks[0].Id);

        Assert.True(split.IsValid, string.Join("; ", split.Errors));
        Assert.Equal(2, split.Candidate.Tracks.Length);
        Assert.All(split.Candidate.Tracks.SelectMany(static track => track.Segments), segment =>
        {
            Assert.Equal(new ChapterFrameRate(50, 1), segment.FrameRate);
            Assert.Equal(new ChapterFrameRate(25, 1), segment.SourceFrameRate);
        });
        Assert.Equal(TimeSpan.FromSeconds(2.5).Ticks, split.Candidate.Tracks[0].Chapters[0].StartTicks);
        Assert.Equal(TimeSpan.FromSeconds(1).Ticks, split.Candidate.Tracks[1].Chapters[0].StartTicks);
    }

    [Fact]
    public void Append_assigns_fresh_ids_and_preserves_current_edits_and_segment_media_metadata()
    {
        var current = CreateDocument(SingleTrack("current", "Current", [Chapter(1, "Edited")]), 10);
        var imported = CreateDocument(SingleTrack("imported", "Imported", [Chapter(2, "Imported")], 5), 5, "append.mpls");
        var currentId = current.Tracks[0].Chapters[0].Id;
        var importedId = imported.Tracks[0].Chapters[0].Id;

        var result = ChapterClipCandidateBuilder.Append(current, imported);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var track = result.Candidate.Tracks.Single();
        Assert.Equal("Edited", track.Chapters[0].Name);
        Assert.Equal(currentId, track.Chapters[0].Id);
        Assert.NotEqual(importedId, track.Chapters[1].Id);
        Assert.Equal(TimeSpan.FromSeconds(12).Ticks, track.Chapters[1].StartTicks);
        Assert.Equal(2, track.Segments.Length);
        Assert.Equal("imported.mpls", track.Segments[1].SourceName);
    }

    [Fact]
    public async Task Merge_and_split_commit_as_separate_undoable_history_transactions()
    {
        var source = CreateDocument(
            Segment("a", "A", 0, 10),
            Segment("b", "B", 0, 10),
            TrackChapters([Chapter(0, "A")], [Chapter(0, "B")]));
        var session = new SessionState(source);
        var merge = ChapterContentOperationSession.Prepare(session, "Merge tracks", document =>
            ChapterClipCandidateBuilder.MergeTracks(document, document.Tracks.Select(static track => track.Id)));
        Assert.Equal(TransactionOutcomeKind.Committed, (await ChapterContentOperationSession.ApplyAsync(session, merge)).Kind);
        var merged = session.Snapshot.Document;
        var split = ChapterContentOperationSession.Prepare(session, "Split by boundaries", document =>
            ChapterClipCandidateBuilder.SplitByBoundaries(document, document.Tracks[0].Id));
        Assert.Equal(TransactionOutcomeKind.Committed, (await ChapterContentOperationSession.ApplyAsync(session, split)).Kind);

        Assert.Equal(3, session.GetHistorySnapshot().Nodes.Length);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, (await session.UndoAsync()).Kind);
        Assert.Equal(merged.Tracks[0].Segments, session.Snapshot.Document.Tracks[0].Segments);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, (await session.UndoAsync()).Kind);
        Assert.Equal(source.Tracks.Length, session.Snapshot.Document.Tracks.Length);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, (await session.RedoAsync()).Kind);
        Assert.Single(session.Snapshot.Document.Tracks);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, (await session.RedoAsync()).Kind);
        Assert.Equal(2, session.Snapshot.Document.Tracks.Length);
    }

    [Fact]
    public async Task Append_candidate_rejects_stale_base_and_cancellation_is_atomic()
    {
        var current = CreateDocument(SingleTrack("current", "Current", [Chapter(0, "Current")]), 10);
        var imported = CreateDocument(SingleTrack("imported", "Imported", [Chapter(0, "Imported")], 5), 5);
        var session = new SessionState(current);
        var preview = ChapterContentOperationSession.Prepare(session, "Append", document => ChapterClipCandidateBuilder.Append(document, imported));
        var changed = ChapterContentOperationSession.Prepare(session, "Edit", document =>
            new ChapterCandidateBuildResult(true, RenameFirst(document, "Changed"), [], []));
        Assert.Equal(TransactionOutcomeKind.Committed, (await ChapterContentOperationSession.ApplyAsync(session, changed)).Kind);
        var staleAppend = await ChapterContentOperationSession.ApplyAsync(session, preview);
        Assert.True(staleAppend.Kind == TransactionOutcomeKind.Conflict, string.Join("; ", staleAppend.Errors));
        Assert.Single(session.Snapshot.Document.Tracks[0].Chapters);

        var beforeCancel = session.Snapshot;
        using var cancellation = new CancellationTokenSource();
        var outcome = await session.ExecuteAsync(
            beforeCancel.BaseToken,
            Guid.NewGuid(),
            "append-cancel",
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(beforeCancel.Document);
            },
            cancellation.Token);

        Assert.Equal(TransactionOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal(beforeCancel.StateIdentity, session.Snapshot.StateIdentity);
        Assert.Single(session.Snapshot.Document.Tracks[0].Chapters);
    }

    private static EditableChapterDocument RenameFirst(EditableChapterDocument document, string name)
    {
        var track = document.Tracks[0];
        var chapters = track.Chapters.SetItem(0, track.Chapters[0] with { Name = name });
        return new EditableChapterDocument(document.Id, document.Title, document.SourceName, document.ImportFormat, document.Duration, document.FrameRate,
            [new EditableChapterTrack(track.Id, track.Name, chapters, track.Segments)]);
    }

    private static EditableChapterSegment Segment(string id, string name, int startSeconds, int durationSeconds, ChapterFrameRate? rate = null) =>
        EditableChapterSegment.Create(ChapterTrackId.New(), id, name, TimeSpan.FromSeconds(startSeconds).Ticks,
            ChapterDuration.FromTicks(TimeSpan.FromSeconds(durationSeconds).Ticks), rate ?? new ChapterFrameRate(25, 1),
            ChapterImportFormat.Mpls, id + ".mpls", [new ReferencedMediaFile(id + ".m2ts", id + ".m2ts")]);

    private static EditableChapterTrack SingleTrack(string id, string name, EditableChapter[] chapters, int durationSeconds = 10) =>
        TrackWithSegments(id, name, [Segment(id, name, 0, durationSeconds)], chapters);

    private static EditableChapterTrack TrackWithSegments(string id, string name, EditableChapterSegment[] segments, EditableChapter[] chapters) =>
        new(ChapterTrackId.New(), name, chapters, segments);

    private static EditableChapterDocument CreateDocument(
        EditableChapterSegment first,
        EditableChapterSegment second,
        (EditableChapter[] First, EditableChapter[] Second) chapters) =>
        CreateDocument(
            [TrackWithSegments(first.SourceEntryId, first.Name, [first], chapters.First),
             TrackWithSegments(second.SourceEntryId, second.Name, [second], chapters.Second)], 20);

    private static EditableChapterDocument CreateDocument(EditableChapterTrack track, int durationSeconds, string? sourceName = "movie.mpls") =>
        CreateDocument([track], durationSeconds, sourceName);

    private static (EditableChapter[] First, EditableChapter[] Second) TrackChapters(EditableChapter[] first, EditableChapter[] second) => (first, second);

    private static EditableChapterDocument CreateDocument(EditableChapterTrack[] tracks, int durationSeconds, string? sourceName = "movie.mpls") =>
        new(ChapterDocumentId.New(), "Movie", sourceName, ChapterImportFormat.Mpls,
            ChapterDuration.FromTicks(TimeSpan.FromSeconds(durationSeconds).Ticks), new ChapterFrameRate(25, 1), tracks);

    private static EditableChapter Chapter(int startSeconds, string name, int? endSeconds = null) =>
        new(ChapterId.New(), 1, TimeSpan.FromSeconds(startSeconds).Ticks, name,
            EndTicks: endSeconds is int end ? TimeSpan.FromSeconds(end).Ticks : null);
}
