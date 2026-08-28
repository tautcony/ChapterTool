using ChapterTool.Core.Importing.Disc.Clpi;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

internal static class MplsMediaTrackProjection
{
    internal static IReadOnlyList<ChapterImportMediaTrack> ForPlayItem(
        MplsPlayItem playItem,
        IReadOnlyDictionary<string, ClpiFile>? clpiByClip = null)
    {
        var aspectRatio = ResolveAspectRatio(playItem, clpiByClip);
        return
        [
            .. playItem.STNTable.PrimaryVideoStreamEntries
                .Select(entry => CreateVideoTrack(entry.StreamAttributes, aspectRatio))
                .Where(static track => track is not null)
                .Cast<ChapterImportMediaTrack>(),
            .. playItem.STNTable.PrimaryAudioStreamEntries
                .Select(entry => CreateAudioTrack(entry.StreamAttributes))
                .Where(static track => track is not null)
                .Cast<ChapterImportMediaTrack>()
        ];
    }

    internal static IReadOnlyList<ChapterImportMediaTrack> ForPlayItems(
        IEnumerable<MplsPlayItem> playItems,
        IReadOnlyDictionary<string, ClpiFile>? clpiByClip = null)
    {
        foreach (var playItem in playItems)
        {
            var tracks = ForPlayItem(playItem, clpiByClip);
            if (tracks.Count == 0)
            {
                continue;
            }

            return tracks;
        }

        return [];
    }

    private static ChapterImportMediaTrack? CreateVideoTrack(MplsStreamAttributes attributes, string? aspectRatio)
    {
        var codec = VideoCodec(attributes.StreamCodingType);
        if (codec is null)
        {
            return null;
        }

        var format = string.Concat(VideoFormat(attributes.VideoFormat), VideoFrameRate(attributes.FrameRate));
        var summary = string.IsNullOrWhiteSpace(format)
            ? codec
            : string.IsNullOrWhiteSpace(aspectRatio)
                ? $"{codec}, {format}"
                : $"{codec}, {format} ({aspectRatio})";

        return new ChapterImportMediaTrack(
            "video",
            summary,
            Codec: codec,
            Format: format,
            AspectRatio: aspectRatio);
    }

    private static ChapterImportMediaTrack? CreateAudioTrack(MplsStreamAttributes attributes)
    {
        var codec = AudioCodec(attributes.StreamCodingType);
        if (codec is null)
        {
            return null;
        }

        var language = NormalizeLanguage(attributes.LanguageCode);
        var channels = AudioFormat(attributes.AudioFormat);
        var sampleRate = AudioSampleRate(attributes.SampleRate);
        var parts = new List<string> { codec };
        if (!string.IsNullOrWhiteSpace(language))
        {
            parts.Add($"[{language}]");
        }

        if (!string.IsNullOrWhiteSpace(channels))
        {
            parts.Add(channels);
        }

        if (!string.IsNullOrWhiteSpace(sampleRate))
        {
            parts.Add(sampleRate);
        }

        return new ChapterImportMediaTrack(
            "audio",
            string.Join(", ", parts),
            Codec: codec,
            Language: language,
            Channels: channels,
            SampleRate: sampleRate);
    }

    private static string? ResolveAspectRatio(MplsPlayItem playItem, IReadOnlyDictionary<string, ClpiFile>? clpiByClip)
    {
        if (clpiByClip is null || clpiByClip.Count == 0)
        {
            return null;
        }

        foreach (var clipName in MplsPlaylistProjection.ClipNames(playItem))
        {
            if (!clpiByClip.TryGetValue(clipName, out var clpi))
            {
                continue;
            }

            var aspectCode = clpi.ProgramInfo?.Programs
                .SelectMany(static program => program.StreamCodingInfos)
                .Where(static info => VideoCodec(info.StreamCodingType) is not null)
                .Select(static info => info.VideoAspect)
                .FirstOrDefault(static aspect => aspect is not null);
            var aspectRatio = VideoAspect(aspectCode);
            if (!string.IsNullOrWhiteSpace(aspectRatio))
            {
                return aspectRatio;
            }
        }

        return null;
    }

    private static string? NormalizeLanguage(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToLowerInvariant();

    private static string? VideoCodec(byte code) => code switch
    {
        0x01 => "MPEG-1",
        0x02 => "MPEG-2",
        0x1B => "h264/AVC",
        0x20 => "MVC",
        0x24 => "HEVC",
        0xEA => "VC-1",
        _ => null
    };

    private static string? AudioCodec(byte code) => code switch
    {
        0x03 => "MPEG-1 Audio",
        0x04 => "MPEG-2 Audio",
        0x80 => "RAW/PCM",
        0x81 => "AC3",
        0x82 => "DTS",
        0x83 => "TrueHD",
        0x84 => "AC3+",
        0x85 => "DTS-HD",
        0x86 => "DTS-HD Master Audio",
        0xA1 => "AC3+",
        0xA2 => "DTS-HD",
        _ => null
    };

    private static string VideoFormat(byte? code) => code switch
    {
        1 => "480i",
        2 => "576i",
        3 => "480p",
        4 => "1080i",
        5 => "720p",
        6 => "1080p",
        7 => "576p",
        8 => "2160p",
        _ => string.Empty
    };

    private static string VideoFrameRate(byte? code) => code switch
    {
        1 => "24/1.001",
        2 => "24",
        3 => "25",
        4 => "30/1.001",
        6 => "50",
        7 => "60/1.001",
        _ => string.Empty
    };

    private static string? VideoAspect(byte? code) => code switch
    {
        1 => "4:3",
        2 => "16:9",
        _ => null
    };

    private static string? AudioFormat(byte? code) => code switch
    {
        1 => "mono",
        3 => "stereo",
        6 => "multi-channel",
        12 => "combo",
        _ => null
    };

    private static string? AudioSampleRate(byte? code) => code switch
    {
        1 => "48kHz",
        4 => "96kHz",
        5 => "192kHz",
        12 => "48/192kHz",
        14 => "48/96kHz",
        _ => null
    };
}
