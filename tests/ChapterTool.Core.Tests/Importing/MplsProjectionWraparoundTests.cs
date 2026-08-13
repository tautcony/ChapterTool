using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

/// <summary>
/// Verifies that malformed MPLS timing values (OUTTime &lt; INTime, unsorted marks)
/// clamp to zero instead of wrapping the unsigned subtraction to about 26.5 hours.
/// </summary>
public sealed class MplsProjectionWraparoundTests
{
    [Fact]
    public void CreateClampsWrappedPlayItemDurationToZero()
    {
        // The first play item is malformed (OUTTime < INTime); the second is one second long.
        var playlist = Playlist(
            [PlayItem(inTime: 1000, outTime: 500), PlayItem(inTime: 0, outTime: 45000)],
            marks: []);

        var projection = MplsPlaylistProjection.Create(playlist);

        Assert.Equal(TimeSpan.FromSeconds(1), projection.Duration);
        Assert.Equal(0UL, projection.PlayItemStartPts[1]);
    }

    [Fact]
    public void ChaptersForPlayItemClampsMarksBeforeOffsetToZero()
    {
        // The second mark is earlier than the offset (unsorted marks in a malformed file).
        var playlist = Playlist(
            [PlayItem(inTime: 1000, outTime: 90000)],
            [Mark(playItem: 0, timeStamp: 2000), Mark(playItem: 0, timeStamp: 500)]);

        var projection = MplsPlaylistProjection.Create(playlist);
        var chapters = projection.ChaptersForPlayItem(0);

        Assert.Equal(2, chapters.Count);
        Assert.Equal(MplsChapterImporter.PtsToTime(1000), chapters[0].StartTime);
        Assert.Equal(TimeSpan.Zero, chapters[1].StartTime);
    }

    private static MplsPlaylistFile Playlist(IReadOnlyList<MplsPlayItem> playItems, IReadOnlyList<MplsMark> marks) =>
        new(
            "MPLS",
            "0200",
            0,
            0,
            0,
            new MplsAppInfoPlayList(0, 0, 0, new MplsUOMaskTable(new byte[8]), 0),
            new MplsPlayList(0, (ushort)playItems.Count, 0, playItems, []),
            new MplsPlayListMark(0, (ushort)marks.Count, marks),
            null);

    private static MplsPlayItem PlayItem(uint inTime, uint outTime) =>
        new(
            0,
            new MplsClipName("00001", "M2TS"),
            0,
            0,
            inTime,
            outTime,
            new MplsUOMaskTable(new byte[8]),
            0,
            0,
            0,
            null,
            new MplsSTNTable(0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], [], [], [], [], []));

    private static MplsMark Mark(ushort playItem, uint timeStamp) =>
        new(0x01, playItem, timeStamp, 0, 0);
}
