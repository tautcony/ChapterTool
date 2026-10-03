using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Models;

public sealed class EditableChapterDocumentTests
{
    [Fact]
    public void Adapter_copies_chapters_into_an_immutable_snapshot_and_retains_unknown_duration()
    {
        var chapters = new List<Chapter> { new(1, TimeSpan.FromSeconds(10), "A") };
        var legacy = new ChapterSet("Title", "source", ChapterImportFormat.Ogm, 24, TimeSpan.Zero, chapters);

        var document = EditableChapterDocumentAdapter.FromChapterSet(legacy);
        chapters.Add(new Chapter(2, TimeSpan.FromSeconds(20), "B"));

        Assert.Single(document.Tracks);
        Assert.Single(document.Tracks[0].Chapters);
        Assert.False(document.Duration.IsKnown);
        Assert.Equal(new ChapterFrameRate(24, 1), document.FrameRate);
        Assert.True(EditableChapterDocumentValidator.Validate(document).IsValid);
        Assert.Equal(TimeSpan.Zero, EditableChapterDocumentAdapter.ToChapterSet(document).Duration);
    }

    [Fact]
    public void Candidate_value_edit_preserves_identity_without_changing_published_snapshot()
    {
        var original = EditableChapterDocumentAdapter.FromChapterSet(
            new ChapterSet("Title", null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, [new Chapter(1, TimeSpan.Zero, "A")]));
        var sourceChapter = original.Tracks[0].Chapters[0];
        var changedTrack = new EditableChapterTrack(
            original.Tracks[0].Id,
            original.Tracks[0].Name,
            [sourceChapter with { Name = "B" }]);
        var candidate = new EditableChapterDocument(
            original.Id, original.Title, original.SourceName, original.ImportFormat, original.Duration, original.FrameRate, [changedTrack]);

        Assert.Equal("A", original.Tracks[0].Chapters[0].Name);
        Assert.Equal("B", candidate.Tracks[0].Chapters[0].Name);
        Assert.Equal(sourceChapter.Id, candidate.Tracks[0].Chapters[0].Id);
        Assert.Equal(original.Id, candidate.Id);
    }

    [Fact]
    public void Unknown_duration_does_not_limit_chapter_start()
    {
        var original = EditableChapterDocumentAdapter.FromChapterSet(
            new ChapterSet("Title", null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, [new Chapter(1, TimeSpan.Zero, "A")]));
        var chapter = original.Tracks[0].Chapters[0] with { StartTicks = TimeSpan.FromDays(100).Ticks };
        var document = new EditableChapterDocument(
            original.Id, original.Title, original.SourceName, original.ImportFormat, original.Duration, original.FrameRate,
            [new EditableChapterTrack(original.Tracks[0].Id, "Track", [chapter])]);

        Assert.True(EditableChapterDocumentValidator.Validate(document).IsValid);
    }

    [Fact]
    public void Export_adapter_preserves_track_boundaries_as_segment_separators()
    {
        var document = new EditableChapterDocument(
            ChapterDocumentId.New(),
            "Title",
            null,
            ChapterImportFormat.Unknown,
            ChapterDuration.Unknown,
            null,
            [
                new EditableChapterTrack(ChapterTrackId.New(), "First", [new EditableChapter(ChapterId.New(), 1, 0, "A")]),
                new EditableChapterTrack(ChapterTrackId.New(), "Second", [new EditableChapter(ChapterId.New(), 1, 0, "B")])
            ]);

        var chapterSet = EditableChapterDocumentAdapter.ToChapterSet(document);

        Assert.Equal([ChapterKind.Marker, ChapterKind.Separator, ChapterKind.Marker], chapterSet.Chapters.Select(static chapter => chapter.Kind));
        Assert.Equal("Second", chapterSet.Chapters[1].Name);
    }

    [Fact]
    public void Rational_frame_rate_is_reduced_and_rounds_midpoints_away_from_zero()
    {
        var rate = new ChapterFrameRate(48_000, 2_002);

        Assert.Equal(new ChapterFrameRate(24_000, 1_001), rate);
        Assert.Equal(5_000_000, ChapterFrameTimeConverter.FramesToTicks(12, new ChapterFrameRate(24, 1)).Value);
        Assert.Equal(-5_000_000, ChapterFrameTimeConverter.FramesToTicks(-12, new ChapterFrameRate(24, 1)).Value);
        Assert.Equal(1, ChapterFrameTimeConverter.TicksToFrames(2_500_000, new ChapterFrameRate(2, 1)).Value);
    }

    [Fact]
    public void Frame_conversion_overflow_returns_a_diagnostic_failure()
    {
        var result = ChapterFrameTimeConverter.FramesToTicks(long.MaxValue, new ChapterFrameRate(1, long.MaxValue));

        Assert.False(result.Success);
        Assert.Equal(ChapterDiagnosticCode.FrameConversionOverflow, result.Diagnostic?.Code);
    }
}
