using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

/// <summary>Builds the encoded output document delivered to a host sink.</summary>
public static class ChapterOutputDocumentFactory
{
    public static ChapterOutputDocument Create(
        ChapterSet chapterSet,
        ChapterExportOptions options,
        ChapterExportResult exportResult,
        string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(chapterSet);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exportResult);

        var baseName = ChapterSavePath.BuildBaseFileName(chapterSet, sourcePath);
        var extension = ChapterExportFormats.Extension(options.Format);
        var fileName = baseName + extension;
        var bytes = exportResult.Success
            ? Encode(exportResult.Content, options)
            : [];

        return new ChapterOutputDocument(
            fileName,
            MediaType(options.Format),
            bytes,
            exportResult.Diagnostics);
    }

    private static string MediaType(ChapterExportFormat format) => format switch
    {
        ChapterExportFormat.Xml => "application/xml",
        ChapterExportFormat.Json => "application/json",
        ChapterExportFormat.WebVtt => "text/vtt",
        ChapterExportFormat.Cue => "application/x-cue",
        _ => "text/plain"
    };

    private static byte[] Encode(string content, ChapterExportOptions options)
    {
        var encoding = OutputTextEncodings.Create(options.TextEncoding, options.EmitBom);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(content)];
    }
}
