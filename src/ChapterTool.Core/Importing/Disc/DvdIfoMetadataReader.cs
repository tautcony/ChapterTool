using System.Globalization;
using System.Text;
using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Disc;

internal static class DvdIfoMetadataReader
{
    private const int MaximumAudioStreams = 8;
    private const int MaximumSubtitleStreams = 32;
    private const int VtsVideoAttributesOffset = 0x200;
    private const int VtsAudioCountOffset = 0x202;
    private const int VtsAudioAttributesOffset = 0x204;
    private const int VtsSubtitleCountOffset = 0x254;
    private const int VtsSubtitleAttributesOffset = 0x256;

    internal static IReadOnlyList<ChapterImportMediaTrack> ReadTitleSetTracks(string path)
    {
        var requiredLength = VtsSubtitleAttributesOffset + MaximumSubtitleStreams * 6;
        var file = new FileInfo(path);
        if (!PortableInputPolicy.IsWithinLimit(file.Length) || file.Length < requiredLength)
        {
            return [];
        }

        var bytes = new byte[requiredLength];
        using (var stream = File.OpenRead(path))
        {
            stream.ReadExactly(bytes);
        }

        var tracks = new List<ChapterImportMediaTrack>();
        var video = ReadVideoTrack(bytes.AsSpan(VtsVideoAttributesOffset, 2));
        if (video is not null)
        {
            tracks.Add(video);
        }

        var audioCount = Math.Min((int)ReadUInt16(bytes, VtsAudioCountOffset), MaximumAudioStreams);
        for (var index = 0; index < audioCount; index++)
        {
            var attributes = bytes.AsSpan(VtsAudioAttributesOffset + index * 8, 8);
            var track = ReadAudioTrack(index + 1, attributes);
            if (track is not null)
            {
                tracks.Add(track);
            }
        }

        var subtitleCount = Math.Min((int)ReadUInt16(bytes, VtsSubtitleCountOffset), MaximumSubtitleStreams);
        for (var index = 0; index < subtitleCount; index++)
        {
            var attributes = bytes.AsSpan(VtsSubtitleAttributesOffset + index * 6, 6);
            var track = ReadSubtitleTrack(index + 1, attributes);
            if (track is not null)
            {
                tracks.Add(track);
            }
        }

        return tracks;
    }

    internal static ChapterDiagnostic CreateDiscLoadedDiagnostic(string vmgPath, IReadOnlyList<ChapterImportEntry> entries)
    {
        var bytes = new byte[0x60];
        using (var stream = File.OpenRead(vmgPath))
        {
            stream.ReadExactly(bytes);
        }
        var root = Path.GetDirectoryName(vmgPath) ?? string.Empty;
        var titleSets = Directory.Exists(root)
            ? EnumerateFiles(root, "VTS_*_0.IFO", 99)
                .Concat(EnumerateFiles(root, "VTS_*_0.BUP", 99))
                .DistinctBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        var titleVobFiles = Enumerable.Range(1, 9)
            .Sum(number => EnumerateFiles(root, $"VTS_*_{number}.VOB", 99).Count);
        var tracks = entries
            .SelectMany(static entry => entry.MediaTracks ?? [])
            .DistinctBy(static track => (track.Kind, track.Summary))
            .ToArray();
        var provider = Encoding.ASCII.GetString(bytes, 0x40, 32).Trim('\0', ' ');
        var volumeCount = ReadUInt16(bytes, 0x26);
        var volumeNumber = ReadUInt16(bytes, 0x28);
        var declaredTitleSetCount = ReadUInt16(bytes, 0x3E);
        var chapterCount = entries.Sum(static entry => entry.ChapterSet.Chapters.Count);
        var trackSummary = string.Join(", ", tracks
            .GroupBy(static track => track.Kind, StringComparer.Ordinal)
            .OrderBy(static group => TrackKindOrder(group.Key))
            .Select(static group => $"{group.Count()} {group.Key} track{(group.Count() == 1 ? string.Empty : "s")}"));
        var summary = string.IsNullOrWhiteSpace(trackSummary) ? "no stream attributes" : trackSummary;

        return new ChapterDiagnostic(
            DiagnosticSeverity.Info,
            ChapterDiagnosticCode.IfoDiscLoaded,
            $"Loaded DVD disc metadata: {entries.Count} title{(entries.Count == 1 ? string.Empty : "s")}, {chapterCount} chapter{(chapterCount == 1 ? string.Empty : "s")}, {titleSets.Length} title set{(titleSets.Length == 1 ? string.Empty : "s")}; {summary}.",
            vmgPath,
            Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["provider"] = provider,
                ["volumeCount"] = volumeCount,
                ["volumeNumber"] = volumeNumber,
                ["regionMask"] = bytes[0x23],
                ["declaredTitleSetCount"] = declaredTitleSetCount,
                ["discoveredTitleSets"] = titleSets,
                ["titleSetCount"] = titleSets.Length,
                ["titleVobCount"] = titleVobFiles,
                ["menuVobPresent"] = File.Exists(Path.Combine(root, "VIDEO_TS.VOB")),
                ["backupVmgPresent"] = File.Exists(Path.Combine(root, "VIDEO_TS.BUP")),
                ["titleCount"] = entries.Count,
                ["chapterCount"] = chapterCount,
                ["mediaTracks"] = tracks.Select(static track => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = track.Kind,
                    ["summary"] = track.Summary,
                    ["codec"] = track.Codec ?? string.Empty,
                    ["language"] = track.Language ?? string.Empty,
                    ["format"] = track.Format ?? string.Empty,
                    ["channels"] = track.Channels ?? string.Empty,
                    ["sampleRate"] = track.SampleRate ?? string.Empty,
                    ["aspectRatio"] = track.AspectRatio ?? string.Empty
                }).Cast<object?>().ToList(),
                ["titles"] = entries.Select(static entry => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = entry.Id,
                    ["title"] = entry.ChapterSet.Title,
                    ["source"] = entry.ChapterSet.SourceName ?? string.Empty,
                    ["chapterCount"] = entry.ChapterSet.Chapters.Count,
                    ["duration"] = entry.ChapterSet.Duration,
                    ["mediaFiles"] = entry.ReferencedMediaFiles?.Select(static file => file.DisplayName).ToList() ?? []
                }).Cast<object?>().ToList()
            });
    }

    private static ChapterImportMediaTrack? ReadVideoTrack(ReadOnlySpan<byte> attributes)
    {
        var coding = attributes[0] >> 6;
        var standard = attributes[0] >> 4 & 0x03;
        var aspectCode = attributes[0] >> 2 & 0x03;
        var sizeCode = attributes[1] >> 2 & 0x03;
        var codec = coding switch { 0 => "MPEG-1", 1 => "MPEG-2", _ => null };
        var standardName = standard switch { 0 => "NTSC", 1 => "PAL", _ => null };
        var dimensions = standard switch
        {
            0 => sizeCode switch { 0 => "720x480", 1 => "704x480", 2 => "352x480", _ => "352x240" },
            1 => sizeCode switch { 0 => "720x576", 1 => "704x576", 2 => "352x576", _ => "352x288" },
            _ => null
        };
        var aspect = aspectCode switch { 0 => "4:3", 3 => "16:9", _ => null };
        if (codec is null || standardName is null || dimensions is null)
        {
            return null;
        }

        var format = $"{standardName} {dimensions}";
        var summary = aspect is null ? $"{codec}, {format}" : $"{codec}, {format} ({aspect})";
        return new ChapterImportMediaTrack("video", summary, codec, format, AspectRatio: aspect);
    }

    private static ChapterImportMediaTrack? ReadAudioTrack(int number, ReadOnlySpan<byte> attributes)
    {
        if (attributes.IndexOfAnyExcept((byte)0) < 0)
        {
            return null;
        }

        var codec = (attributes[0] >> 5) switch
        {
            0 => "AC3",
            2 => "MPEG-1 Audio",
            3 => "MPEG-2 Audio",
            4 => "LPCM",
            6 => "DTS",
            _ => "DVD audio"
        };
        var language = ((attributes[0] >> 2) & 0x03) == 1
            ? NormalizeLanguage(attributes[2], attributes[3])
            : null;
        var channelCountCode = attributes[1] & 0x07;
        var channels = channelCountCode switch
        {
            0 => "mono",
            1 => "stereo",
            2 => "3ch",
            3 => "4ch",
            4 => "5ch",
            5 => "6ch",
            6 => "7ch",
            7 => "8ch",
            _ => null
        };
        var sampleRate = ((attributes[1] >> 4) & 0x03) switch
        {
            0 => "48kHz",
            1 => "96kHz",
            2 => "192kHz",
            _ => null
        };
        var parts = new List<string> { codec };
        if (language is not null)
        {
            parts.Add($"[{language}]");
        }

        if (channels is not null)
        {
            parts.Add(channels);
        }

        if (sampleRate is not null)
        {
            parts.Add(sampleRate);
        }

        if (codec == "LPCM")
        {
            var bitDepth = ((attributes[1] >> 6) & 0x03) switch { 0 => "16-bit", 1 => "20-bit", 2 => "24-bit", _ => null };
            if (bitDepth is not null)
            {
                parts.Add(bitDepth);
            }
        }

        return new ChapterImportMediaTrack("audio", $"Audio {number}: {string.Join(", ", parts)}", codec, Language: language, Channels: channels, SampleRate: sampleRate);
    }

    private static ChapterImportMediaTrack? ReadSubtitleTrack(int number, ReadOnlySpan<byte> attributes)
    {
        if (attributes.IndexOfAnyExcept((byte)0) < 0)
        {
            return null;
        }

        var language = (attributes[0] & 0x03) == 1
            ? NormalizeLanguage(attributes[2], attributes[3])
            : null;
        var summary = language is null ? $"DVD subtitles {number}" : $"DVD subtitles {number}, [{language}]";
        return new ChapterImportMediaTrack("subtitle", summary, "DVD subpicture", Language: language);
    }

    private static string? NormalizeLanguage(byte first, byte second)
    {
        if (!char.IsAsciiLetter((char)first) || !char.IsAsciiLetter((char)second))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo($"{(char)first}{(char)second}").ThreeLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return $"{(char)first}{(char)second}".ToLowerInvariant();
        }
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

    private static IReadOnlyList<string> EnumerateFiles(string directory, string pattern, int limit)
    {
        try
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Take(limit).ToArray()
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static int TrackKindOrder(string kind) => kind switch
    {
        "video" => 0,
        "audio" => 1,
        "subtitle" => 2,
        _ => 3
    };
}
