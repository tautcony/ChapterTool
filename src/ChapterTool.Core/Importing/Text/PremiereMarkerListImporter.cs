using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing.Text;

public sealed partial class PremiereMarkerListImporter(IChapterTimeFormatter timeFormatter) : IChapterImporter
{
    private static readonly decimal[] CommonFrameRates = [23.976M, 24M, 25M, 29.97M, 30M, 50M, 59.94M, 60M];

    public string Id => "premiere-marker-list";

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".txt"
    };

    public async ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
    {
        var text = await TextImportUtilities.ReadTextAsync(request, cancellationToken);
        return ImportText(text, request.Path);
    }

    public bool CanImportText(string text) => TryParse(text, path: string.Empty, out _);

    public ChapterImportResult ImportText(string text, string path = "")
    {
        if (TryParse(text, path, out var result))
        {
            return result;
        }

        return ChapterImportResult.Failed(Error("PremiereMarkerListInvalid", "Unable to parse Adobe Premiere Pro chapter marker list."));
    }

    private bool TryParse(string text, string path, out ChapterImportResult result)
    {
        result = ChapterImportResult.Failed();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lines = NormalizeLines(text);
        if (lines.Length < 2)
        {
            return false;
        }

        var separator = DetectSeparator(lines[0]);
        if (separator == '\0')
        {
            return false;
        }

        var headers = SplitLine(lines[0], separator).Select(NormalizeHeader).ToArray();
        if (!headers.Any(static header => header.Contains("marker", StringComparison.Ordinal)))
        {
            return false;
        }

        var timeIndex = FindColumn(headers, "in", "start", "time", "timestamp");
        if (timeIndex < 0)
        {
            return false;
        }

        var typeIndex = FindColumn(headers, "markertype", "type");
        var nameIndex = FindColumn(headers, "markername", "chaptername", "name", "title");
        var commentIndex = FindColumn(headers, "comment", "comments", "description", "notes", "note");
        var chapters = new List<Chapter>();

        foreach (var line in lines.Skip(1))
        {
            var columns = SplitLine(line, separator);
            if (columns.Length <= timeIndex)
            {
                continue;
            }

            if (typeIndex >= 0)
            {
                var markerType = GetColumnValue(columns, typeIndex);
                if (!string.IsNullOrWhiteSpace(markerType) &&
                    !markerType.Contains("chapter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (!TryParseTime(GetColumnValue(columns, timeIndex), out var time))
            {
                continue;
            }

            var name = FirstNonEmpty(
                GetColumnValue(columns, nameIndex),
                GetColumnValue(columns, commentIndex),
                $"Chapter {chapters.Count + 1:D2}");

            chapters.Add(new Chapter(chapters.Count + 1, time, name));
        }

        if (chapters.Count == 0)
        {
            return false;
        }

        var info = new ChapterInfo(
            "Adobe Premiere Pro Markers",
            Path.GetFileName(path),
            0,
            "Adobe Premiere Pro",
            0,
            chapters[^1].Time,
            chapters,
            Tag: text,
            TagType: typeof(string).FullName);
        result = TextImportUtilities.SingleGroup(path, info);
        return true;
    }

    private bool TryParseTime(string input, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parsed = timeFormatter.Parse(input);
        if (parsed.Diagnostics.Count == 0)
        {
            time = parsed.Value;
            return true;
        }

        var frameTime = FrameTimeRegex().Match(input);
        if (!frameTime.Success)
        {
            return false;
        }

        var hour = int.Parse(frameTime.Groups["Hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(frameTime.Groups["Minute"].Value, CultureInfo.InvariantCulture);
        var second = int.Parse(frameTime.Groups["Second"].Value, CultureInfo.InvariantCulture);
        var frame = int.Parse(frameTime.Groups["Frame"].Value, CultureInfo.InvariantCulture);
        var fps = GuessFrameRate(frame);
        time = new TimeSpan(0, hour, minute, second)
            + TimeSpan.FromTicks((long)Math.Round(frame * TimeSpan.TicksPerSecond / fps));
        return true;
    }

    private static string[] NormalizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static char DetectSeparator(string headerLine)
    {
        if (headerLine.Contains('\t', StringComparison.Ordinal))
        {
            return '\t';
        }

        if (headerLine.Count(static character => character == ',') >= 1)
        {
            return ',';
        }

        return headerLine.Count(static character => character == ';') >= 1 ? ';' : '\0';
    }

    private static string NormalizeHeader(string header)
    {
        var builder = new StringBuilder(header.Length);
        foreach (var character in header.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static int FindColumn(IEnumerable<string> headers, params string[] candidates)
    {
        var normalizedCandidates = new HashSet<string>(candidates.Select(NormalizeHeader), StringComparer.Ordinal);
        var index = 0;
        foreach (var header in headers)
        {
            if (normalizedCandidates.Contains(header))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static string GetColumnValue(IReadOnlyList<string> columns, int index) =>
        index >= 0 && index < columns.Count ? columns[index].Trim() : string.Empty;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string[] SplitLine(string line, char separator)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == separator && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static decimal GuessFrameRate(int frame)
    {
        foreach (var frameRate in CommonFrameRates)
        {
            if (frame < Math.Ceiling(frameRate))
            {
                return frameRate;
            }
        }

        return CommonFrameRates[^1];
    }

    private static ChapterDiagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);

    [GeneratedRegex(@"^\s*(?<Hour>\d+)\s*[:;]\s*(?<Minute>\d+)\s*[:;]\s*(?<Second>\d+)\s*[:;]\s*(?<Frame>\d+)\s*$")]
    private static partial Regex FrameTimeRegex();
}
