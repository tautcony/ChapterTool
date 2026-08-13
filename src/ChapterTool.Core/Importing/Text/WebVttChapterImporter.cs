using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

/// <summary>
/// Imports WebVTT cue starts as chapter markers.
/// </summary>
public sealed partial class WebVttChapterImporter : IChapterImporter
{
    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "webvtt";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".vtt"
    };

    /// <summary>
    /// Imports chapters from the supplied request.
    /// </summary>
    /// <param name="request">The import request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var text = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        return ImportText(text, request.Path);
    }

    /// <summary>
    /// Imports chapters from text content.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="path">The source path.</param>
    /// <returns>The operation result.</returns>
    public static ChapterImportResult ImportText(string text, string path = "")
    {
        text = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        var blocks = text.Split("\n\n");
        if (blocks.Length == 0 || !blocks[0].TrimStart().StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.WebVttInvalidHeader, "WebVTT header is missing."));
        }

        var chapters = new List<Chapter>();
        foreach (var block in blocks.Skip(1).Where(static block => !string.IsNullOrWhiteSpace(block)))
        {
            var lines = block.Split('\n').SkipWhile(static line => !line.Contains("-->", StringComparison.Ordinal)).ToArray();
            if (lines.Length < 2)
            {
                return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.WebVttMalformedCue, $"Unable to parse WebVTT cue: {block}"));
            }

            var parts = lines[0].Split("-->", StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TryParseTimestamp(parts[0], out var start) || !TryParseTimestamp(parts[1], out var end))
            {
                var code = parts.Length == 2 && parts[1].Contains(' ', StringComparison.Ordinal)
                    ? ChapterDiagnosticCode.WebVttUnsupportedTimingSettings
                    : ChapterDiagnosticCode.WebVttMalformedCue;
                return ChapterImportResult.Failed(Error(code, $"Unable to parse WebVTT timing line: {lines[0]}"));
            }

            chapters.Add(new Chapter(chapters.Count + 1, start, lines[1], EndTime: end));
        }

        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.WebVttMalformedCue, "No WebVTT cues were parsed."));
        }

        var info = new ChapterSet(
            "WebVTT Chapters",
            Path.GetFileName(path),
            ChapterImportFormat.WebVtt,
            0,
            chapters[^1].EndTime ?? chapters[^1].StartTime,
            chapters);
        return TextImportUtilities.SingleGroup(path, info);
    }

    // The WebVTT timestamp grammar is [hh…:]mm:ss.ttt. The hour component is
    // optional, has two or more digits, and may exceed 24. TimeSpan.TryParse
    // rejects both the short form and hours >= 24, so parse the grammar directly.
    private static bool TryParseTimestamp(string text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var match = TimestampRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var hours = 0L;
        if (match.Groups["Hours"].Success
            && !long.TryParse(match.Groups["Hours"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        if (hours > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerHour)
        {
            return false;
        }

        var minutes = int.Parse(match.Groups["Minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups["Seconds"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var milliseconds = int.Parse(match.Groups["Milliseconds"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var ticks = hours * TimeSpan.TicksPerHour
            + minutes * TimeSpan.TicksPerMinute
            + seconds * TimeSpan.TicksPerSecond
            + milliseconds * TimeSpan.TicksPerMillisecond;
        if (ticks < 0)
        {
            return false;
        }

        value = TimeSpan.FromTicks(ticks);
        return true;
    }

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    [GeneratedRegex(@"^(?:(?<Hours>\d{2,}):)?(?<Minutes>[0-5]\d):(?<Seconds>[0-5]\d)\.(?<Milliseconds>\d{3})$")]
    private static partial Regex TimestampRegex();
}
