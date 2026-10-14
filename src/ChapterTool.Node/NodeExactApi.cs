using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ChapterTool.Core.Boundaries;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Node;

/// <summary>Exposes exact frame-rate operations to the Node.js host.</summary>
public static partial class NodeApi
{
    [JSExport]
    public static string ExactImport(string fileName, string contentBase64) => ExactGuard(() =>
    {
        if (!PortableInputPolicy.TryGetBase64DecodedLength(contentBase64, out var size)
            || size == 0 || !PortableInputPolicy.IsWithinLimit(size))
        {
            throw new ExactValidationException(ChapterDiagnosticCode.InvalidStructure, "Input Base64 is empty, invalid, or too large.");
        }

        var imported = ChapterService.ImportAsync(fileName, Convert.FromBase64String(contentBase64)).GetAwaiter().GetResult();
        return new ExactImportResponse(imported.Success, imported.IsPartial,
            [.. imported.Groups.Select(group => new ExactImportGroup(group.SourcePath,
                [.. group.Entries.Select(entry => new ExactImportEntry(entry.Id, entry.DisplayName,
                    ToExactSet(entry.ChapterSet, entry.ChapterSet.DurationIsKnown ? TickText(entry.ChapterSet.Duration.Ticks) : null,
                        [.. entry.Segments.Select(ToExactSegment)]), entry.CanCombine,
                    entry.ReferencedMediaFiles?.Select(static media => new NodeReferencedMediaFile(media.DisplayName, media.RelativePath, media.AbsolutePath)).ToArray(),
                    entry.MediaTracks?.Select(static track => new NodeImportMediaTrack(track.Kind, track.Summary, track.Codec, track.Format,
                        track.Language, track.Channels, track.SampleRate, track.AspectRatio)).ToArray()))], group.DefaultEntryIndex))],
            [.. imported.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new ExactImportResponse(false, false, [], diagnostics), NodeJsonContext.Default.ExactImportResponse);

    [JSExport]
    public static string ExactEdit(string chapterSetJson, string operation, string optionsJson) => ExactGuard(() =>
    {
        var input = ReadExactSet(chapterSetJson);
        var set = FromExactSet(input);
        var options = JsonSerializer.Deserialize(optionsJson, NodeJsonContext.Default.ExactEditOptions)
            ?? throw new ArgumentException("Edit options are missing.");
        var rate = options.ComputationRate is null ? set.FrameRate : RequireExactComputationRate(options.ComputationRate);
        var result = operation switch
        {
            "editTime" => EditingService.EditTime(set, options.Index, options.Text),
            "editFrame" => EditingService.EditFrame(set, options.Index, options.Text, rate),
            "rename" => EditingService.Rename(set, options.Index, options.Text),
            "delete" => EditingService.Delete(set, (options.Indexes ?? []).ToHashSet()),
            "insertBefore" => EditingService.InsertBefore(set, options.Index),
            "applyOrderShift" => EditingService.ApplyOrderShift(set, options.Shift),
            "applyTemplate" => EditingService.ApplyTemplate(set, options.Text),
            "shiftFramesForward" => EditingService.ShiftFramesForward(set, options.Frames, rate),
            "editStartTicks" => EditExactTicks(set, options.Index, options.Ticks),
            _ => throw new ArgumentException($"Unsupported edit operation: {operation}")
        };
        var output = ToExactSet(result.ChapterSet, input.DurationTicks, input.Segments);
        if (result.Diagnostics.Count == 0)
        {
            _ = FromExactSet(output);
        }

        return new ExactContentResponse(result.Diagnostics.Count == 0, result.Diagnostics.Count == 0 ? output : input,
            [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new ExactContentResponse(false, null, diagnostics), NodeJsonContext.Default.ExactContentResponse);

    [JSExport]
    public static string ExactCreateZones(string chapterSetJson, string indexesJson, string? computationRateJson) => ExactGuard(() =>
    {
        var set = FromExactSet(ReadExactSet(chapterSetJson));
        var indexes = JsonSerializer.Deserialize(indexesJson, NodeJsonContext.Default.Int32Array) ?? [];
        var result = EditingService.CreateZones(set, indexes.ToHashSet(), ReadComputationRate(computationRateJson) ?? set.FrameRate);
        return new NodeZonesResponse(result.Zones, [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new NodeZonesResponse(string.Empty, diagnostics), NodeJsonContext.Default.NodeZonesResponse);

    [JSExport]
    public static string ExactChangeFrameRate(string chapterSetJson, string sourceRateJson, string targetRateJson) => ExactGuard(() =>
    {
        var input = ReadExactSet(chapterSetJson);
        var set = FromExactSet(input);
        var sourceRate = ReadComputationRate(sourceRateJson) ?? set.FrameRate;
        var targetRate = ReadComputationRate(targetRateJson) ?? throw InvalidExactRate();
        var original = EditableChapterDocumentAdapter.FromChapterSet(set);
        var track = original.Tracks[0];
        var segments = input.Segments?.Select(segment => EditableChapterSegment.Create(track.Id, segment.Id, segment.Name,
            ParseExactTicks(segment.StartTicks), segment.DurationTicks is null ? ChapterDuration.Unknown : ChapterDuration.FromTicks(ParseExactTicks(segment.DurationTicks)),
            ParseExactRate(segment.FrameRate), set.ImportFormat, set.SourceName,
            sourceFrameRate: ParseExactRate(segment.SourceFrameRate))).ToArray();
        var document = new EditableChapterDocument(original.Id, original.Title, original.SourceName, original.ImportFormat,
            input.DurationTicks is null ? ChapterDuration.Unknown : ChapterDuration.FromTicks(ParseExactTicks(input.DurationTicks)),
            [new EditableChapterTrack(track.Id, track.Name, track.Chapters, segments, set.FrameRate)]);
        var converted = new ChapterContentCandidateBuilder(EditingService).ChangeFrameRate(document, sourceRate, targetRate);
        if (!converted.IsValid)
        {
            return new ExactContentResponse(false, input, [.. converted.Diagnostics.Select(ToDiagnostic)]);
        }

        var updated = converted.Candidate;
        var convertedSegments = input.Segments?.Select((segment, index) => segment with
        {
            FrameRate = ToExactRate(updated.Tracks[0].Segments[index].FrameRate),
            StartTicks = TickText(updated.Tracks[0].Segments[index].StartTicks),
            DurationTicks = updated.Tracks[0].Segments[index].Duration.IsKnown ? TickText(updated.Tracks[0].Segments[index].Duration.Ticks) : null
        }).ToArray();
        return new ExactContentResponse(true, ToExactSet(EditableChapterDocumentAdapter.ToChapterSet(updated),
            updated.Duration.IsKnown ? TickText(updated.Duration.Ticks) : null, convertedSegments), []);
    }, diagnostics => new ExactContentResponse(false, null, diagnostics), NodeJsonContext.Default.ExactContentResponse);

    [JSExport]
    public static string ExactApplyExpression(string chapterSetJson, bool enabled, string expression, string? computationRateJson) => ExactGuard(() =>
    {
        var input = ReadExactSet(chapterSetJson);
        var result = ExpressionService.Apply(FromExactSet(input), enabled, expression, ReadComputationRate(computationRateJson));
        return new ExactContentResponse(result.Diagnostics.Count == 0,
            ToExactSet(result.Info, input.DurationTicks, input.Segments), [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new ExactContentResponse(false, null, diagnostics), NodeJsonContext.Default.ExactContentResponse);

    [JSExport]
    public static string ExactSerialize(string chapterSetJson, string optionsJson) => ExactGuard(() =>
    {
        var input = ReadExactSet(chapterSetJson);
        var options = ReadExactOutputOptions(optionsJson);
        var serialized = ChapterService.Serialize(FromExactSet(input), ExactSerializationOptions(options));
        return ToExportResponse(serialized);
    }, diagnostics => new NodeExportResponse(false, string.Empty, string.Empty, diagnostics), NodeJsonContext.Default.NodeExportResponse);

    [JSExport]
    public static string ExactCombine(string groupJson) => ExactCombineCore(groupJson, null);

    [JSExport]
    public static string ExactAppend(string existingJson, string appendedJson) => ExactCombineCore(existingJson, appendedJson);

    [JSExport]
    public static string ExactGetFrameRates() => JsonSerializer.Serialize(
        FrameRateService.Options.Select(ToExactOption).ToArray(), NodeJsonContext.Default.ExactFrameOptionArray);

    [JSExport]
    public static string ExactDetectFrameRate(string chapterSetJson, double tolerance) => ExactGuard(() =>
    {
        var result = FrameRateService.DetectDetailed(FromExactSet(ReadExactSet(chapterSetJson)), (decimal)tolerance);
        return new ExactDetectionResponse(true, ToExactOption(result.Option), result.AccurateChapterCount,
            result.EvaluatedChapterCount, result.CumulativeDeviation.ToString(CultureInfo.InvariantCulture), result.Confidence.ToString(), []);
    }, diagnostics => new ExactDetectionResponse(false, null, 0, 0, "0", "Low", diagnostics), NodeJsonContext.Default.ExactDetectionResponse);

    [JSExport]
    public static string ExactUpdateFrames(string chapterSetJson, string? computationRateJson, int decimalPlaces, double tolerance) => ExactGuard(() =>
    {
        var input = ReadExactSet(chapterSetJson);
        var set = FromExactSet(input);
        var requested = ReadComputationRate(computationRateJson);
        var option = requested is { } rate ? FrameRateService.FindByRate(rate) : FrameRateService.Options[0];
        var result = FrameRateService.UpdateFrames(set, option, decimalPlaces, (decimal)tolerance);
        return new ExactFrameResponse(true, ToExactSet(result.Info, input.DurationTicks, input.Segments), ToExactOption(result.SelectedOption),
            [.. result.Accuracy.Select(static accuracy => accuracy.ToString())], []);
    }, diagnostics => new ExactFrameResponse(false, null, null, [], diagnostics), NodeJsonContext.Default.ExactFrameResponse);

    [JSExport]
    public static string ExactToCelltimes(string chapterSetJson, string? computationRateJson) => ExactGuard(() =>
    {
        var set = FromExactSet(ReadExactSet(chapterSetJson));
        var result = ChapterConversionService.ToCelltimes(set, ReadComputationRate(computationRateJson) ?? set.FrameRate);
        return new NodeConversionResponse(result.Success, result.Content, result.Extension, [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new NodeConversionResponse(false, string.Empty, ".txt", diagnostics), NodeJsonContext.Default.NodeConversionResponse);

    [JSExport]
    public static string ExactChapterTextToQpfile(string chapterText, string? computationRateJson, string? timecodeText) => ExactGuard(() =>
    {
        var result = ConversionService.ChapterTextToQpfile(chapterText, ReadComputationRate(computationRateJson) ?? FrameRateValue.Unknown, timecodeText);
        return new NodeConversionResponse(result.Success, result.Content, result.Extension, [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new NodeConversionResponse(false, string.Empty, ".qpf", diagnostics), NodeJsonContext.Default.NodeConversionResponse);

    private static string ExactCombineCore(string existingJson, string? appendedJson) => ExactGuard(() =>
    {
        var input = JsonSerializer.Deserialize(existingJson, NodeJsonContext.Default.ExactImportGroup)
            ?? throw new ArgumentException("Import group is missing.");
        var existing = FromExactGroup(input);
        var appended = appendedJson is null ? null : JsonSerializer.Deserialize(appendedJson, NodeJsonContext.Default.ExactImportGroup)
            ?? throw new ArgumentException("Appended group is missing.");
        var result = appended is null ? ChapterSegmentService.Combine(existing) : ChapterSegmentService.Append(existing, FromExactGroup(appended));
        var entries = input.Entries.Concat(appended?.Entries ?? []).ToArray();

        // null means unknown. A rejected combine still reports the requested entries.
        var hasDuration = entries.All(static entry => entry.ChapterSet.DurationTicks is not null);
        return new ExactContentResponse(result.Diagnostics.Count == 0, ToExactSet(result.ChapterSet,
            hasDuration ? TickText(result.ChapterSet.Duration.Ticks) : null), [.. result.Diagnostics.Select(ToDiagnostic)]);
    }, diagnostics => new ExactContentResponse(false, null, diagnostics), NodeJsonContext.Default.ExactContentResponse);

    private static ChapterImportSource FromExactGroup(ExactImportGroup group) => new(group.SourcePath,
        [.. group.Entries.Select(entry => new ChapterImportEntry(entry.Id, entry.DisplayName, FromExactSet(entry.ChapterSet), entry.CanCombine)
        {
            Segments = FromExactSegments(entry.ChapterSet.Segments)
        })], group.DefaultEntryIndex);

    private static ChapterEditResult EditExactTicks(ChapterSet set, int index, string? text)
    {
        if (index < 0 || index >= set.Chapters.Count)
        {
            throw new ExactValidationException(ChapterDiagnosticCode.InvalidChapterIndex, "Chapter index is invalid.");
        }

        var ticks = ParseExactTicks(text);
        var chapters = set.Chapters.ToArray();
        chapters[index] = chapters[index] with { StartTime = TimeSpan.FromTicks(ticks) };
        return new ChapterEditResult(set with { Chapters = chapters }, []);
    }

    private static ExactChapterSet ReadExactSet(string json) =>
        JsonSerializer.Deserialize(json, NodeJsonContext.Default.ExactChapterSet) ?? throw new ArgumentException("Chapter set is missing.");

    private static ExactOutputOptions ReadExactOutputOptions(string json) =>
        JsonSerializer.Deserialize(json, NodeJsonContext.Default.ExactOutputOptions) ?? throw new ArgumentException("Output options are missing.");

    private static FrameRateValue RequireExactComputationRate(ExactRate rate)
    {
        var parsed = ParseExactRate(rate);
        return parsed.IsKnown ? parsed : throw InvalidExactRate();
    }

    private static FrameRateValue? ReadComputationRate(string? json) => json is null ? null :
        RequireExactComputationRate(JsonSerializer.Deserialize(json, NodeJsonContext.Default.ExactRate) ?? throw InvalidExactRate());

    private static ChapterSerializationOptions ExactSerializationOptions(ExactOutputOptions options) =>
        new(ParseExportFormat(options.Format), options.XmlLanguage, options.SourceFileName, ParseTextEncoding(options.TextEncoding))
        {
            ComputationRate = options.ComputationRate is null ? null : RequireExactComputationRate(options.ComputationRate)
        };

    private static ExactFrameOption ToExactOption(FrameRateOption option) => new(option.Code, option.DisplayName,
        ToExactRate(option.Rate), option.IsValid, option.MplsFrameRateCode);

    private static string TickText(long ticks) => ticks.ToString(CultureInfo.InvariantCulture);

    private static string ExactGuard<T>(Func<T> operation, Func<IReadOnlyList<NodeDiagnostic>, T> failure, JsonTypeInfo<T> type)
    {
        try
        {
            return JsonSerializer.Serialize(operation(), type);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException or JsonException)
        {
            var code = exception is ExactValidationException validation ? validation.Code : ChapterDiagnosticCode.InvalidStructure;
            return JsonSerializer.Serialize(failure([ToDiagnostic(new ChapterDiagnostic(DiagnosticSeverity.Error, code, exception.Message))]), type);
        }
    }
}
