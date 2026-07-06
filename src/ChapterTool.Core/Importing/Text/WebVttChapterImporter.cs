using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Core.Importing.Text;

public sealed class WebVttChapterImporter : IChapterImporter
{
    public string Id => "webvtt";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".vtt"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var text = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        return ImportText(text, request.Path);
    }

    public static ChapterImportResult ImportText(string text, string path = "")
    {
        text = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        var blocks = text.Split("\n\n");
        if (blocks.Length == 0 || !blocks[0].TrimStart().StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            return ChapterImportResult.Failed(Error("WebVttInvalidHeader", "WebVTT header is missing."));
        }

        var chapters = new List<Chapter>();
        foreach (var block in blocks.Skip(1).Where(static block => !string.IsNullOrWhiteSpace(block)))
        {
            var lines = block.Split('\n').SkipWhile(static line => !line.Contains("-->", StringComparison.Ordinal)).ToArray();
            if (lines.Length < 2)
            {
                return ChapterImportResult.Failed(Error("WebVttMalformedCue", $"Unable to parse WebVTT cue: {block}"));
            }

            var parts = lines[0].Split("-->", StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
            {
                var code = parts.Length == 2 && parts[1].Contains(' ', StringComparison.Ordinal)
                    ? "WebVttUnsupportedTimingSettings"
                    : "WebVttMalformedCue";
                return ChapterImportResult.Failed(Error(code, $"Unable to parse WebVTT timing line: {lines[0]}"));
            }

            chapters.Add(new Chapter(chapters.Count + 1, start, lines[1], End: end));
        }

        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error("WebVttMalformedCue", "No WebVTT cues were parsed."));
        }

        var info = new ChapterInfo(
            "WebVTT Chapters",
            Path.GetFileName(path),
            0,
            "WebVTT",
            0,
            chapters[^1].End ?? chapters[^1].Time,
            chapters);
        return TextImportUtilities.SingleGroup(path, info);
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}
