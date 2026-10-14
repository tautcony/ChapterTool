using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class CanonicalCandidateTests
{
    private static readonly ChapterContentCandidateBuilder Builder = new(new ChapterEditingService(new ChapterTimeFormatter()));

    [Fact]
    public async Task Repeated_conversions_preserve_source_unknown_and_undo_restores_off_grid_ticks()
    {
        var rate = FrameRateValue.Rational(24000, 1001);
        var source = Document(rate, 12_345_678, withSegment: true);
        var session = new SessionState(source);
        var first = Builder.ChangeFrameRate(source, rate, FrameRateValue.Approximate(25m));
        Assert.True(first.IsValid, string.Join(";", first.Errors));
        Assert.NotEqual(source.Tracks[0].Chapters[0].StartTicks, first.Candidate.Tracks[0].Chapters[0].StartTicks);
        await Commit(session, first);
        var second = Builder.ChangeFrameRate(first.Candidate, FrameRateValue.Approximate(25m), rate);
        Assert.True(second.IsValid, string.Join(";", second.Errors));
        Assert.Equal(FrameRateValue.Unknown, second.Candidate.Tracks[0].Segments[0].SourceFrameRate);
        await Commit(session, second);
        await session.UndoAsync();
        var root = await session.UndoAsync();
        Assert.Equal(12_345_678, root.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        Assert.Equal(source.Tracks[0].Segments, root.Snapshot.Document.Tracks[0].Segments);
        await session.RedoAsync();
        var restored = await session.RedoAsync();
        Assert.True(ExpressionPreviewProjector.DocumentsEqual(second.Candidate, restored.Snapshot.Document));
    }

    [Fact]
    public async Task Same_rate_quantizes_once_and_kind_only_assignment_has_history()
    {
        var rate = FrameRateValue.Rational(25, 1);
        var source = Document(rate, 400_001);
        var session = new SessionState(source);
        var quantized = Builder.ChangeFrameRate(source, rate, rate);
        Assert.True(quantized.IsValid);
        Assert.Equal(400_000, quantized.Candidate.Tracks[0].Chapters[0].StartTicks);
        await Commit(session, quantized);
        var noChange = await Commit(session, Builder.ChangeFrameRate(quantized.Candidate, rate, rate));
        Assert.Equal(TransactionOutcomeKind.NoChange, noChange.Kind);
        var approximate = Builder.ChangeFrameRate(quantized.Candidate, rate, FrameRateValue.Approximate(25m));
        var assigned = await Commit(session, approximate);
        Assert.Equal(TransactionOutcomeKind.Committed, assigned.Kind);
        Assert.Equal(400_000, assigned.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        Assert.Equal(FrameRateKind.Approximate, assigned.Snapshot.Document.FrameRate.Kind);
        var undo = await session.UndoAsync();
        Assert.Equal(rate, undo.Snapshot.Document.Tracks[0].FrameRate);
    }

    [Fact]
    public void Invalid_bounds_unknown_rates_overflow_and_collapsed_segments_reject_the_whole_candidate()
    {
        var rate = FrameRateValue.Rational(25, 1);
        var source = Document(rate, 400_000);
        var missing = Builder.ChangeFrameRate(source, FrameRateValue.Unknown, rate);
        Assert.False(missing.IsValid);
        Assert.Same(source, missing.Candidate);
        Assert.Equal(ChapterDiagnosticCode.InvalidFrameRate, Assert.Single(missing.Diagnostics).Code);

        var invalid = Builder.EditCell(source, source.Tracks[0].Chapters[0].Id, ChapterCellField.StartTime, "00:00:02.000", rate);
        Assert.False(invalid.IsValid);
        Assert.Same(source, invalid.Candidate);
        Assert.Equal(ChapterDiagnosticCode.InvalidStructure, Assert.Single(invalid.Diagnostics).Code);

        var large = Document(rate, long.MaxValue - 1);
        var overflow = Builder.ChangeFrameRate(large, rate, FrameRateValue.Rational(1, long.MaxValue));
        Assert.False(overflow.IsValid);
        Assert.Same(large, overflow.Candidate);

        var tiny = Document(rate, 0, withSegment: true, durationTicks: 1);
        var collapse = Builder.ChangeFrameRate(tiny, rate, rate);
        Assert.False(collapse.IsValid);
        Assert.Same(tiny, collapse.Candidate);
        Assert.NotEmpty(collapse.Diagnostics);
    }

    private static EditableChapterDocument Document(FrameRateValue rate, long startTicks, bool withSegment = false, long durationTicks = 20_000_000)
    {
        var id = ChapterTrackId.New();
        var chapter = new EditableChapter(ChapterId.New(), 1, startTicks, "A", EndTicks: startTicks == long.MaxValue - 1 ? long.MaxValue : Math.Max(startTicks, 10_000_000));
        var segments = withSegment
            ? new[] { EditableChapterSegment.Create(id, "source", "Source", 0, ChapterDuration.FromTicks(durationTicks), rate,
                ChapterImportFormat.Mpls, "source.mpls", sourceFrameRate: FrameRateValue.Unknown) } : [];
        return new EditableChapterDocument(ChapterDocumentId.New(), "Title", "source", ChapterImportFormat.Mpls,
            ChapterDuration.FromTicks(durationTicks), [new EditableChapterTrack(id, "Track", [chapter], segments, rate)]);
    }

    private static ValueTask<TransactionOutcome> Commit(SessionState session, ChapterCandidateBuildResult candidate) =>
        session.ExecuteAsync(session.Snapshot.BaseToken, Guid.NewGuid(), "canonical conversion",
            (_, _) => ValueTask.FromResult(candidate.Candidate));
}
