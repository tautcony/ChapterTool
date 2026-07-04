using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

public sealed partial class OgmChapterImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    public string Id => "ogm-text";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var text = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        return ImportText(text, request.Path);
    }

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
        var info = new ChapterInfo(
            "OGM Chapters",
            Path.GetFileName(path),
            0,
            "OGM",
            0,
            chapters[^1].Time,
            chapters);
        var option = new ChapterSourceOption("default", "OGM Chapters", info);
        var group = new ChapterInfoGroup(path, [option]);
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
