using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MplsMediaTrackProjectionTests
{
    [Theory]
    [InlineData(0x01, "MPEG-1", 1, 1, "480i24/1.001")]
    [InlineData(0x02, "MPEG-2", 2, 2, "576i24")]
    [InlineData(0x1B, "h264/AVC", 6, 3, "1080p25")]
    [InlineData(0x20, "MVC", 5, 4, "720p30/1.001")]
    [InlineData(0x24, "HEVC", 8, 6, "2160p50")]
    [InlineData(0xEA, "VC-1", 7, 7, "576p60/1.001")]
    public void ForPlayItem_maps_known_video_codes(byte codec, string expectedCodec, byte videoFormat, byte frameRate, string expectedFormat)
    {
        var playItem = PlayItem(video: Attributes(codec, videoFormat, frameRate));

        var tracks = MplsMediaTrackProjection.ForPlayItem(playItem);

        var track = Assert.Single(tracks);
        Assert.Equal("video", track.Kind);
        Assert.Equal(expectedCodec, track.Codec);
        Assert.Equal(expectedFormat, track.Format);
        Assert.Equal($"{expectedCodec}, {expectedFormat}", track.Summary);
    }

    [Theory]
    [InlineData(0x03, "MPEG-1 Audio")]
    [InlineData(0x04, "MPEG-2 Audio")]
    [InlineData(0x80, "RAW/PCM")]
    [InlineData(0x81, "AC3")]
    [InlineData(0x82, "DTS")]
    [InlineData(0x83, "TrueHD")]
    [InlineData(0x84, "AC3+")]
    [InlineData(0x85, "DTS-HD")]
    [InlineData(0x86, "DTS-HD Master Audio")]
    [InlineData(0xA1, "AC3+")]
    [InlineData(0xA2, "DTS-HD")]
    public void ForPlayItem_maps_known_audio_codes(byte codec, string expectedCodec)
    {
        var playItem = PlayItem(audio: Attributes(codec, audioFormat: 3, sampleRate: 14, languageCode: " JPN "));

        var track = Assert.Single(MplsMediaTrackProjection.ForPlayItem(playItem));

        Assert.Equal("audio", track.Kind);
        Assert.Equal(expectedCodec, track.Codec);
        Assert.Equal("jpn", track.Language);
        Assert.Equal("stereo", track.Channels);
        Assert.Equal("48/96kHz", track.SampleRate);
        Assert.Equal($"{expectedCodec}, [jpn], stereo, 48/96kHz", track.Summary);
    }

    [Fact]
    public void ForPlayItem_filters_unknown_codes_and_keeps_known_tracks()
    {
        var playItem = PlayItem(
            video: Attributes(0xFF, videoFormat: 6, frameRate: 2),
            audio: Attributes(0x80, audioFormat: 1, sampleRate: 1));

        var tracks = MplsMediaTrackProjection.ForPlayItem(playItem);

        var track = Assert.Single(tracks);
        Assert.Equal("RAW/PCM, mono, 48kHz", track.Summary);
    }

    [Fact]
    public void ForPlayItems_skips_empty_items_and_returns_first_track_set()
    {
        var tracks = MplsMediaTrackProjection.ForPlayItems(
        [
            PlayItem(),
            PlayItem(video: Attributes(0x24, videoFormat: 6, frameRate: 2)),
            PlayItem(audio: Attributes(0x80, audioFormat: 3, sampleRate: 1))
        ]);

        var track = Assert.Single(tracks);
        Assert.Equal("HEVC, 1080p24", track.Summary);
    }

    private static MplsPlayItem PlayItem(MplsStreamAttributes? video = null, MplsStreamAttributes? audio = null) =>
        new(
            0,
            new MplsClipName("00001", "M2TS"),
            0,
            0,
            0,
            0,
            new MplsUOMaskTable(new byte[8]),
            0,
            0,
            0,
            null,
            Stn(video, audio));

    private static MplsSTNTable Stn(MplsStreamAttributes? video, MplsStreamAttributes? audio) =>
        new(
            0,
            (byte)(video is null ? 0 : 1),
            (byte)(audio is null ? 0 : 1),
            0,
            0,
            0,
            0,
            0,
            0,
            video is null ? [] : [new MplsBasicStreamEntry(new MplsStreamEntry(0, 1, null, null, 0), video)],
            audio is null ? [] : [new MplsBasicStreamEntry(new MplsStreamEntry(0, 1, null, null, 0), audio)],
            [],
            [],
            [],
            [],
            [],
            []);

    private static MplsStreamAttributes Attributes(
        byte codec,
        byte? videoFormat = null,
        byte? frameRate = null,
        byte? audioFormat = null,
        byte? sampleRate = null,
        string? languageCode = null) =>
        new(0, codec, videoFormat, frameRate, null, null, null, null, audioFormat, sampleRate, null, languageCode);
}
