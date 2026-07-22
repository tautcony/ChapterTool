using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions.Lua;

namespace ChapterTool.Node;

/// <summary>
/// Exposes the portable ChapterTool Core editing and transformation services to Node.js.
/// </summary>
public static partial class NodeApi
{
    private static readonly ChapterEditingService EditingService = new(ChapterService.TimeFormatter);
    private static readonly FrameRateService FrameRateService = new();
    private static readonly ChapterExpressionService ExpressionService = new();
    private static readonly ExpressionAuthoringService ExpressionAuthoringService = new();
    private static readonly LuaExpressionScriptService ExpressionEngine = new();
    private static readonly ChapterOutputProjectionService ProjectionService = new();
    private static readonly ChapterConversionService ConversionService = new(ChapterService.TimeFormatter);

    [JSExport]
    public static string Edit(string chapterSetJson, string operation, string optionsJson)
    {
        var chapterSet = DeserializeChapterSet(chapterSetJson);
        var options = JsonSerializer.Deserialize(optionsJson, NodeJsonContext.Default.NodeEditOptions)
            ?? new NodeEditOptions();
        var indexes = (options.Indexes ?? []).ToHashSet();

        var result = operation switch
        {
            "editTime" => EditingService.EditTime(chapterSet, options.Index, options.Text),
            "editFrame" => EditingService.EditFrame(chapterSet, options.Index, options.Text, (decimal)options.FramesPerSecond),
            "rename" => EditingService.Rename(chapterSet, options.Index, options.Text),
            "delete" => EditingService.Delete(chapterSet, indexes),
            "insertBefore" => EditingService.InsertBefore(chapterSet, options.Index),
            "applyOrderShift" => EditingService.ApplyOrderShift(chapterSet, options.Shift),
            "applyTemplate" => EditingService.ApplyTemplate(chapterSet, options.Text),
            "shiftFramesForward" => EditingService.ShiftFramesForward(chapterSet, options.Frames, (decimal)options.FramesPerSecond),
            _ => throw new ArgumentException($"Unsupported edit operation: {operation}", nameof(operation))
        };

        return SerializeEditResponse(result);
    }

    [JSExport]
    public static string CreateZones(string chapterSetJson, string indexesJson, double framesPerSecond)
    {
        var chapterSet = DeserializeChapterSet(chapterSetJson);
        var indexes = JsonSerializer.Deserialize(indexesJson, NodeJsonContext.Default.Int32Array) ?? [];
        var result = EditingService.CreateZones(chapterSet, indexes.ToHashSet(), (decimal)framesPerSecond);
        return JsonSerializer.Serialize(
            new NodeZonesResponse(result.Zones, result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeZonesResponse);
    }

    [JSExport]
    public static string Combine(string sourceJson)
    {
        var source = DeserializeImportSource(sourceJson);
        return SerializeEditResponse(ChapterSegmentService.Combine(source));
    }

    [JSExport]
    public static string Append(string existingJson, string appendedJson)
    {
        var existing = DeserializeImportSource(existingJson);
        var appended = DeserializeImportSource(appendedJson);
        return SerializeEditResponse(ChapterSegmentService.Append(existing, appended));
    }

    [JSExport]
    public static string GetFrameRates() =>
        JsonSerializer.Serialize(
            FrameRateService.Options.Select(ToFrameRateOption).ToArray(),
            NodeJsonContext.Default.NodeFrameRateOptionArray);

    [JSExport]
    public static string FindFrameRate(double framesPerSecond) =>
        JsonSerializer.Serialize(
            ToFrameRateOption(FrameRateService.FindByValue((decimal)framesPerSecond)),
            NodeJsonContext.Default.NodeFrameRateOption);

    [JSExport]
    public static string DetectFrameRate(string chapterSetJson, double tolerance)
    {
        var result = FrameRateService.DetectDetailed(DeserializeChapterSet(chapterSetJson), (decimal)tolerance);
        return JsonSerializer.Serialize(
            new NodeFrameDetectionResponse(
                ToFrameRateOption(result.Option),
                result.AccurateChapterCount,
                result.EvaluatedChapterCount,
                (double)result.CumulativeDeviation,
                result.Confidence.ToString()),
            NodeJsonContext.Default.NodeFrameDetectionResponse);
    }

    [JSExport]
    public static string UpdateFrames(string chapterSetJson, string optionCode, bool round, double tolerance)
    {
        var result = FrameRateService.UpdateFrames(
            DeserializeChapterSet(chapterSetJson),
            ResolveFrameRateOption(optionCode),
            round,
            (decimal)tolerance);
        return JsonSerializer.Serialize(
            new NodeFrameInfoResponse(
                ToNodeChapterSet(result.Info),
                result.Chapters.Select(ToNodeChapter).ToArray(),
                ToFrameRateOption(result.SelectedOption),
                (double)result.FramesPerSecond,
                result.Accuracy.Select(static accuracy => accuracy.ToString()).ToArray()),
            NodeJsonContext.Default.NodeFrameInfoResponse);
    }

    [JSExport]
    public static string ChangeFrameRate(string chapterSetJson, double sourceFps, double targetFps)
    {
        var result = ChapterFpsTransformService.ChangeFps(
            DeserializeChapterSet(chapterSetJson),
            (decimal)sourceFps,
            (decimal)targetFps);
        return JsonSerializer.Serialize(
            new NodeTransformResponse(
                result.Success,
                ToNodeChapterSet(result.Info),
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeTransformResponse);
    }

    [JSExport]
    public static string ApplyExpression(string chapterSetJson, bool enabled, string expression)
    {
        var result = ExpressionService.Apply(DeserializeChapterSet(chapterSetJson), enabled, expression);
        return JsonSerializer.Serialize(
            new NodeEditResponse(
                ToNodeChapterSet(result.Info),
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeEditResponse);
    }

    [JSExport]
    public static string Project(string chapterSetJson, string optionsJson)
    {
        var options = JsonSerializer.Deserialize(optionsJson, NodeJsonContext.Default.NodeExportOptions)
            ?? throw new ArgumentException("Export options JSON is invalid.", nameof(optionsJson));
        var result = ProjectionService.Project(DeserializeChapterSet(chapterSetJson), ToExportOptions(options));
        return JsonSerializer.Serialize(
            new NodeProjectionResponse(
                ToNodeChapterSet(result.Info),
                result.OutputChapters.Select(ToNodeChapter).ToArray(),
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeProjectionResponse);
    }

    [JSExport]
    public static string AnalyzeExpression(
        string expression,
        int caretIndex,
        double timeSeconds,
        double framesPerSecond)
    {
        var result = ExpressionAuthoringService.Analyze(
            expression,
            caretIndex,
            (decimal)timeSeconds,
            (decimal)framesPerSecond);
        return JsonSerializer.Serialize(
            new NodeExpressionAnalysisResponse(
                result.Spans.Select(static span => new NodeExpressionSpan(
                    span.Start,
                    span.Length,
                    span.Text,
                    span.Kind.ToString())).ToArray(),
                result.Completions.Select(static completion => new NodeExpressionCompletion(
                    completion.Text,
                    completion.Kind.ToString(),
                    completion.KindLabel,
                    completion.Description,
                    completion.ReplacementStart,
                    completion.ReplacementLength,
                    completion.InsertText)).ToArray(),
                result.Diagnostics.Select(static diagnostic => new NodeExpressionDiagnostic(
                    ToDiagnostic(diagnostic.Diagnostic),
                    new NodeExpressionSuggestion(diagnostic.Suggestion.Code, diagnostic.Suggestion.Message),
                    diagnostic.Start,
                    diagnostic.Length)).ToArray()),
            NodeJsonContext.Default.NodeExpressionAnalysisResponse);
    }

    [JSExport]
    public static string GetExpressionSymbols() =>
        JsonSerializer.Serialize(
            ExpressionAuthoringService.Symbols.Select(static symbol => new NodeExpressionSymbol(
                symbol.Text,
                symbol.Kind.ToString(),
                symbol.Description,
                symbol.Arity,
                symbol.InsertText)).ToArray(),
            NodeJsonContext.Default.NodeExpressionSymbolArray);

    [JSExport]
    public static string GetExpressionPresets() =>
        JsonSerializer.Serialize(
            ExpressionEngine.Presets.Select(static preset => new NodeExpressionPreset(
                preset.Id,
                preset.DisplayName,
                preset.Description,
                preset.ScriptText)).ToArray(),
            NodeJsonContext.Default.NodeExpressionPresetArray);

    [JSExport]
    public static string ParseTime(string text)
    {
        var result = ChapterService.TimeFormatter.Parse(text);
        return JsonSerializer.Serialize(
            new NodeTimeParseResponse(
                result.Value.TotalSeconds,
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeTimeParseResponse);
    }

    [JSExport]
    public static double ParseTimeOrZero(string text) =>
        ChapterService.TimeFormatter.ParseOrZero(text).TotalSeconds;

    [JSExport]
    public static string FormatTime(double seconds) =>
        ChapterService.TimeFormatter.Format(TimeSpan.FromSeconds(seconds));

    [JSExport]
    public static string FormatCueTime(double seconds) =>
        ChapterService.TimeFormatter.FormatCue(TimeSpan.FromSeconds(seconds));

    [JSExport]
    public static string ToCelltimes(string chapterSetJson, double framesPerSecond) =>
        SerializeConversion(ChapterConversionService.ToCelltimes(
            DeserializeChapterSet(chapterSetJson),
            (decimal)framesPerSecond));

    [JSExport]
    public static string ChapterTextToQpfile(string chapterText, double framesPerSecond, string? timecodeText) =>
        SerializeConversion(ConversionService.ChapterTextToQpfile(
            chapterText,
            (decimal)framesPerSecond,
            timecodeText));

    [JSExport]
    public static string GetXmlLanguages() =>
        JsonSerializer.Serialize(
            XmlChapterLanguageCatalog.Languages.Select(static language => new NodeXmlLanguage(
                language.Code,
                language.DisplayName)).ToArray(),
            NodeJsonContext.Default.NodeXmlLanguageArray);

    [JSExport]
    public static string GetOutputEncodings() =>
        JsonSerializer.Serialize(
            OutputTextEncodings.All.Select(static encoding => new NodeOutputEncoding(
                OutputTextEncodings.Id(encoding),
                OutputTextEncodings.DisplayName(encoding),
                OutputTextEncodings.XmlName(encoding))).ToArray(),
            NodeJsonContext.Default.NodeOutputEncodingArray);

    private static ChapterSet DeserializeChapterSet(string json) =>
        JsonSerializer.Deserialize(json, NodeJsonContext.Default.NodeChapterSet) is { } chapterSet
            ? ToChapterSet(chapterSet)
            : throw new ArgumentException("Chapter set JSON is invalid.", nameof(json));

    private static ChapterImportSource DeserializeImportSource(string json)
    {
        var source = JsonSerializer.Deserialize(json, NodeJsonContext.Default.NodeImportGroup)
            ?? throw new ArgumentException("Import source JSON is invalid.", nameof(json));
        return new ChapterImportSource(
            source.SourcePath,
            source.Entries.Select(static entry => new ChapterImportEntry(
                entry.Id,
                entry.DisplayName,
                ToChapterSet(entry.ChapterSet),
                entry.CanCombine,
                entry.ReferencedMediaFiles?.Select(static media => new ReferencedMediaFile(
                    media.DisplayName,
                    media.RelativePath,
                    media.AbsolutePath)).ToArray())).ToArray(),
            source.DefaultEntryIndex);
    }

    private static string SerializeEditResponse(ChapterEditResult result) =>
        JsonSerializer.Serialize(
            new NodeEditResponse(
                ToNodeChapterSet(result.ChapterSet),
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeEditResponse);

    private static string SerializeConversion(ChapterConversionResult result) =>
        JsonSerializer.Serialize(
            new NodeConversionResponse(
                result.Success,
                result.Content,
                result.Extension,
                result.Diagnostics.Select(ToDiagnostic).ToArray()),
            NodeJsonContext.Default.NodeConversionResponse);

    private static NodeFrameRateOption ToFrameRateOption(FrameRateOption option) =>
        new(option.Code, option.DisplayName, (double)option.Value, option.IsValid, option.LegacyMplsCode);

    private static FrameRateOption ResolveFrameRateOption(string code) =>
        FrameRateService.Options.FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unsupported frame rate option code: {code}", nameof(code));

    private static NodeChapter ToNodeChapter(Chapter chapter) =>
        new(
            chapter.DisplayNumber,
            chapter.StartTime.TotalSeconds,
            chapter.Name,
            chapter.FramesInfo,
            chapter.EndTime?.TotalSeconds,
            chapter.FrameAccuracy.ToString(),
            chapter.Kind.ToString());

    private sealed record NodeEditOptions(
        int Index = 0,
        int[]? Indexes = null,
        string Text = "",
        int Shift = 0,
        int Frames = 0,
        double FramesPerSecond = 0);

    private sealed record NodeEditResponse(NodeChapterSet ChapterSet, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeZonesResponse(string Zones, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeFrameRateOption(
        string Code,
        string DisplayName,
        double Value,
        bool IsValid,
        int LegacyMplsCode);

    private sealed record NodeFrameDetectionResponse(
        NodeFrameRateOption Option,
        int AccurateChapterCount,
        int EvaluatedChapterCount,
        double CumulativeDeviation,
        string Confidence);

    private sealed record NodeFrameInfoResponse(
        NodeChapterSet ChapterSet,
        IReadOnlyList<NodeChapter> Chapters,
        NodeFrameRateOption SelectedOption,
        double FramesPerSecond,
        IReadOnlyList<string> Accuracy);

    private sealed record NodeTransformResponse(
        bool Success,
        NodeChapterSet ChapterSet,
        IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeProjectionResponse(
        NodeChapterSet ChapterSet,
        IReadOnlyList<NodeChapter> OutputChapters,
        IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeExpressionSpan(int Start, int Length, string Text, string Kind);

    private sealed record NodeExpressionCompletion(
        string Text,
        string Kind,
        string KindLabel,
        string Description,
        int ReplacementStart,
        int ReplacementLength,
        string InsertText);

    private sealed record NodeExpressionSuggestion(string Code, string Message);

    private sealed record NodeExpressionDiagnostic(
        NodeDiagnostic Diagnostic,
        NodeExpressionSuggestion Suggestion,
        int Start,
        int Length);

    private sealed record NodeExpressionAnalysisResponse(
        IReadOnlyList<NodeExpressionSpan> Spans,
        IReadOnlyList<NodeExpressionCompletion> Completions,
        IReadOnlyList<NodeExpressionDiagnostic> Diagnostics);

    private sealed record NodeExpressionSymbol(
        string Text,
        string Kind,
        string Description,
        int? Arity,
        string InsertText);

    private sealed record NodeExpressionPreset(
        string Id,
        string DisplayName,
        string Description,
        string ScriptText);

    private sealed record NodeTimeParseResponse(double Seconds, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeConversionResponse(
        bool Success,
        string Content,
        string Extension,
        IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record NodeXmlLanguage(string Code, string DisplayName);

    private sealed record NodeOutputEncoding(string Id, string DisplayName, string XmlName);
}
