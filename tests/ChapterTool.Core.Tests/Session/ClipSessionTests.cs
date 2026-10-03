using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Core.Tests.Session;

public sealed class ClipSessionTests
{
    [Fact]
    public void FromLoad_keeps_selector_metadata_without_retaining_chapter_values()
    {
        var source = MultiMplsGroup();
        var session = ClipSessionTransitions.FromLoad(source);

        Assert.IsType<SplitClipSession>(session);
        Assert.False(session.IsCombined);
        Assert.Equal(0, session.SelectedIndex);
        Assert.Equal(2, session.ClipOptions.Count);
        Assert.True(session.CanCombine);
        Assert.True(session.CanAppendMpls);
        Assert.Equal("00001", session.ClipOptions[0].DisplayName);
        Assert.All(session.OriginalGroup.Entries, static entry => Assert.Empty(entry.ChapterSet.Chapters));
        Assert.All(source.Entries, static entry => Assert.NotEmpty(entry.ChapterSet.Chapters));
    }

    [Fact]
    public void Select_updates_index_without_changing_session_identity()
    {
        var session = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var sessionId = session.SessionId;

        var selected = ClipSessionTransitions.Select(session, 1);

        Assert.Equal(1, selected.SelectedIndex);
        Assert.Equal(sessionId, selected.SessionId);
        Assert.False(selected.IsCombined);
        Assert.Empty(selected.ClipOptions[1].ChapterSet.Chapters);
    }

    [Fact]
    public void Metadata_transition_never_retains_a_second_editable_chapter_collection()
    {
        var split = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(split).Session);

        Assert.True(combined.IsCombined);
        Assert.Single(combined.ClipOptions);
        Assert.Equal(2, combined.OriginalGroup.Entries.Count);
        Assert.All(combined.OriginalGroup.Entries, static entry => Assert.Empty(entry.ChapterSet.Chapters));
        Assert.Empty(combined.CombinedEntry.ChapterSet.Chapters);
        Assert.NotEqual(split.SessionId, combined.SessionId);
    }

    [Fact]
    public void Append_metadata_transition_adds_only_source_metadata()
    {
        var split = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var appended = SingleMplsGroup("append.mpls", "Append");

        var result = ClipSessionTransitions.Append(split, appended);

        Assert.True(result.Succeeded);
        var combined = Assert.IsType<CombinedClipSession>(result.Session);
        Assert.Equal(3, combined.OriginalGroup.Entries.Count);
        Assert.All(combined.OriginalGroup.Entries, static entry => Assert.Empty(entry.ChapterSet.Chapters));
        Assert.Empty(combined.CombinedEntry.ChapterSet.Chapters);
    }

    [Fact]
    public void Combined_option_deduplicates_media_tracks_from_source_metadata()
    {
        var video = new ChapterImportMediaTrack("video", "h264/AVC, 1080p24", Codec: "h264/AVC", Format: "1080p24");
        var audio = new ChapterImportMediaTrack("audio", "RAW/PCM, stereo", Codec: "RAW/PCM", Channels: "stereo");
        var group = new ChapterImportSource(
            "movie.mpls",
            [
                new ChapterImportEntry("clip-0", "00001", Info(ChapterImportFormat.Mpls, "00001", new Chapter(1, TimeSpan.Zero, "A")), MediaTracks: [video, audio]),
                new ChapterImportEntry("clip-1", "00002", Info(ChapterImportFormat.Mpls, "00002", new Chapter(1, TimeSpan.FromSeconds(1), "B")), MediaTracks: [video, audio])
            ]);
        var combined = ClipSessionTransitions.CreateCombinedClipOption(group, Info(ChapterImportFormat.Mpls, "combined"));

        Assert.Equal([video, audio], combined.MediaTracks);
        Assert.Empty(combined.ChapterSet.Chapters);
    }

    [Fact]
    public void FromDocument_rebuilds_selector_metadata_from_current_segments()
    {
        var original = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var document = EditableChapterDocumentAdapter.FromChapterImportSource(MultiMplsGroup());
        var merged = ChapterTool.Core.Editing.ChapterClipCandidateBuilder.MergeTracks(document, document.Tracks.Select(static track => track.Id));
        Assert.True(merged.IsValid);

        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.FromDocument(original, merged.Candidate, combined: true));

        Assert.Equal(2, combined.OriginalGroup.Entries.Count);
        Assert.Equal(["00001", "00002"], combined.OriginalGroup.Entries.Select(static entry => entry.DisplayName));
        Assert.All(combined.OriginalGroup.Entries, static entry => Assert.Empty(entry.ChapterSet.Chapters));
        Assert.Empty(combined.CombinedEntry.ReferencedMediaFiles!);
    }

    [Fact]
    public void FromDocument_assigns_boundary_marker_to_next_segment_and_keeps_source_count_separate()
    {
        var sourceRate = new ChapterFrameRate(24, 1);
        var firstTrackId = ChapterTrackId.New();
        var secondTrackId = ChapterTrackId.New();
        var first = EditableChapterSegment.Create(firstTrackId, "first", "First", 0,
            ChapterDuration.FromTicks(TimeSpan.FromSeconds(5).Ticks), sourceRate, ChapterImportFormat.Mpls,
            "first.mpls", chapterCount: 6, sourceChapterCount: 6);
        var second = EditableChapterSegment.Create(secondTrackId, "second", "Second", TimeSpan.FromSeconds(5).Ticks,
            ChapterDuration.FromTicks(TimeSpan.FromSeconds(10).Ticks), sourceRate, ChapterImportFormat.Mpls,
            "second.mpls", chapterCount: 0, sourceChapterCount: 0);
        var chapters = Enumerable.Range(0, 6).Select(index => new EditableChapter(
            ChapterId.New(), index + 1, TimeSpan.FromSeconds(index).Ticks, $"C{index + 1}")).ToArray();
        var document = new EditableChapterDocument(ChapterDocumentId.New(), "Merged", "movie.mpls", ChapterImportFormat.Mpls,
            ChapterDuration.FromTicks(TimeSpan.FromSeconds(15).Ticks), sourceRate,
            [new EditableChapterTrack(ChapterTrackId.New(), "Merged", chapters, [first, second])]);
        var basis = ClipSessionTransitions.FromLoad(MultiMplsGroup());

        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.FromDocument(basis, document, combined: true));

        Assert.Equal([5, 1], combined.OriginalGroup.Entries.Select(static entry => entry.ChapterCount));
        Assert.Equal([6, 0], combined.OriginalGroup.Entries.Select(static entry => entry.SourceChapterCount));
    }

    [Fact]
    public void FromLoad_clears_previous_combined_identity_and_single_entry_cannot_combine()
    {
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(ClipSessionTransitions.FromLoad(MultiMplsGroup())).Session);
        var second = ClipSessionTransitions.FromLoad(SingleMplsGroup("other.mpls", "Other"));

        Assert.IsType<SplitClipSession>(second);
        Assert.False(second.IsCombined);
        Assert.NotEqual(combined.SessionId, second.SessionId);
        Assert.Single(second.ClipOptions);
        Assert.False(second.CanCombine);
        Assert.True(second.CanAppendMpls);
    }

    private static ChapterImportSource MultiMplsGroup() => new(
        "movie.mpls",
        [
            new ChapterImportEntry("clip-0", "00001", Info(ChapterImportFormat.Mpls, "00001",
                new Chapter(1, TimeSpan.Zero, "A"), new Chapter(2, TimeSpan.FromSeconds(10), "B"))),
            new ChapterImportEntry("clip-1", "00002", Info(ChapterImportFormat.Mpls, "00002",
                new Chapter(1, TimeSpan.Zero, "C"), new Chapter(2, TimeSpan.FromSeconds(5), "D")))
        ]);

    private static ChapterImportSource SingleMplsGroup(string path, string name) => new(
        path,
        [new ChapterImportEntry("clip-0", name, Info(ChapterImportFormat.Mpls, name, new Chapter(1, TimeSpan.Zero, name)))]);

    private static ChapterSet Info(ChapterImportFormat format, string sourceName, params Chapter[] chapters) => new(
        sourceName,
        sourceName,
        format,
        24000d / 1001d,
        chapters.Length == 0 ? TimeSpan.FromSeconds(15) : chapters[^1].StartTime + TimeSpan.FromSeconds(1),
        chapters);
}
