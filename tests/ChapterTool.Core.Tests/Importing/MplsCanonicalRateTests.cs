using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MplsCanonicalRateTests
{
    [Theory]
    [InlineData(3, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void AggregatePreservesPerItemRatesAndAbsoluteBoundaries(byte secondCode, bool uniform)
    {
        var playlist = ReadPlaylist();
        var first = playlist.PlayList.PlayItems[0];
        var stream = first.STNTable.PrimaryVideoStreamEntries[0];
        var second = first with { STNTable = first.STNTable with
        {
            PrimaryVideoStreamEntries = [stream with { StreamAttributes = stream.StreamAttributes with { FrameRate = secondCode } }]
        } };
        var projection = MplsPlaylistProjection.Create(playlist with { PlayList = playlist.PlayList with { PlayItems = [first, second] } });
        var exact = FrameRateValue.Rational(24000, 1001);
        Assert.Equal(exact, projection.Segments[0].FrameRate);
        Assert.Equal(uniform ? exact : FrameRateValue.Unknown, projection.FrameRate);
        Assert.Equal(projection.Segments[0].Duration.Ticks, projection.Segments[1].StartTicks);
        Assert.Equal(projection.Duration.Ticks, projection.Segments[1].StartTicks + projection.Segments[1].Duration.Ticks);

        var entry = new ChapterImportEntry("playlist", "Playlist", projection.ToChapterSet()) { Segments = projection.Segments };
        var document = EditableChapterDocumentAdapter.FromChapterImportSource(new ChapterImportSource("playlist.mpls", [entry]));
        var segments = document.Tracks[0].Segments;
        Assert.Equal(2, segments.Length);
        Assert.Equal(projection.Segments.Select(static segment => segment.FrameRate), segments.Select(static segment => segment.FrameRate));
        Assert.All(segments, segment => Assert.Equal(segment.FrameRate, segment.SourceFrameRate));
        Assert.Equal(projection.FrameRate, document.FrameRate);
        Assert.True(EditableChapterDocumentValidator.Validate(document).IsValid);
    }

    [Fact]
    public void ConflictingOrMissingPrimaryVideoDoesNotUseFirstStream()
    {
        var playlist = ReadPlaylist();
        var item = playlist.PlayList.PlayItems[0];
        var video = item.STNTable.PrimaryVideoStreamEntries[0];
        var conflict = item with { STNTable = item.STNTable with
        {
            PrimaryVideoStreamEntries = [video, video with { StreamAttributes = video.StreamAttributes with { FrameRate = 3 } }]
        } };
        var missing = item with { STNTable = item.STNTable with { PrimaryVideoStreamEntries = [] } };
        var projection = MplsPlaylistProjection.Create(playlist with { PlayList = playlist.PlayList with { PlayItems = [conflict, missing] } });
        Assert.All(projection.Segments, segment => Assert.Equal(FrameRateValue.Unknown, segment.FrameRate));
        Assert.Contains(projection.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidFrameRate);
    }

    private static MplsPlaylistFile ReadPlaylist()
    {
        using var stream = File.OpenRead(FixtureResolver.Fixture("Importing", "Disc", "Mpls", "00020_Terminator2.mpls"));
        return MplsPlaylistFile.Read(stream);
    }
}
