using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Exporting;

public sealed class ChapterExportService(
    IChapterTimeFormatter timeFormatter,
    IExpressionService expressionService)
{
    public ChapterExportResult Export(ChapterInfo info, ChapterExportOptions options)
    {
        var projection = options.ProjectOutput
            ? new ChapterOutputProjectionService(expressionService).Project(info, options)
            : new ChapterOutputProjectionResult(info, Array.Empty<ChapterDiagnostic>());
        info = projection.Info;
        var outputInfo = info with { Chapters = projection.OutputChapters };
        var result = options.Format switch
        {
            ChapterExportFormat.Txt => Text(outputInfo, options),
            ChapterExportFormat.Xml => Xml(outputInfo, options),
            ChapterExportFormat.Qpf => Lines(".qpf", outputInfo.Chapters.Select(static c => c.FramesInfo.TrimEnd('K', '*') + "I")),
            ChapterExportFormat.TimeCodes => Lines(".TimeCodes.txt", outputInfo.Chapters.Select(FormatTime)),
            ChapterExportFormat.TsMuxerMeta => TsMuxer(outputInfo, options),
            ChapterExportFormat.Cue => Cue(outputInfo, options),
            ChapterExportFormat.Json => Json(info, options),
            _ => Failure("UnsupportedExportFormat", "Unsupported export format.")
        };

        return result with
        {
            Diagnostics = result.Diagnostics.Concat(projection.Diagnostics).ToArray()
        };
    }

    private ChapterExportResult Text(ChapterInfo info, ChapterExportOptions options)
    {
        var builder = new StringBuilder();
        foreach (var chapter in info.Chapters.Where(NotSeparator))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"CHAPTER{chapter.Number:D2}={FormatTime(chapter)}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"CHAPTER{chapter.Number:D2}NAME={chapter.Name}");
        }

        return Success(builder.ToString(), ".txt");
    }

    private ChapterExportResult Xml(ChapterInfo info, ChapterExportOptions options)
    {
        var language = string.IsNullOrWhiteSpace(options.XmlLanguage) ? "und" : options.XmlLanguage;
        var atoms = info.Chapters.Where(NotSeparator).Select(chapter =>
            new XElement(
                "ChapterAtom",
                new XElement("ChapterDisplay", new XElement("ChapterString", chapter.Name), new XElement("ChapterLanguage", language)),
                new XElement("ChapterUID", chapter.Number),
                new XElement("ChapterTimeStart", FormatTime(chapter) + "000"),
                new XElement("ChapterFlagHidden", "0"),
                new XElement("ChapterFlagEnabled", "1")));
        var document = new XDocument(
            new XElement(
                "Chapters",
                new XElement(
                    "EditionEntry",
                    new XElement("EditionFlagHidden", "0"),
                    new XElement("EditionFlagDefault", "0"),
                    new XElement("EditionUID", "1"),
                    atoms)));
        return Success(document.ToString(SaveOptions.DisableFormatting), ".xml");
    }

    private ChapterExportResult TsMuxer(ChapterInfo info, ChapterExportOptions options)
    {
        var chapters = info.Chapters.Where(NotSeparator).Select(FormatTime).ToArray();
        if (chapters.Length == 0)
        {
            return Failure("NoChapters", "No chapters are available for tsMuxeR meta export.");
        }

        return Success($"--custom-{Environment.NewLine}chapters={string.Join(';', chapters)}", ".TsMuxeR_Meta.txt");
    }

    private ChapterExportResult Cue(ChapterInfo info, ChapterExportOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine("REM Generate By ChapterTool");
        builder.AppendLine(CultureInfo.InvariantCulture, $"TITLE \"{Escape(info.Title)}\"");
        builder.AppendLine(CultureInfo.InvariantCulture, $"FILE \"{Escape(options.SourceFileName ?? info.SourceName ?? string.Empty)}\" WAVE");
        var track = 0;
        foreach (var chapter in info.Chapters.Where(NotSeparator))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"  TRACK {++track:D2} AUDIO");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    TITLE \"{Escape(chapter.Name)}\"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    INDEX 01 {timeFormatter.FormatCue(chapter.Time)}");
        }

        return Success(builder.ToString(), ".cue");
    }

    private ChapterExportResult Json(ChapterInfo info, ChapterExportOptions options)
    {
        var entries = new List<JsonChapter>();
        var baseTime = TimeSpan.Zero;
        Chapter? previous = null;
        foreach (var chapter in info.Chapters)
        {
            if (chapter.IsSeparator)
            {
                if (previous is not null)
                {
                    baseTime = previous.Time;
                    entries.Add(new JsonChapter(previous.Name, 0));
                }

                continue;
            }

            entries.Add(new JsonChapter(chapter.Name, (chapter.Time - baseTime).TotalSeconds));
            previous = chapter;
        }

        var payload = new JsonPayload(info.SourceType == "MPLS" ? $"{info.SourceName}.m2ts" : null, entries);
        return Success(JsonSerializer.Serialize(payload, JsonOptions), ".json");
    }

    private ChapterExportResult Lines(string extension, IEnumerable<string> lines) =>
        Success(string.Join(Environment.NewLine, lines), extension);

    private static bool NotSeparator(Chapter chapter) => !chapter.IsSeparator;

    private static string Escape(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private string FormatTime(Chapter chapter) => timeFormatter.Format(chapter.Time);

    private static ChapterExportResult Success(string content, string extension) =>
        new(true, content, extension, Array.Empty<ChapterDiagnostic>());

    private static ChapterExportResult Failure(string code, string message) =>
        new(false, string.Empty, string.Empty, [new ChapterDiagnostic(DiagnosticSeverity.Error, code, message)]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record JsonPayload(string? SourceName, IReadOnlyList<JsonChapter> Chapter);

    private sealed record JsonChapter(string Name, double Time);
}
