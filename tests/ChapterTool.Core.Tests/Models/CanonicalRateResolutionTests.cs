using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Models;

public sealed class CanonicalRateResolutionTests
{
    [Fact]
    public void UniformResolutionIncludesMissingAndDifferentlyClassifiedContributors()
    {
        var exact = FrameRateValue.Rational(24, 1);
        Assert.Equal(exact, FrameRateValue.ResolveUniform([exact, FrameRateValue.Rational(48, 2)]));
        Assert.Equal(FrameRateValue.Unknown, FrameRateValue.ResolveUniform([]));
        Assert.Equal(FrameRateValue.Unknown, FrameRateValue.ResolveUniform([exact, FrameRateValue.Unknown]));
        Assert.Equal(FrameRateValue.Unknown, FrameRateValue.ResolveUniform([exact, FrameRateValue.Approximate(24)]));
        Assert.Equal(FrameRateValue.Unknown, FrameRateValue.ResolveUniform([exact, FrameRateValue.Rational(25, 1)]));
    }

    [Fact]
    public void SelectedTrackDoesNotBorrowAnotherTracksRate()
    {
        var unknown = new EditableChapterTrack(ChapterTrackId.New(), "Unknown", []);
        var known = new EditableChapterTrack(ChapterTrackId.New(), "Known", [], frameRate: FrameRateValue.Rational(24, 1));
        var document = new EditableChapterDocument(ChapterDocumentId.New(), "Title", null,
            ChapterImportFormat.Unknown, ChapterDuration.Unknown, [unknown, known]);
        Assert.Equal(FrameRateValue.Unknown, document.FrameRate);
        Assert.Equal(FrameRateValue.Unknown, EditableChapterDocumentAdapter.ToChapterSet(document, 0).FrameRate);
        Assert.Equal(FrameRateValue.Rational(24, 1), EditableChapterDocumentAdapter.ToChapterSet(document, 1).FrameRate);
        Assert.Equal(FrameRateValue.Unknown, new EditableChapterDocument(ChapterDocumentId.New(), "Empty", null,
            ChapterImportFormat.Unknown, ChapterDuration.Unknown, []).FrameRate);
    }

    [Fact]
    public void SegmentSummaryIncludesEverySegmentAndPreservesOriginalUnknown()
    {
        var id = ChapterTrackId.New();
        var exact = FrameRateValue.Rational(24, 1);
        var first = EditableChapterSegment.Create(id, "a", "A", 0, ChapterDuration.FromTicks(10), exact,
            ChapterImportFormat.Mpls, null, sourceFrameRate: FrameRateValue.Unknown);
        var second = first with { Id = Guid.NewGuid(), StartTicks = 10, FrameRate = FrameRateValue.Unknown };
        var track = new EditableChapterTrack(id, "Mixed", [], [first, second], exact);
        Assert.Equal(FrameRateValue.Unknown, track.FrameRate);
        Assert.Equal(FrameRateValue.Unknown, first.SourceFrameRate);
        Assert.Equal(exact, first.FrameRate);
    }
}
