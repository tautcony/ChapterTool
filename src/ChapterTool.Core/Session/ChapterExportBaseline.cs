using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Session;

/// <summary>A read-only export snapshot that does not retain the history owner.</summary>
public sealed record ChapterExportSnapshot(
    Guid SessionGeneration,
    SessionSnapshot Content,
    ChapterTrackId TrackId,
    ChapterSet ChapterSet,
    ChapterSerializationOptions Options,
    string Digest,
    string FormatFingerprint);

/// <summary>Describes the last successful export for one track in one live session.</summary>
public sealed record ChapterExportBaseline(
    Guid StateIdentity,
    string Digest,
    string FormatFingerprint);

internal static class ChapterExportFingerprint
{
    public static string Digest(ChapterSet content, EditableChapterTrack track, ChapterSerializationOptions options)
    {
        var builder = new StringBuilder(Digest(content, options));
        Append(builder, track.FrameRate.Kind.ToString());
        Append(builder, track.FrameRate.ToString());
        foreach (var segment in track.Segments)
        {
            Append(builder, segment.FrameRate.Kind.ToString());
            Append(builder, segment.FrameRate.ToString());
            Append(builder, segment.SourceFrameRate.Kind.ToString());
            Append(builder, segment.SourceFrameRate.ToString());
        }

        return Hash(builder.ToString());
    }

    public static string Digest(ChapterSet content, ChapterSerializationOptions options)
    {
        var builder = new StringBuilder();
        Append(builder, content.Title);
        Append(builder, content.SourceName);
        Append(builder, content.ImportFormat.ToString());
        Append(builder, content.FrameRate.Kind.ToString());
        Append(builder, content.FrameRate.ToString());
        Append(builder, content.Duration.Ticks.ToString(CultureInfo.InvariantCulture));
        foreach (var chapter in content.Chapters)
        {
            Append(builder, chapter.DisplayNumber.ToString(CultureInfo.InvariantCulture));
            Append(builder, chapter.StartTime.Ticks.ToString(CultureInfo.InvariantCulture));
            Append(builder, chapter.Name);
            Append(builder, chapter.EndTime?.Ticks.ToString(CultureInfo.InvariantCulture));
            Append(builder, chapter.Kind.ToString());
        }

        AppendOptions(builder, options);
        return Hash(builder.ToString());
    }

    public static string FormatFingerprint(ChapterSerializationOptions options)
    {
        var builder = new StringBuilder();
        AppendOptions(builder, options);
        return Hash(builder.ToString());
    }

    private static void AppendOptions(StringBuilder builder, ChapterSerializationOptions options)
    {
        Append(builder, options.Format.ToString());
        Append(builder, options.XmlLanguage);
        Append(builder, options.SourceFileName);
        Append(builder, options.TextEncoding.ToString());
        Append(builder, options.EmitBom.ToString());
        Append(builder, options.ComputationRate?.Kind.ToString());
        Append(builder, options.ComputationRate?.ToString());
    }

    private static void Append(StringBuilder builder, string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
