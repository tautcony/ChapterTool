using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ChapterContentCandidateBuilderTests
{
    [Theory]
    [MemberData(nameof(OperationFamilies))]
    public async Task Candidate_family_commits_and_round_trips_through_history(
        string operation,
        Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult> build)
    {
        var document = CreateDocument();
        var session = new SessionState(document);
        var before = session.Snapshot;
        var preview = ChapterContentOperationSession.Prepare(session, operation, source => build(Builder(), source));

        Assert.True(preview.IsValid, string.Join("; ", preview.Errors));
        Assert.NotEmpty(preview.Differences);

        var committed = await ChapterContentOperationSession.ApplyAsync(session, preview);
        Assert.Equal(TransactionOutcomeKind.Committed, committed.Kind);
        Assert.Equal(1, session.Snapshot.MutationRevision);
        if (operation == "metadata-title")
        {
            Assert.NotEqual(before.Document.Title, session.Snapshot.Document.Title);
        }
        else if (operation == "metadata-source")
        {
            Assert.NotEqual(before.Document.SourceName, session.Snapshot.Document.SourceName);
        }
        else if (operation == "metadata-track")
        {
            Assert.NotEqual(before.Document.Tracks[0].Name, session.Snapshot.Document.Tracks[0].Name);
        }
        else
        {
            Assert.NotEqual(before.Document.Tracks[0].Chapters, session.Snapshot.Document.Tracks[0].Chapters);
        }

        var undone = await session.UndoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, undone.Kind);
        AssertDocumentValues(before.Document, undone.Snapshot.Document);

        var redone = await session.RedoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, redone.Kind);
        AssertDocumentValues(committed.Snapshot.Document, redone.Snapshot.Document);
    }

    [Fact]
    public async Task Invalid_batch_preview_does_not_change_document_or_history()
    {
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;
        var targets = before.Document.Tracks[0].Chapters.Select(static chapter => chapter.Id).ToHashSet();
        var preview = ChapterContentOperationSession.Prepare(session, "replace names", document =>
            Builder().ReplaceNames(document, "[", "replacement", targets));

        Assert.False(preview.IsValid);
        var outcome = await ChapterContentOperationSession.ApplyAsync(session, preview);

        Assert.Equal(TransactionOutcomeKind.Invalid, outcome.Kind);
        Assert.Equal(before.StateIdentity, session.Snapshot.StateIdentity);
        Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);
        Assert.Equal(session.GetHistorySnapshot().RootId, session.GetHistorySnapshot().CursorId);
        AssertDocumentValues(before.Document, session.Snapshot.Document);
    }

    [Fact]
    public async Task Preview_from_stale_snapshot_cannot_apply()
    {
        var session = new SessionState(CreateDocument());
        var preview = ChapterContentOperationSession.Prepare(session, "rename", document =>
            Builder().EditCell(document, document.Tracks[0].Chapters[0].Id, ChapterCellField.Name, "changed", 25));
        var intervening = ChapterContentOperationSession.Prepare(session, "other rename", document =>
            Builder().EditCell(document, document.Tracks[0].Chapters[1].Id, ChapterCellField.Name, "other", 25));
        _ = await ChapterContentOperationSession.ApplyAsync(session, intervening);

        var outcome = await ChapterContentOperationSession.ApplyAsync(session, preview);

        Assert.Equal(TransactionOutcomeKind.Conflict, outcome.Kind);
        Assert.Equal(1, session.Snapshot.MutationRevision);
        Assert.Equal("other", session.Snapshot.Document.Tracks[0].Chapters[1].Name);
    }

    [Fact]
    public async Task Expression_preview_commits_the_prepared_candidate_once_and_redoes_without_rebuilding()
    {
        var session = new SessionState(CreateDocument());
        var buildCount = 0;
        var preview = ChapterContentOperationSession.Prepare(session, "expression", document =>
        {
            buildCount++;
            return Builder().ApplyExpression(document, "t + 1");
        });
        var reviewedCandidate = preview.Candidate;

        var committed = await ChapterContentOperationSession.ApplyAsync(session, preview);
        var retry = await ChapterContentOperationSession.ApplyAsync(session, preview);

        Assert.Equal(1, buildCount);
        Assert.Same(committed, retry);
        Assert.Equal(reviewedCandidate.Tracks[0].Chapters, committed.Snapshot.Document.Tracks[0].Chapters);
        Assert.Equal(1, session.GetHistorySnapshot().Nodes.Length - 1);
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, (await session.UndoAsync()).Kind);
        var redone = await session.RedoAsync();
        Assert.Equal(HistoryNavigationOutcomeKind.Committed, redone.Kind);
        Assert.Equal(reviewedCandidate.Tracks[0].Chapters, redone.Snapshot.Document.Tracks[0].Chapters);
        Assert.Equal(1, buildCount);
    }

    public static IEnumerable<object[]> OperationFamilies()
    {
        yield return ["edit name", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.EditCell(document, document.Tracks[0].Chapters[0].Id, ChapterCellField.Name, "Renamed", 25))];
        yield return ["edit time", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.EditCell(document, document.Tracks[0].Chapters[0].Id, ChapterCellField.StartTime, "00:00:02.000", 25))];
        yield return ["insert", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.InsertBefore(document, document.Tracks[0].Chapters[1].Id))];
        yield return ["delete", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.Delete(document, new HashSet<ChapterId> { document.Tracks[0].Chapters[0].Id }))];
        yield return ["reorder", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.Reorder(document, document.Tracks[0].Chapters.Reverse().Select(static chapter => chapter.Id).ToArray()))];
        yield return ["number", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ApplyNumbering(document, 3))];
        yield return ["template", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ApplyTemplateNames(document, "One\nTwo"))];
        yield return ["regex replacement", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ReplaceNames(document, "Chapter", "Episode"))];
        yield return ["offset", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.Offset(document, document.Tracks[0].Chapters.Select(static chapter => chapter.Id).ToHashSet(), TimeSpan.FromSeconds(1)))];
        yield return ["frame offset", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ShiftFrames(document, document.Tracks[0].Chapters.Select(static chapter => chapter.Id).ToHashSet(), 2, 25))];
        yield return ["frame rate", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ChangeFrameRate(document, 25, 50))];
        yield return ["expression", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.ApplyExpression(document, "t + 1"))];
        yield return ["metadata-title", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.UpdateMetadata(document, title: "New title", updateTitle: true))];
        yield return ["metadata-source", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.UpdateMetadata(document, sourceName: "new-source.mkv", updateSourceName: true))];
        yield return ["metadata-track", (Func<ChapterContentCandidateBuilder, EditableChapterDocument, ChapterCandidateBuildResult>)((builder, document) =>
            builder.UpdateMetadata(document, trackName: "Main audio", updateTrackName: true))];
    }

    private static ChapterContentCandidateBuilder Builder() => new(new ChapterEditingService(new ChapterTimeFormatter()));

    private static EditableChapterDocument CreateDocument() => EditableChapterDocumentAdapter.FromChapterSet(new ChapterSet(
        "Title",
        "source.mkv",
        ChapterImportFormat.Ogm,
        25,
        TimeSpan.FromSeconds(30),
        [
            new Chapter(1, TimeSpan.Zero, "Chapter One", EndTime: TimeSpan.FromSeconds(5)),
            new Chapter(2, TimeSpan.FromSeconds(10), "Chapter Two", EndTime: TimeSpan.FromSeconds(15))
        ]));

    private static void AssertDocumentValues(EditableChapterDocument expected, EditableChapterDocument actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.SourceName, actual.SourceName);
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
