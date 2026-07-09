using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

/// <summary>
/// Imports WebVTT cue starts as chapter markers.
/// </summary>
public sealed class WebVttChapterImporter : IChapterImporter
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
            if (parts.Length != 2 || !TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
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

    private static ChapterDiagnostic Error(ChapterDiagnosticCode code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}
