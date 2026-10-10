using System.Collections.Immutable;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Core.Tests.Session;

public sealed class ExpressionPreviewProjectorTests
{
    [Fact]
    public void Overlapping_time_and_frame_effects_count_one_affected_chapter()
    {
        var before = CreateDocument([
            new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One", "100", FrameAccuracy: FrameAccuracy.Neutral)
        ], new ChapterFrameRate(24_000, 1_001));
        var old = before.Tracks[0].Chapters[0];
        var candidate = ReplaceChapters(before, [
            old with { StartTicks = TimeSpan.FromSeconds(1).Ticks, FramesInfo = "101", FrameAccuracy = FrameAccuracy.Accurate }
        ]);

        var projection = Project(before, candidate);

        Assert.True(projection.HasChanges);
        Assert.Equal(1, projection.ParticipatingChapterCount);
        Assert.Equal(1, projection.AffectedChapterCount);
        Assert.Equal(1, projection.TimeChangedChapterCount);
        Assert.Equal(1, projection.FrameInformationChangedChapterCount);
        Assert.Equal(TimeSpan.FromSeconds(1).Ticks, projection.Chapters[0].DeltaTicks);
        Assert.Equal(1, projection.Chapters[0].DeltaFrames);
        Assert.Equal(new ChapterFrameRate(24_000, 1_001), projection.Chapters[0].BeforeFrames!.FrameRate);
    }

    [Fact]
    public void Frame_only_and_property_only_effects_remain_distinct_from_time_changes()
    {
        var before = CreateDocument([
            new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One", string.Empty, FrameAccuracy: FrameAccuracy.Neutral)
        ], null);
        var old = before.Tracks[0].Chapters[0];
        var frameCandidate = ReplaceChapters(before, [old with { FramesInfo = "0", FrameAccuracy = FrameAccuracy.Accurate }]);

        var frameProjection = Project(before, frameCandidate);

        Assert.Equal(0, frameProjection.TimeChangedChapterCount);
        Assert.Equal(1, frameProjection.FrameInformationChangedChapterCount);
        Assert.True(frameProjection.TimesUnchanged);
        Assert.True(frameProjection.Chapters[0].BeforeFrames!.IsMissing);
        Assert.Equal(0, frameProjection.Chapters[0].CandidateFrames!.FrameCount);

        var propertyCandidate = new EditableChapterDocument(
            before.Id, before.Title, before.SourceName, before.ImportFormat, before.Duration,
            new ChapterFrameRate(25, 1), before.Tracks);
        var propertyProjection = Project(before, propertyCandidate);

        Assert.True(propertyProjection.HasChanges);
        Assert.Equal(0, propertyProjection.TimeChangedChapterCount);
        Assert.Equal(0, propertyProjection.AffectedChapterCount);
        Assert.Contains(propertyProjection.Properties, item => item.Property == "FrameRate");
    }

    [Fact]
    public void Incompatible_frame_bases_have_no_numeric_delta_and_missing_rate_is_not_zero()
    {
        var before = CreateDocument([
            new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One", "24", FrameAccuracy: FrameAccuracy.Neutral)
        ], new ChapterFrameRate(24, 1));
        var old = before.Tracks[0].Chapters[0];
        var candidate = ReplaceChapters(before, [old with { FramesInfo = "25" }]);
        candidate = new EditableChapterDocument(candidate.Id, candidate.Title, candidate.SourceName,
            candidate.ImportFormat, candidate.Duration, new ChapterFrameRate(25, 1), candidate.Tracks);

        var projection = Project(before, candidate);

        Assert.Null(projection.Chapters[0].DeltaFrames);
        Assert.Equal(new ChapterFrameRate(24, 1), projection.Chapters[0].BeforeFrames!.FrameRate);
        Assert.Equal(new ChapterFrameRate(25, 1), projection.Chapters[0].CandidateFrames!.FrameRate);

        var missingRate = CreateDocument([
            new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One", "0", FrameAccuracy: FrameAccuracy.Neutral)
        ], null);
        var missingProjection = Project(missingRate, missingRate);
        Assert.Null(missingProjection.Chapters[0].BeforeFrames!.FrameRate);
        Assert.Equal(0, missingProjection.Chapters[0].BeforeFrames!.FrameCount);
    }

    [Fact]
    public void Projection_preserves_submillisecond_ticks_and_excludes_separators_from_scope()
    {
        var chapter = new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 1, "One");
        var separator = new EditableChapter(new ChapterId(Guid.NewGuid()), 0, 0, "Section", Kind: ChapterKind.Separator);
        var before = CreateDocument([chapter, separator], new ChapterFrameRate(25, 1));
        var candidate = ReplaceChapters(before, [chapter with { StartTicks = 2 }, separator]);

        var projection = Project(before, candidate);

        Assert.Equal(1, projection.ParticipatingChapterCount);
        Assert.Equal(1, projection.ExcludedSeparatorCount);
        Assert.Single(projection.Chapters);
        Assert.Equal(1, projection.Chapters[0].DeltaTicks);
    }

    [Fact]
    public void Projection_calculates_preview_frames_from_ticks_and_exact_display_rate()
    {
        var chapter = new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One", string.Empty);
        var before = CreateDocument([chapter], null);
        var candidate = ReplaceChapters(before, [chapter with { StartTicks = 1 }]);
        var session = new SessionState(before);
        var preview = ChapterContentOperationSession.Prepare(session, "expression", _ =>
            new ChapterCandidateBuildResult(true, candidate, [chapter.Id], []));

        var projection = ExpressionPreviewProjector.Build(preview, displayFrameRate: 24000m / 1001m,
            frameDecimalPlaces: 6, exactDisplayFrameRate: new ChapterFrameRate(24_000, 1_001));

        Assert.Equal("0.000002", projection.Chapters.Single().CandidateFrames!.Text);
        Assert.Equal("0.000000", projection.Chapters.Single().BeforeFrames!.Text);
        Assert.True(projection.Chapters.Single().BeforeFrames!.IsMissing);
    }

    [Fact]
    public void Projection_keeps_all_one_thousand_candidate_comparisons_accessible()
    {
        var chapters = Enumerable.Range(0, 1_000)
            .Select(index => new EditableChapter(new ChapterId(Guid.NewGuid()), index + 1, index, $"Chapter {index + 1}"))
            .ToArray();
        var before = CreateDocument(chapters, new ChapterFrameRate(25, 1));
        var candidate = ReplaceChapters(before,
            [.. chapters.Select(static chapter => chapter with { StartTicks = chapter.StartTicks + 1 })]);

        var projection = Project(before, candidate);

        Assert.Equal(1_000, projection.ParticipatingChapterCount);
        Assert.Equal(1_000, projection.AffectedChapterCount);
        Assert.Equal(1_000, projection.TimeChangedChapterCount);
        Assert.Equal(1_000, projection.Chapters.Length);
        Assert.Equal(chapters[0].Id, projection.Chapters[0].Id);
        Assert.Equal(chapters[^1].Id, projection.Chapters[^1].Id);
    }

    [Fact]
    public void Complete_document_equality_detects_metadata_even_when_chapter_rows_are_equal()
    {
        var before = CreateDocumentWithMetadataSegment([
            new EditableChapter(new ChapterId(Guid.NewGuid()), 1, 0, "One")
        ], null);
        var withMetadata = new EditableChapterDocument(before.Id, before.Title, before.SourceName,
            before.ImportFormat, ChapterDuration.FromTicks(10), before.FrameRate, before.Tracks);

        Assert.False(ExpressionPreviewProjector.DocumentsEqual(before, withMetadata));
        Assert.True(ExpressionPreviewProjector.DocumentsEqual(before, CreateDocumentCopy(before)));
    }

    private static ExpressionPreviewProjection Project(EditableChapterDocument before, EditableChapterDocument candidate)
    {
        var session = new SessionState(before);
        var targets = before.Tracks.SelectMany(static track => track.Chapters).Select(static chapter => chapter.Id).ToImmutableHashSet();
        var preview = ChapterContentOperationSession.Prepare(session, "expression", _ =>
            new ChapterCandidateBuildResult(true, candidate, targets, []));
        return ExpressionPreviewProjector.Build(preview);
    }

    private static EditableChapterDocument CreateDocument(IEnumerable<EditableChapter> chapters, ChapterFrameRate? rate) =>
        new(ChapterDocumentId.New(), "Title", "source.mkv", ChapterImportFormat.Ogm,
            ChapterDuration.Unknown, rate,
            [new EditableChapterTrack(ChapterTrackId.New(), "Main", chapters)]);

    private static EditableChapterDocument ReplaceChapters(EditableChapterDocument document, IEnumerable<EditableChapter> chapters) =>
        new(document.Id, document.Title, document.SourceName, document.ImportFormat, document.Duration,
            document.FrameRate,
            [new EditableChapterTrack(document.Tracks[0].Id, document.Tracks[0].Name, chapters, document.Tracks[0].Segments)]);

    private static EditableChapterDocument CreateDocumentCopy(EditableChapterDocument document) =>
        new(document.Id, document.Title, document.SourceName, document.ImportFormat, document.Duration,
            document.FrameRate,
            document.Tracks.Select(static track => new EditableChapterTrack(
                track.Id,
                track.Name,
                track.Chapters,
                track.Segments.Select(static segment => segment with
                {
                    ReferencedMediaFiles = [.. segment.ReferencedMediaFiles],
                    MediaTracks = [.. segment.MediaTracks]
                }))));

    private static EditableChapterDocument CreateDocumentWithMetadataSegment(IEnumerable<EditableChapter> chapters, ChapterFrameRate? rate)
    {
        var document = CreateDocument(chapters, rate);
        var track = document.Tracks[0];
        var segment = EditableChapterSegment.Create(
            track.Id,
            "entry-1",
            "Segment one",
            0,
            ChapterDuration.Unknown,
            rate,
            ChapterImportFormat.Ogm,
            "source.mkv",
            [new ReferencedMediaFile("source.mkv", "source.mkv")],
            [new ChapterImportMediaTrack("video", "1920x1080")]);
        return new EditableChapterDocument(
            document.Id,
            document.Title,
            document.SourceName,
            document.ImportFormat,
            document.Duration,
            document.FrameRate,
            [new EditableChapterTrack(track.Id, track.Name, track.Chapters, [segment])]);
    }
}
