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
            ChapterExportFormat.WebVtt => WebVtt(outputInfo, options),
            ChapterExportFormat.Celltimes => new ChapterConversionService(timeFormatter).ToCelltimes(outputInfo, (decimal)outputInfo.FramesPerSecond) is { } conversion
                ? new ChapterExportResult(conversion.Success, conversion.Content, conversion.Extension, conversion.Diagnostics)
                : Failure("CelltimesExportFailed", "Celltimes export failed."),
            ChapterExportFormat.Chapter2Qpfile => Chapter2Qpfile(outputInfo, options),
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
        var language = XmlChapterLanguageCatalog.NormalizeOrDefault(options.XmlLanguage);
        var uidSeed = HashCode.Combine(info.Title, info.SourceName, info.SourceType, info.Chapters.Count);
        var random = new Random(uidSeed);
        var atoms = info.Chapters.Where(NotSeparator).Select(chapter =>
            new XElement(
                "ChapterAtom",
                new XElement("ChapterDisplay", new XElement("ChapterString", chapter.Name), new XElement("ChapterLanguage", language)),
                new XElement("ChapterUID", NextUid(random)),
                new XElement("ChapterTimeStart", FormatTime(chapter) + "000"),
                new XElement("ChapterFlagHidden", "0"),
                new XElement("ChapterFlagEnabled", "1")));
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XComment("<!DOCTYPE Tags SYSTEM \"matroskatags.dtd\">"),
            new XElement(
                "Chapters",
                new XElement(
                    "EditionEntry",
                    new XElement("EditionFlagHidden", "0"),
                    new XElement("EditionFlagDefault", "0"),
                    new XElement("EditionUID", NextUid(random)),
                    atoms)));
        return Success(document.Declaration + Environment.NewLine + document.ToString(SaveOptions.None), ".xml");
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

    private ChapterExportResult Chapter2Qpfile(ChapterInfo info, ChapterExportOptions options)
    {
        var text = Text(info, options);
        var conversion = new ChapterConversionService(timeFormatter)
            .ChapterTextToQpfile(text.Content, (decimal)info.FramesPerSecond);
        return new ChapterExportResult(conversion.Success, conversion.Content, conversion.Extension, conversion.Diagnostics);
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

    private ChapterExportResult WebVtt(ChapterInfo info, ChapterExportOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");
        builder.AppendLine();

        var chapters = info.Chapters.Where(NotSeparator).ToArray();
        for (var i = 0; i < chapters.Length; i++)
        {
            var chapter = chapters[i];
            var endTime = i + 1 < chapters.Length ? chapters[i + 1].Time : info.Duration;

            builder.AppendLine(CultureInfo.InvariantCulture, $"{FormatWebVttTime(chapter.Time)} --> {FormatWebVttTime(endTime)}");
            builder.AppendLine(chapter.Name);
            if (i < chapters.Length - 1)
            {
                builder.AppendLine();
            }
        }

        return Success(builder.ToString(), ".vtt");
    }

    private static string FormatWebVttTime(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        var seconds = time.Seconds;
        var milliseconds = time.Milliseconds;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
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

    private static int NextUid(Random random) => random.Next(1, int.MaxValue);

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
