using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Node;

/// <summary>Hosts the exact ChapterTool Core operations for Node.js.</summary>
public static partial class NodeApi
{
    private static readonly ChapterContentService ChapterService = new();
    private static readonly ChapterEditingService EditingService = new(ChapterService.TimeFormatter);
    private static readonly FrameRateService FrameRateService = new();
    private static readonly ChapterExpressionService ExpressionService = new();
    private static readonly ChapterConversionService ConversionService = new(ChapterService.TimeFormatter);

    [JSExport]
    public static double GetMaxInputBytes() => PortableInputPolicy.MaxBytes;

    [JSExport]
    public static string GetFormats() => JsonSerializer.Serialize(
        ChapterExportFormats.All.Select((format, index) => new NodeFormat(index, ChapterExportFormats.Code(format),
            ChapterExportFormats.DisplayName(format), ChapterExportFormats.Extension(format), ChapterExportFormats.Description(format))).ToArray(),
        NodeJsonContext.Default.NodeFormatArray);

    [JSExport]
    public static string GetImportFormats() => JsonSerializer.Serialize(
        ChapterService.ImportFormats.Select(static format => new NodeImportFormat(ChapterImportFormats.Code(format),
            ChapterImportFormats.DisplayName(format))).ToArray(), NodeJsonContext.Default.NodeImportFormatArray);

    [JSExport]
    public static bool IsBinaryExtension(string? extension) => ChapterContentService.IsBinaryExtension(extension);

    [JSExport]
    public static string GetXmlLanguages() => JsonSerializer.Serialize(
        XmlChapterLanguageCatalog.Languages.Select(static item => new NodeXmlLanguage(item.Code, item.DisplayName)).ToArray(),
        NodeJsonContext.Default.NodeXmlLanguageArray);

    [JSExport]
    public static string GetOutputEncodings() => JsonSerializer.Serialize(
        OutputTextEncodings.All.Select(static item => new NodeOutputEncoding(OutputTextEncodings.Id(item),
            OutputTextEncodings.DisplayName(item), OutputTextEncodings.XmlName(item))).ToArray(),
        NodeJsonContext.Default.NodeOutputEncodingArray);

    private static OutputTextEncoding ParseTextEncoding(string? value) =>
        OutputTextEncodings.TryParse(value, out var encoding)
            ? encoding
            : throw new ArgumentException($"Unsupported text encoding: {value}", nameof(value));

    private static ChapterExportFormat ParseExportFormat(string code)
    {
        foreach (var format in ChapterExportFormats.All)
        {
            if (string.Equals(ChapterExportFormats.Code(format), code, StringComparison.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        throw new ArgumentException($"Unsupported export format code: {code}", nameof(code));
    }

    private static NodeExportResponse ToExportResponse(ChapterExportResult result) =>
        new(result.Success, result.Content, result.FileExtension, [.. result.Diagnostics.Select(ToDiagnostic)]);

    private static NodeDiagnostic ToDiagnostic(ChapterDiagnostic diagnostic) =>
        new(diagnostic.Severity.ToString(), diagnostic.DisplayCode, diagnostic.Message, diagnostic.Location, diagnostic.Details);

    private sealed record NodeExportResponse(bool Success, string Content, string FileExtension, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeDiagnostic(string Severity, string Code, string Message, string? Location, string? Details);

    private sealed record NodeZonesResponse(string Zones, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeConversionResponse(bool Success, string Content, string Extension, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeReferencedMediaFile(string DisplayName, string RelativePath, string? AbsolutePath = null);

    private sealed record NodeImportMediaTrack(string Kind, string Summary, string? Codec = null, string? Format = null,
        string? Language = null, string? Channels = null, string? SampleRate = null, string? AspectRatio = null);

    private sealed record NodeFormat(int Index, string Code, string DisplayName, string Extension, string Description);

    private sealed record NodeImportFormat(string Code, string DisplayName);

    private sealed record NodeXmlLanguage(string Code, string DisplayName);

    private sealed record NodeOutputEncoding(string Id, string DisplayName, string XmlName);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(NodeExportResponse))]
    [JsonSerializable(typeof(NodeZonesResponse))]
    [JsonSerializable(typeof(NodeConversionResponse))]
    [JsonSerializable(typeof(NodeDiagnostic[]))]
    [JsonSerializable(typeof(NodeFormat[]))]
    [JsonSerializable(typeof(NodeImportFormat[]))]
    [JsonSerializable(typeof(NodeXmlLanguage[]))]
    [JsonSerializable(typeof(NodeOutputEncoding[]))]
    [JsonSerializable(typeof(ExactRate))]
    [JsonSerializable(typeof(ExactChapterSet))]
    [JsonSerializable(typeof(ExactImportGroup))]
    [JsonSerializable(typeof(ExactImportResponse))]
    [JsonSerializable(typeof(ExactContentResponse))]
    [JsonSerializable(typeof(ExactEditOptions))]
    [JsonSerializable(typeof(ExactOutputOptions))]
    [JsonSerializable(typeof(ExactFrameOption[]))]
    [JsonSerializable(typeof(ExactFrameResponse))]
    [JsonSerializable(typeof(ExactDetectionResponse))]
    [JsonSerializable(typeof(int[]))]
    private sealed partial class NodeJsonContext : JsonSerializerContext;
}
