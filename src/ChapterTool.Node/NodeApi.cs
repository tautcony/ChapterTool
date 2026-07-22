using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Node;

/// <summary>
/// Exposes the pure managed ChapterTool Core operations to the Node.js host.
/// </summary>
public static partial class NodeApi
{
    private static readonly ChapterContentService ChapterService = new();

    [JSExport]
    public static string Import(string fileName, string contentBase64)
    {
        ArgumentNullException.ThrowIfNull(contentBase64);
        var content = Convert.FromBase64String(contentBase64);
        var result = ChapterService.ImportAsync(fileName, content).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(ToImportResponse(result), NodeJsonContext.Default.NodeImportResponse);
    }

    [JSExport]
    public static string Export(string chapterSetJson, string optionsJson)
    {
        ArgumentNullException.ThrowIfNull(chapterSetJson);
        ArgumentNullException.ThrowIfNull(optionsJson);

        var chapterSet = JsonSerializer.Deserialize(chapterSetJson, NodeJsonContext.Default.NodeChapterSet)
            ?? throw new ArgumentException("Chapter set JSON is invalid.", nameof(chapterSetJson));
        var options = JsonSerializer.Deserialize(optionsJson, NodeJsonContext.Default.NodeExportOptions)
            ?? throw new ArgumentException("Export options JSON is invalid.", nameof(optionsJson));

        var result = ChapterService.Export(ToChapterSet(chapterSet), ToExportOptions(options));
        return JsonSerializer.Serialize(ToExportResponse(result), NodeJsonContext.Default.NodeExportResponse);
    }

    [JSExport]
    public static string GetFormats() =>
        JsonSerializer.Serialize(
            ChapterService.SaveFormats.Select(static option => new NodeFormat(
                option.Index,
                option.Code,
                option.DisplayName,
                option.Extension,
                ChapterExportFormats.Description(ParseExportFormat(option.Code)))).ToArray(),
            NodeJsonContext.Default.NodeFormatArray);

    [JSExport]
    public static string GetImportFormats() =>
        JsonSerializer.Serialize(
            ChapterService.ImportFormats
                .Select(static format => new NodeImportFormat(
                    ChapterImportFormats.Code(format),
                    ChapterImportFormats.DisplayName(format)))
                .ToArray(),
            NodeJsonContext.Default.NodeImportFormatArray);

    [JSExport]
    public static bool IsBinaryExtension(string? extension) =>
        ChapterContentService.IsBinaryExtension(extension);

    private static NodeImportResponse ToImportResponse(ChapterImportResult result) =>
        new(
            result.Success,
            result.IsPartial,
            result.Groups.Select(static group => new NodeImportGroup(
                group.SourcePath,
                group.Entries.Select(static entry => new NodeImportEntry(
                    entry.Id,
                    entry.DisplayName,
                    ToNodeChapterSet(entry.ChapterSet),
                    entry.CanCombine,
                    entry.ReferencedMediaFiles?.Select(static media => new NodeReferencedMediaFile(
                        media.DisplayName,
                        media.RelativePath,
                        media.AbsolutePath)).ToArray())).ToArray(),
                group.DefaultEntryIndex)).ToArray(),
            result.Diagnostics.Select(ToDiagnostic).ToArray());

    private static NodeChapterSet ToNodeChapterSet(ChapterSet chapterSet) =>
        new(
            chapterSet.Title,
            chapterSet.SourceName,
            chapterSet.ImportFormat.ToString(),
            chapterSet.FramesPerSecond,
            chapterSet.Duration.TotalSeconds,
            chapterSet.Chapters.Select(static chapter => new NodeChapter(
                chapter.DisplayNumber,
                chapter.StartTime.TotalSeconds,
                chapter.Name,
                chapter.FramesInfo,
                chapter.EndTime?.TotalSeconds,
                chapter.FrameAccuracy.ToString(),
                chapter.Kind.ToString())).ToArray());

    private static NodeExportResponse ToExportResponse(ChapterExportResult result) =>
        new(
            result.Success,
            result.Content,
            result.FileExtension,
            result.Diagnostics.Select(ToDiagnostic).ToArray());

    private static NodeDiagnostic ToDiagnostic(ChapterDiagnostic diagnostic) =>
        new(
            diagnostic.Severity.ToString(),
            diagnostic.DisplayCode,
            diagnostic.Message,
            diagnostic.Location,
            diagnostic.Details);

    private static ChapterSet ToChapterSet(NodeChapterSet chapterSet) =>
        new(
            chapterSet.Title,
            chapterSet.SourceName,
            Enum.Parse<ChapterImportFormat>(chapterSet.ImportFormat, ignoreCase: true),
            chapterSet.FramesPerSecond,
            TimeSpan.FromSeconds(chapterSet.DurationSeconds),
            chapterSet.Chapters.Select(static chapter => new Chapter(
                chapter.DisplayNumber,
                TimeSpan.FromSeconds(chapter.StartTimeSeconds),
                chapter.Name,
                chapter.FramesInfo,
                chapter.EndTimeSeconds is null ? null : TimeSpan.FromSeconds(chapter.EndTimeSeconds.Value),
                Enum.Parse<FrameAccuracy>(chapter.FrameAccuracy, ignoreCase: true),
                Enum.Parse<ChapterKind>(chapter.Kind, ignoreCase: true))).ToArray());

    private static ChapterExportOptions ToExportOptions(NodeExportOptions options) =>
        new(
            ParseExportFormat(options.Format),
            options.XmlLanguage,
            options.SourceFileName,
            options.AutoGenerateNames,
            options.UseTemplateNames,
            options.ChapterNameTemplateText,
            options.OrderShift,
            options.ApplyExpression,
            options.Expression,
            options.ExpressionPresetId,
            options.ExpressionSourceName,
            Enum.Parse<OutputTextEncoding>(options.TextEncoding, ignoreCase: true),
            options.EmitBom,
            options.ProjectOutput);

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

    private sealed record NodeImportResponse(
        bool Success,
        bool IsPartial,
        IReadOnlyList<NodeImportGroup> Groups,
        IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeImportGroup(
        string SourcePath,
        IReadOnlyList<NodeImportEntry> Entries,
        int DefaultEntryIndex = 0);

    private sealed record NodeImportEntry(
        string Id,
        string DisplayName,
        NodeChapterSet ChapterSet,
        bool CanCombine = false,
        IReadOnlyList<NodeReferencedMediaFile>? ReferencedMediaFiles = null);

    private sealed record NodeReferencedMediaFile(
        string DisplayName,
        string RelativePath,
        string? AbsolutePath = null);

    private sealed record NodeExportResponse(
        bool Success,
        string Content,
        string FileExtension,
        IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeDiagnostic(
        string Severity,
        string Code,
        string Message,
        string? Location,
        string? Details);

    private sealed record NodeFormat(int Index, string Code, string DisplayName, string Extension, string Description);

    private sealed record NodeImportFormat(string Code, string DisplayName);

    private sealed record NodeChapterSet(
        string Title,
        string? SourceName,
        string ImportFormat,
        double FramesPerSecond,
        double DurationSeconds,
        IReadOnlyList<NodeChapter> Chapters);

    private sealed record NodeChapter(
        int DisplayNumber,
        double StartTimeSeconds,
        string Name,
        string FramesInfo,
        double? EndTimeSeconds,
        string FrameAccuracy,
        string Kind);

    private sealed record NodeExportOptions(
        string Format,
        string? XmlLanguage = null,
        string? SourceFileName = null,
        bool AutoGenerateNames = false,
        bool UseTemplateNames = false,
        string ChapterNameTemplateText = "",
        int OrderShift = 0,
        bool ApplyExpression = false,
        string Expression = "t",
        string ExpressionPresetId = "",
        string ExpressionSourceName = "",
        string TextEncoding = "Utf8",
        bool EmitBom = true,
        bool ProjectOutput = true);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(NodeImportResponse))]
    [JsonSerializable(typeof(NodeChapterSet))]
    [JsonSerializable(typeof(NodeExportOptions))]
    [JsonSerializable(typeof(NodeExportResponse))]
    [JsonSerializable(typeof(NodeFormat[]))]
    [JsonSerializable(typeof(NodeImportFormat[]))]
    [JsonSerializable(typeof(NodeImportGroup))]
    [JsonSerializable(typeof(NodeEditOptions))]
    [JsonSerializable(typeof(NodeEditResponse))]
    [JsonSerializable(typeof(NodeZonesResponse))]
    [JsonSerializable(typeof(NodeFrameRateOption[]))]
    [JsonSerializable(typeof(NodeFrameRateOption))]
    [JsonSerializable(typeof(NodeFrameDetectionResponse))]
    [JsonSerializable(typeof(NodeFrameInfoResponse))]
    [JsonSerializable(typeof(NodeTransformResponse))]
    [JsonSerializable(typeof(NodeProjectionResponse))]
    [JsonSerializable(typeof(NodeExpressionAnalysisResponse))]
    [JsonSerializable(typeof(NodeExpressionSymbol[]))]
    [JsonSerializable(typeof(NodeExpressionPreset[]))]
    [JsonSerializable(typeof(NodeTimeParseResponse))]
    [JsonSerializable(typeof(NodeConversionResponse))]
    [JsonSerializable(typeof(NodeXmlLanguage[]))]
    [JsonSerializable(typeof(NodeOutputEncoding[]))]
    [JsonSerializable(typeof(int[]))]
    private sealed partial class NodeJsonContext : JsonSerializerContext;
}
