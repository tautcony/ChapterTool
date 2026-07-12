using System.Text;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.WasmDemo.Services;

/// <summary>
/// Thin Core import/export adapter used by the Blazor WASM workspace.
/// </summary>
public sealed class ChapterDemoService
{
    private static readonly string[] BinaryExtensions = [".mpls", ".ifo"];

    private readonly ChapterTimeFormatter timeFormatter = new();
    private readonly ChapterExportService exportService;
    private readonly TextChapterImporter textImporter;
    private readonly WebVttChapterImporter webVttImporter = new();
    private readonly XmlChapterImporter xmlImporter;
    private readonly CueChapterImporter cueImporter = new();
    private readonly MplsChapterImporter mplsImporter = new();
    private readonly IfoChapterImporter ifoImporter = new();

    public ChapterDemoService()
    {
        exportService = new ChapterExportService(timeFormatter);
        textImporter = new TextChapterImporter(timeFormatter);
        xmlImporter = new XmlChapterImporter(timeFormatter);
    }

    public IChapterTimeFormatter TimeFormatter => timeFormatter;

    public IReadOnlyList<SaveFormatOption> SaveFormats { get; } =
        ChapterExportFormats.All
            .Select((format, index) => new SaveFormatOption(
                index,
                ChapterExportFormats.Code(format),
                ChapterExportFormats.DisplayName(format),
                ChapterExportFormats.Extension(format)))
            .ToArray();

    public IReadOnlyList<string> ChapterNameModes { get; } =
    [
        "As is",
        "Auto generate"
    ];

    public IReadOnlyList<string> XmlLanguages { get; } =
        XmlChapterLanguageCatalog.Languages
            .Select(static language => language.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool IsBinaryExtension(string? extension) =>
        !string.IsNullOrEmpty(extension) &&
        BinaryExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public async Task<ChapterImportResult> ImportAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Content is empty.", nameof(content));
        }

        var path = string.IsNullOrWhiteSpace(fileName) ? "input.txt" : fileName.Trim();
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            extension = DetectTextExtension(content);
            path = Path.ChangeExtension(path, extension);
        }

        await using var stream = new MemoryStream(content, writable: false);
        var request = new ChapterImportRequest(path, stream);
        return await ResolveImporter(extension).ImportAsync(request, cancellationToken);
    }

    public ChapterExportResult Export(ChapterSet chapterSet, ChapterExportOptions options) =>
        exportService.Export(chapterSet, options);

    public ChapterExportFormat FormatAt(int index) => ChapterExportFormats.AtIndex(index);

    public string FormatExtension(ChapterExportFormat format) => ChapterExportFormats.Extension(format);

    public string FormatDisplayName(ChapterExportFormat format) => ChapterExportFormats.DisplayName(format);

    private IChapterImporter ResolveImporter(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".vtt" => webVttImporter,
            ".xml" => xmlImporter,
            ".cue" => cueImporter,
            ".mpls" => mplsImporter,
            ".ifo" => ifoImporter,
            _ => textImporter
        };

    private static string DetectTextExtension(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content);
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            return ".vtt";
        }

        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<Chapters", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml";
        }

        if (trimmed.Contains("FILE \"", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("TRACK ", StringComparison.OrdinalIgnoreCase))
        {
            return ".cue";
        }

        return ".txt";
    }
}
