using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Session;

public sealed class SessionStateTests
{
    [Fact]
    public async Task Invalid_candidate_preserves_document_and_revision()
    {
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;
        var duplicateChapter = before.Document.Tracks[0].Chapters[0];
        var invalid = CreateCandidate(before.Document, [duplicateChapter, duplicateChapter]);

        var outcome = await session.ExecuteAsync(
            before.BaseToken,
            Guid.NewGuid(),
            "invalid batch",
            (_, _) => ValueTask.FromResult(invalid));

        Assert.Equal(TransactionOutcomeKind.Invalid, outcome.Kind);
        Assert.Same(before.Document, session.Snapshot.Document);
        Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);
        Assert.NotEmpty(outcome.Errors);
    }

    [Fact]
    public async Task Cancellation_before_publication_preserves_document_and_revision()
    {
        using var source = new CancellationTokenSource();
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;
        var transactionId = Guid.NewGuid();

        var outcome = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "cancel before publish",
            (_, _) =>
            {
                source.Cancel();
                return ValueTask.FromResult(Rename(before.Document, "Changed"));
            },
            source.Token);

        Assert.Equal(TransactionOutcomeKind.Cancelled, outcome.Kind);
        Assert.Same(before.Document, session.Snapshot.Document);
        Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);

        var retry = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "cancel before publish",
            (_, _) => ValueTask.FromResult(Rename(before.Document, "Changed")));

        Assert.Same(outcome, retry);
    }

    [Fact]
    public async Task Stale_base_token_is_rejected_without_running_candidate()
    {
        var session = new SessionState(CreateDocument());
        var stale = session.Snapshot;
        var first = await session.ExecuteAsync(
            stale.BaseToken,
            Guid.NewGuid(),
            "first",
            (document, _) => ValueTask.FromResult(Rename(document, "First")));
        var candidateCalled = false;

        var second = await session.ExecuteAsync(
            stale.BaseToken,
            Guid.NewGuid(),
            "stale",
            (document, _) =>
            {
                candidateCalled = true;
                return ValueTask.FromResult(Rename(document, "Stale"));
            });

        Assert.Equal(TransactionOutcomeKind.Committed, first.Kind);
        Assert.Equal(TransactionOutcomeKind.Conflict, second.Kind);
        Assert.False(candidateCalled);
        Assert.Equal("First", session.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal(1, session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Duplicate_transaction_replays_the_original_result()
    {
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;
        var transactionId = Guid.NewGuid();
        var first = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "rename:changed",
            (document, _) => ValueTask.FromResult(Rename(document, "Changed")));

        var retry = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "rename:changed",
            (_, _) => throw new InvalidOperationException("A retry must not recompute the candidate."));

        Assert.Same(first, retry);
        Assert.Equal(TransactionOutcomeKind.Committed, retry.Kind);
        Assert.Equal(1, session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Reusing_transaction_id_for_different_request_is_rejected()
    {
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;
        var transactionId = Guid.NewGuid();
        _ = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "rename:changed",
            (document, _) => ValueTask.FromResult(Rename(document, "Changed")));

        var conflict = await session.ExecuteAsync(
            before.BaseToken,
            transactionId,
            "rename:other",
            (document, _) => ValueTask.FromResult(Rename(document, "Other")));

        Assert.Equal(TransactionOutcomeKind.TransactionIdConflict, conflict.Kind);
        Assert.Equal("Changed", session.Snapshot.Document.Tracks[0].Chapters[0].Name);
        Assert.Equal(1, session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Resource_failure_preserves_document_and_revision()
    {
        var session = new SessionState(CreateDocument());
        var before = session.Snapshot;

        var outcome = await session.ExecuteAsync(
            before.BaseToken,
            Guid.NewGuid(),
            "resource failure",
            (_, _) => ValueTask.FromException<EditableChapterDocument>(new OutOfMemoryException()));

        Assert.Equal(TransactionOutcomeKind.ResourceFailure, outcome.Kind);
        Assert.Same(before.Document, session.Snapshot.Document);
        Assert.Equal(before.MutationRevision, session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Canonically_unchanged_candidate_does_not_advance_revision()
    {
        var document = CreateDocument();
        var session = new SessionState(document);
        var before = session.Snapshot;

        var outcome = await session.ExecuteAsync(
            before.BaseToken,
            Guid.NewGuid(),
            "same document",
            (_, _) => ValueTask.FromResult(CreateCandidate(document, document.Tracks[0].Chapters)));

        Assert.Equal(TransactionOutcomeKind.NoChange, outcome.Kind);
        Assert.Equal(0, session.Snapshot.MutationRevision);
    }

    [Fact]
    public async Task Legacy_non_structural_edit_adapter_preserves_chapter_identity_in_transaction()
    {
        var legacy = new ChapterSet(
            "Title",
            "source.txt",
            ChapterImportFormat.Ogm,
            25,
            TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "A"), new Chapter(2, TimeSpan.FromSeconds(10), "B")]);
        var document = EditableChapterDocumentAdapter.FromChapterSet(legacy);
        var firstId = document.Tracks[0].Chapters[0].Id;
        var secondId = document.Tracks[0].Chapters[1].Id;
        var session = new SessionState(document);
        var baseSnapshot = session.Snapshot;
        var editing = new ChapterEditingService(new ChapterTimeFormatter());

        var outcome = await session.ExecuteAsync(
            baseSnapshot.BaseToken,
            Guid.NewGuid(),
            "rename:row-1:B renamed",
            (current, _) =>
            {
                var result = editing.Rename(EditableChapterDocumentAdapter.ToChapterSet(current), 1, "B renamed");
                return ValueTask.FromResult(EditableChapterDocumentAdapter.ApplyNonStructuralChapterSetResult(current, result.ChapterSet));
            });

        Assert.Equal(TransactionOutcomeKind.Committed, outcome.Kind);
        Assert.Equal(firstId, outcome.Snapshot.Document.Tracks[0].Chapters[0].Id);
        Assert.Equal(secondId, outcome.Snapshot.Document.Tracks[0].Chapters[1].Id);
        Assert.Equal("B renamed", outcome.Snapshot.Document.Tracks[0].Chapters[1].Name);
    }

    [Fact]
    public void Legacy_non_structural_edit_adapter_rejects_row_count_changes()
    {
        var source = CreateDocument();
        var changedCount = new ChapterSet("Title", null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);

        Assert.Throws<ArgumentException>(() => EditableChapterDocumentAdapter.ApplyNonStructuralChapterSetResult(source, changedCount));
    }

    private static EditableChapterDocument CreateDocument() =>
        EditableChapterDocumentAdapter.FromChapterSet(new ChapterSet(
            "Title",
            null,
            ChapterImportFormat.Unknown,
            0,
            TimeSpan.Zero,
            [new Chapter(1, TimeSpan.Zero, "A")]));

    private static EditableChapterDocument Rename(EditableChapterDocument document, string name)
    {
        var track = document.Tracks[0];
        var chapter = track.Chapters[0] with { Name = name };
        return CreateCandidate(document, [chapter]);
    }

    private static EditableChapterDocument CreateCandidate(
        EditableChapterDocument document,
        IEnumerable<EditableChapter> chapters)
    {
        var track = document.Tracks[0];
        return new EditableChapterDocument(
            document.Id,
            document.Title,
            document.SourceName,
            document.ImportFormat,
            document.Duration,
            document.FrameRate,
            [new EditableChapterTrack(track.Id, track.Name, chapters)]);
    }
}
