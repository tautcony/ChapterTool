using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.Node;

/// <summary>Provides exact-rate serialization models used by the Node.js API.</summary>
public static partial class NodeApi
{
    private sealed class ExactRate
    {
        [JsonConstructor]
        public ExactRate()
        {
        }

        public ExactRate(string kind, string? numerator = null, string? denominator = null, string? value = null)
        {
            Kind = kind;
            Numerator = numerator;
            Denominator = denominator;
            Value = value;
        }

        public string? Kind { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Numerator { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Denominator { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Value { get; set; }

        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? Additional { get; set; }
    }

    private sealed record ExactChapter(int DisplayNumber, string StartTicks, string Name, string FramesInfo = "",
        string? EndTicks = null, string FrameAccuracy = "Neutral", string Kind = "Marker");

    private sealed record ExactSegment(string Id, string Name, string StartTicks, string? DurationTicks,
        ExactRate FrameRate, ExactRate SourceFrameRate, IReadOnlyList<NodeReferencedMediaFile>? ReferencedMediaFiles = null);

    private sealed record ExactChapterSet(string Title, string? SourceName, string ImportFormat, ExactRate FrameRate,
        string? DurationTicks, IReadOnlyList<ExactChapter> Chapters, IReadOnlyList<ExactSegment>? Segments = null);

    private sealed record ExactImportEntry(string Id, string DisplayName, ExactChapterSet ChapterSet, bool CanCombine = false,
        IReadOnlyList<NodeReferencedMediaFile>? ReferencedMediaFiles = null, IReadOnlyList<NodeImportMediaTrack>? MediaTracks = null);

    private sealed record ExactImportGroup(string SourcePath, IReadOnlyList<ExactImportEntry> Entries, int DefaultEntryIndex = 0);

    private sealed record ExactImportResponse(bool Success, bool IsPartial, IReadOnlyList<ExactImportGroup> Groups, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record ExactContentResponse(bool Success, ExactChapterSet? ChapterSet, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record ExactFrameOption(string Code, string DisplayName, ExactRate Rate, bool IsValid, int MplsFrameRateCode);

    private sealed record ExactFrameResponse(bool Success, ExactChapterSet? ChapterSet, ExactFrameOption? SelectedOption,
        IReadOnlyList<string> Accuracy, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record ExactDetectionResponse(bool Success, ExactFrameOption? Option, int AccurateChapterCount,
        int EvaluatedChapterCount, string CumulativeDeviation, string Confidence, IReadOnlyList<NodeDiagnostic> Diagnostics);

    private sealed record ExactEditOptions(int Index = 0, int[]? Indexes = null, string Text = "", int Shift = 0,
        int Frames = 0, ExactRate? ComputationRate = null, string? Ticks = null);

    private sealed record ExactOutputOptions(string Format, string? XmlLanguage = null, string? SourceFileName = null,
        string TextEncoding = "Utf8", ExactRate? ComputationRate = null, bool EmitBom = false);

    private sealed class ExactValidationException(ChapterDiagnosticCode code, string message) : ArgumentException(message)
    {
        public ChapterDiagnosticCode Code { get; } = code;
    }

    private static FrameRateValue ParseExactRate(ExactRate? input)
    {
        if (input is null || input.Additional is { Count: > 0 })
        {
            throw InvalidExactRate();
        }

        return input.Kind switch
        {
            "unknown" => ParseUnknownRate(input),
            "rational" => ParseRationalRate(input),
            "approximate" => ParseApproximateRate(input),
            _ => throw InvalidExactRate()
        };
    }

    private static FrameRateValue ParseUnknownRate(ExactRate input) =>
        input.Numerator is null && input.Denominator is null && input.Value is null
            ? FrameRateValue.Unknown
            : throw InvalidExactRate();

    private static FrameRateValue ParseApproximateRate(ExactRate input)
    {
        if (input.Value is not null && input.Numerator is null && input.Denominator is null
            && FrameRateValue.TryFromInvariantDecimal(input.Value, out var approximate, out _))
        {
            return approximate;
        }

        throw InvalidExactRate();
    }

    private static FrameRateValue ParseRationalRate(ExactRate input)
    {
        if (input.Numerator is not null && input.Denominator is not null && input.Value is null
            && long.TryParse(input.Numerator, NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            && long.TryParse(input.Denominator, NumberStyles.None, CultureInfo.InvariantCulture, out var denominator)
            && numerator > 0 && denominator > 0)
        {
            return FrameRateValue.Rational(numerator, denominator);
        }

        throw InvalidExactRate();
    }

    private static ExactValidationException InvalidExactRate() =>
        new(ChapterDiagnosticCode.InvalidFrameRate, "The exact frame rate payload is invalid.");

    private static long ParseExactTicks(string? text)
    {
        if (text is not null && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ticks)
            && text.Trim() == text)
        {
            return ticks;
        }

        throw new ExactValidationException(ChapterDiagnosticCode.InvalidStructure, "Time must be a signed Int64 tick string.");
    }

    private static ExactRate ToExactRate(FrameRateValue rate) => rate.Kind switch
    {
        FrameRateKind.Rational => new ExactRate("rational", rate.RationalRate!.Value.Numerator.ToString(CultureInfo.InvariantCulture),
            rate.RationalRate.Value.Denominator.ToString(CultureInfo.InvariantCulture)),
        FrameRateKind.Approximate => new ExactRate("approximate", value: rate.ToString()),
        _ => new ExactRate("unknown")
    };

    private static ExactChapter ToExactChapter(Chapter chapter) => new(chapter.DisplayNumber,
        chapter.StartTime.Ticks.ToString(CultureInfo.InvariantCulture), chapter.Name, chapter.FramesInfo,
        chapter.EndTime?.Ticks.ToString(CultureInfo.InvariantCulture), chapter.FrameAccuracy.ToString(), chapter.Kind.ToString());

    private static ExactChapterSet ToExactSet(ChapterSet set, string? durationTicks, IReadOnlyList<ExactSegment>? segments = null) =>
        new(set.Title, set.SourceName, set.ImportFormat.ToString(), ToExactRate(set.FrameRate), durationTicks,
            [.. set.Chapters.Select(ToExactChapter)], segments);

    private static ChapterSet FromExactSet(ExactChapterSet set)
    {
        var rate = ParseExactRate(set.FrameRate);
        var duration = set.DurationTicks is null ? 0 : ParseExactTicks(set.DurationTicks);
        ValidateChapterSetPayload(set, duration);
        var chapters = set.Chapters!.Select(FromExactChapter).ToArray();
        ValidateChapterBounds(chapters);
        ValidateSegmentRates(set.Segments, rate);
        return new ChapterSet(set.Title, set.SourceName, Enum.Parse<ChapterImportFormat>(set.ImportFormat, ignoreCase: true),
            rate, TimeSpan.FromTicks(duration), chapters);
    }

    private static void ValidateChapterSetPayload(ExactChapterSet set, long duration)
    {
        if (duration < 0 || set.Chapters is null)
        {
            throw new ExactValidationException(ChapterDiagnosticCode.InvalidStructure, "Chapter duration or chapters are invalid.");
        }
    }

    private static Chapter FromExactChapter(ExactChapter chapter) => new(chapter.DisplayNumber,
        TimeSpan.FromTicks(ParseExactTicks(chapter.StartTicks)), chapter.Name, chapter.FramesInfo,
        chapter.EndTicks is null ? null : TimeSpan.FromTicks(ParseExactTicks(chapter.EndTicks)),
        Enum.Parse<FrameAccuracy>(chapter.FrameAccuracy, ignoreCase: true), Enum.Parse<ChapterKind>(chapter.Kind, ignoreCase: true));

    private static void ValidateChapterBounds(IEnumerable<Chapter> chapters)
    {
        if (chapters.Any(static chapter => !chapter.IsSeparator && (chapter.StartTime.Ticks < 0
            || (chapter.EndTime is { } end && end < chapter.StartTime))))
        {
            throw new ExactValidationException(ChapterDiagnosticCode.InvalidStructure, "Chapter bounds are invalid.");
        }
    }

    private static void ValidateSegmentRates(IReadOnlyList<ExactSegment>? segments, FrameRateValue rate)
    {
        if (segments is { Count: > 0 })
        {
            var contributors = segments.Select(ParseExactSegmentRate).ToArray();
            if (rate != FrameRateValue.ResolveUniform(contributors))
            {
                throw new ExactValidationException(ChapterDiagnosticCode.InvalidFrameRate, "The scalar rate conflicts with its segments.");
            }
        }
    }

    private static FrameRateValue ParseExactSegmentRate(ExactSegment segment)
    {
        _ = ParseExactRate(segment.SourceFrameRate);
        var start = ParseExactTicks(segment.StartTicks);
        var length = segment.DurationTicks is null ? 0 : ParseExactTicks(segment.DurationTicks);
        if (start < 0 || length < 0 || checked(start + length) < start)
        {
            throw new ExactValidationException(ChapterDiagnosticCode.InvalidStructure, "Segment bounds are invalid.");
        }

        return ParseExactRate(segment.FrameRate);
    }

    private static ImmutableArray<ImportedChapterSegment> FromExactSegments(IReadOnlyList<ExactSegment>? segments) =>
        segments is null ? [] : segments.Select(segment => new ImportedChapterSegment(segment.Id, segment.Name,
            ParseExactTicks(segment.StartTicks), segment.DurationTicks is null ? ChapterDuration.Unknown : ChapterDuration.FromTicks(ParseExactTicks(segment.DurationTicks)),
            ParseExactRate(segment.FrameRate), segment.ReferencedMediaFiles is null ? [] : segment.ReferencedMediaFiles.Select(static media =>
                new ReferencedMediaFile(media.DisplayName, media.RelativePath, media.AbsolutePath)).ToImmutableArray())).ToImmutableArray();

    private static ExactSegment ToExactSegment(ImportedChapterSegment segment) => new(segment.Id, segment.Name,
        segment.StartTicks.ToString(CultureInfo.InvariantCulture), segment.Duration.IsKnown ? segment.Duration.Ticks.ToString(CultureInfo.InvariantCulture) : null,
        ToExactRate(segment.FrameRate), ToExactRate(segment.FrameRate),
        [.. segment.ReferencedMediaFiles.Select(static media => new NodeReferencedMediaFile(media.DisplayName, media.RelativePath, media.AbsolutePath))]);
}
