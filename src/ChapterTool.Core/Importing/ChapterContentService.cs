using System.Text;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Importing;

/// <summary>
/// Imports chapter data from bytes and exports a chapter set to text.
/// </summary>
public class ChapterContentService
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
    private readonly XplChapterImporter xplImporter = new();

    /// <summary>
    /// Initializes the byte-based import and export service.
    /// </summary>
    public ChapterContentService()
    {
        exportService = new ChapterExportService(timeFormatter);
        textImporter = new TextChapterImporter(timeFormatter);
        xmlImporter = new XmlChapterImporter(timeFormatter);
    }

    /// <summary>
    /// Gets the chapter time formatter used by text import and export.
    /// </summary>
    public IChapterTimeFormatter TimeFormatter => timeFormatter;

    /// <summary>
    /// Gets the import formats that this byte-based service can read without platform integrations.
    /// </summary>
    public IReadOnlyList<ChapterImportFormat> ImportFormats { get; } =
    [
        ChapterImportFormat.Ogm,
        ChapterImportFormat.MatroskaXml,
        ChapterImportFormat.WebVtt,
        ChapterImportFormat.Cue,
        ChapterImportFormat.PremiereMarkers,
        ChapterImportFormat.Mpls,
        ChapterImportFormat.DvdIfo,
        ChapterImportFormat.HdDvdXpl
    ];

    /// <summary>
    /// Gets the supported export formats.
    /// </summary>
    public IReadOnlyList<SaveFormatOption> SaveFormats { get; } =
        ChapterExportFormats.All
            .Select((format, index) => new SaveFormatOption(
                index,
                ChapterExportFormats.Code(format),
                ChapterExportFormats.DisplayName(format),
                ChapterExportFormats.Extension(format)))
            .ToArray();

    /// <summary>
    /// Gets the supported chapter name modes.
    /// </summary>
    public IReadOnlyList<string> ChapterNameModes { get; } =
    [
        "As is",
        "Auto generate",
        "Template"
    ];

    /// <summary>
    /// Gets the XML language codes supported by export.
    /// </summary>
    public IReadOnlyList<string> XmlLanguages { get; } =
        XmlChapterLanguageCatalog.Languages
            .Select(static language => language.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Determines whether the extension requires binary input handling.
    /// </summary>
    /// <param name="extension">The file extension to test.</param>
    /// <returns><see langword="true" /> when the extension is binary.</returns>
    public static bool IsBinaryExtension(string? extension) =>
        !string.IsNullOrEmpty(extension) &&
        BinaryExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Imports chapter data from a file name and byte content.
    /// </summary>
    /// <param name="fileName">The source file name used to select an importer.</param>
    /// <param name="content">The source bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import result.</returns>
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

    /// <summary>
    /// Exports a chapter set with the specified options.
    /// </summary>
    /// <param name="chapterSet">The chapter set to export.</param>
    /// <param name="options">The export options.</param>
    /// <returns>The export result.</returns>
    public ChapterExportResult Export(ChapterSet chapterSet, ChapterExportOptions options) =>
        exportService.Export(chapterSet, options);

    /// <summary>
    /// Gets an export format by its selector index.
    /// </summary>
    /// <param name="index">The format index.</param>
    /// <returns>The export format.</returns>
    public ChapterExportFormat FormatAt(int index) => ChapterExportFormats.AtIndex(index);

    /// <summary>
    /// Gets the extension for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The file extension.</returns>
    public string FormatExtension(ChapterExportFormat format) => ChapterExportFormats.Extension(format);

    /// <summary>
    /// Gets the display name for an export format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>The display name.</returns>
    public string FormatDisplayName(ChapterExportFormat format) => ChapterExportFormats.DisplayName(format);

    private IChapterImporter ResolveImporter(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".vtt" => webVttImporter,
            ".xml" => xmlImporter,
            ".cue" => cueImporter,
            ".mpls" => mplsImporter,
            ".ifo" => ifoImporter,
            ".xpl" => xplImporter,
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
