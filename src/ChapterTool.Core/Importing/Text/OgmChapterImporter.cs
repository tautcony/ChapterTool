using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

/// <summary>
/// Imports OGM-style chapter text.
/// </summary>
/// <param name="timeFormatter">The chapter time formatter.</param>
public sealed partial class OgmChapterImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    /// <summary>
    /// Gets the stable importer identifier.
    /// </summary>
    public string Id => "ogm-text";

    /// <summary>
    /// Gets the supported file extensions for this importer.
    /// </summary>
    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt"
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
    public ChapterImportResult ImportText(string text, string path = "")
    {
        var diagnostics = new List<ChapterDiagnostic>();
        var lines = text.Trim(' ', '\t', '\r', '\n').Split('\n');
        if (lines.Length == 0 || !TimeLineRegex().IsMatch(lines[0]))
        {
            return ChapterImportResult.Failed(Error("OgmInvalidFirstLine", "The first OGM chapter line is missing or invalid."));
        }

        var firstTimeText = TimeValueRegex().Match(lines[0]).Value;
        var initialTime = timeFormatter.ParseOrZero(firstTimeText);
        var chapters = new List<Chapter>();
        var state = State.Time;
        var timeCode = TimeSpan.Zero;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (state == State.Time)
            {
                var match = TimeLineRegex().Match(line);
                if (!match.Success)
                {
                    return PartialOrFailure(path, chapters, diagnostics, line);
                }

                timeCode = timeFormatter.ParseOrZero(TimeValueRegex().Match(line).Value) - initialTime;
                state = State.Name;
                continue;
            }

            var name = NameLineRegex().Match(line);
            if (!name.Success)
            {
                return PartialOrFailure(path, chapters, diagnostics, line);
            }

            chapters.Add(new Chapter(chapters.Count + 1, timeCode, name.Groups["Name"].Value.Trim('\r')));
            state = State.Time;
        }

        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error("EmptyChapters", "No OGM chapters were parsed."));
        }

        if (state == State.Name)
        {
            diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, "PartialParse", "Parsing stopped after a chapter time without a matching name."));
            return Success(path, chapters, diagnostics, isPartial: true);
        }

        return Success(path, chapters, diagnostics, isPartial: false);
    }

    private static ChapterImportResult PartialOrFailure(
        string path,
        List<Chapter> chapters,
        List<ChapterDiagnostic> diagnostics,
        string line)
    {
        if (chapters.Count == 0)
        {
            return ChapterImportResult.Failed(Error("InvalidChapterPair", $"Unable to parse OGM chapter line: {line}",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["line"] = line }));
        }

        diagnostics.Add(new ChapterDiagnostic(DiagnosticSeverity.Warning, "PartialParse", $"Parsing stopped at line: {line}"));
        return Success(path, chapters, diagnostics, isPartial: true);
    }

    private static ChapterImportResult Success(
        string path,
        IReadOnlyList<Chapter> chapters,
        IReadOnlyList<ChapterDiagnostic> diagnostics,
        bool isPartial)
    {
        var info = new ChapterSet(
            "OGM Chapters",
            Path.GetFileName(path),
            ChapterImportFormat.Ogm,
            0,
            chapters[^1].StartTime,
            chapters);
        var entry = new ChapterImportEntry("default", "OGM Chapters", info);
        var group = new ChapterImportSource(path, [entry]);
        return new ChapterImportResult(true, [group], diagnostics, isPartial);
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    private static ChapterDiagnostic Error(string code, string message, IReadOnlyDictionary<string, object?> arguments) =>
        new(DiagnosticSeverity.Error, code, message, Arguments: arguments);

    private enum State
    {
        Time,
        Name
    }

    [GeneratedRegex(@"^\s*CHAPTER\d+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex TimeLineRegex();

    [GeneratedRegex(@"(?<Time>\d+\s*:\s*\d+\s*:\s*\d+\s*[\.,]\s*\d{3})")]
    private static partial Regex TimeValueRegex();

    [GeneratedRegex(@"^\s*CHAPTER\d+NAME\s*=\s*(?<Name>.*)", RegexOptions.IgnoreCase)]
    private static partial Regex NameLineRegex();
}
