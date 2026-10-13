using System.Collections;
using System.Globalization;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Wasm.Services;

public static class WasmHistoryProjection
{
    public static IReadOnlyList<WasmHistoryChangeRow> FilterChanges(IEnumerable<WasmHistoryChangeRow> changes, WasmHistoryChangeFilter filter) =>
        changes.Where(change => filter switch
        {
            WasmHistoryChangeFilter.Added => !change.Before.IsPresent && change.After.IsPresent,
            WasmHistoryChangeFilter.Removed => change.Before.IsPresent && !change.After.IsPresent,
            WasmHistoryChangeFilter.Modified => change.Before.IsPresent && change.After.IsPresent,
            _ => true
        }).ToArray();

    public static string OperationTitle(WasmLocalizer localizer, HistoryOperationDescriptor? operation, string? fallbackDescription = null)
    {
        if ((operation is null || operation.Kind == "generic") && !string.IsNullOrWhiteSpace(fallbackDescription))
        {
            return fallbackDescription;
        }
        var kind = operation?.Kind;
        var key = kind switch
        {
            "load" => "History.Operation.Load",
            "chapter.cell-edit" => "History.Operation.Edit",
            "chapter.delete" => "History.Operation.Delete",
            "chapter.insert" => "History.Operation.Insert",
            "chapter.frame-shift" => "History.Operation.FrameShift",
            "chapter.frame-rate-change" => "History.Operation.FrameRateChange",
            "expression.apply" => "History.Operation.Expression",
            "template.apply" => "History.Operation.Template",
            "chapter.options-apply" => "History.Operation.Options",
            "workspace.merge" => "History.Operation.Merge",
            "workspace.split" => "History.Operation.Split",
            "workspace.append" => "History.Operation.Append",
            _ => "History.Operation.Generic"
        };
        var title = localizer.T(key);
        if (kind == "chapter.cell-edit")
        {
            var field = operation?.Parameters.FirstOrDefault(static parameter => parameter.Name == "field")?.Value;
            if (!string.IsNullOrWhiteSpace(field))
            {
                title = $"{title} · {localizer.T($"History.Enum.Field.{field}")}";
            }
        }
        return title;
    }

    public static WasmHistoryDetailsProjection Project(WasmLocalizer localizer, HistoryNodeDetails details)
    {
        var culture = CultureInfo.GetCultureInfo(localizer.Culture);
        var parameters = ProjectParameters(localizer, details.Operation);
        var changes = details.Changes.Select(change => ProjectChange(localizer, details, change, culture)).ToArray();
        var summaryParts = new List<string>();
        Add(details.Summary.AddedChapters, "History.Added");
        Add(details.Summary.RemovedChapters, "History.Removed");
        Add(details.Summary.ModifiedChapters, "History.Modified");
        Add(details.Summary.ChangedTracks, "History.ChangedTracks");
        Add(details.Summary.ChangedSegments, "History.ChangedSegments");
        var rootRows = details.RootDocument?.Tracks.SelectMany(track => track.Chapters.Select(chapter =>
            new WasmHistoryRootChapterRow(track.Name, chapter.DisplayNumber, chapter.Name,
                FormatTimeSpan(TimeSpan.FromTicks(chapter.StartTicks)), chapter.FramesInfo))).ToArray() ?? [];

        return new WasmHistoryDetailsProjection(
            OperationTitle(localizer, details.Operation, details.Description),
            details.PublishedAt?.ToLocalTime().ToString("g", culture),
            string.Join(" · ", summaryParts),
            parameters,
            changes,
            rootRows,
            details.RootDocument?.Title,
            details.RootDocument?.Tracks.Length ?? 0,
            details.RootDocument?.Tracks.Sum(static track => track.Chapters.Length) ?? 0);

        void Add(int count, string key)
        {
            if (count > 0)
            {
                summaryParts.Add($"{count} {localizer.T(key)}");
            }
        }
    }

    private static IReadOnlyList<WasmHistoryParameterRow> ProjectParameters(WasmLocalizer localizer, HistoryOperationDescriptor? operation)
    {
        if (operation is null)
        {
            return [];
        }
        var cellEditField = operation.Parameters.FirstOrDefault(static parameter => parameter.Name == "field")?.Value;
        return operation.Parameters
            .Where(parameter => IsVisibleParameter(parameter, operation.Kind, cellEditField))
            .Select(parameter => new WasmHistoryParameterRow(
                localizer.T($"History.Parameter.{parameter.Name}"),
                FormatParameter(localizer, parameter),
                parameter.ValueKind))
            .ToArray();
    }

    private static bool IsVisibleParameter(HistoryOperationParameter parameter, string operationKind, string? cellEditField)
    {
        if (parameter.Name is "chapterId" or "chapterIds" or "trackId" or "trackIds" or "targetChapterIds")
        {
            return false;
        }

        return operationKind != "chapter.cell-edit"
            || (parameter.Name is not ("field" or "value" or "trackName")
                && (parameter.Name != "displayFps" || cellEditField is "Frame" or "StartTime"));
    }

    private static WasmHistoryChangeRow ProjectChange(WasmLocalizer localizer, HistoryNodeDetails details,
        HistoryFieldChange change, CultureInfo culture)
    {
        var context = change.Group == "Chapter"
            ? localizer.Format("History.ChapterContext", change.Context ?? string.Empty, change.ObjectLabel)
            : change.Context is null ? change.ObjectLabel : $"{change.Context} · {change.ObjectLabel}";
        if (change.Field is "StartTicks" or "EndTicks" or "FramesInfo")
        {
            var displayRate = details.Operation?.Parameters
                .FirstOrDefault(static parameter => parameter.Name == "displayFps")?.Value;
            if (decimal.TryParse(displayRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) && rate > 0)
            {
                context += $" · {localizer.T("History.Parameter.displayFps")}: {FormatFrameRate(localizer, rate)}";
            }
            else if (change.BeforeFrameRate.IsKnown || change.AfterFrameRate.IsKnown)
            {
                var beforeText = change.BeforeFrameRate.IsKnown ? FormatFrameRate(localizer, change.BeforeFrameRate) : localizer.T("History.Presence.Absent");
                var afterText = change.AfterFrameRate.IsKnown ? FormatFrameRate(localizer, change.AfterFrameRate) : localizer.T("History.Presence.Absent");
                context += $" · {localizer.Format("History.FrameRateContext", beforeText, afterText)}";
            }
        }

        var beforeTextValue = FormatHistoryValue(localizer, details, change, change.Before, true, culture);
        var afterTextValue = FormatHistoryValue(localizer, details, change, change.After, false, culture);
        return new WasmHistoryChangeRow(change.Group, change.ObjectId,
            localizer.T($"History.Field.{change.Field}"), context, change.Before, change.After,
            beforeTextValue, afterTextValue, FormatDelta(change));
    }

    private static string FormatHistoryValue(WasmLocalizer localizer, HistoryNodeDetails details,
        HistoryFieldChange change, HistoryFieldValue value, bool before, CultureInfo culture)
    {
        var rateText = details.Operation?.Parameters.FirstOrDefault(static parameter => parameter.Name == "displayFps")?.Value;
        if (value.IsPresent && change.Field == "FramesInfo" && value.Value is string frameText
            && string.IsNullOrWhiteSpace(frameText)
            && TryGetRelatedStartTicks(details, change, before, out var startTicks))
        {
            FramePresentationInput? input = null;
            var exactRate = before ? change.BeforeFrameRate : change.AfterFrameRate;
            if (exactRate.IsKnown)
            {
                input = new FramePresentationInput(startTicks, exactRate, frameText);
            }
            else if (decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var displayRate) && displayRate > 0)
            {
                input = new FramePresentationInput(startTicks, FrameRateValue.Approximate(displayRate), frameText);
            }
            if (input is not null)
            {
                var frameDisplay = details.Operation?.Parameters.FirstOrDefault(static parameter => parameter.Name == "frameDisplay")?.Value;
                var placesText = details.Operation?.Parameters.FirstOrDefault(static parameter => parameter.Name == "frameDecimalPlaces")?.Value;
                var places = int.TryParse(placesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPlaces) ? parsedPlaces : -1;
                var policy = new FramePresentationPolicy(frameDisplay == "Round", false, places);
                return FrameValueFormatter.Format(input, policy).PlainNumericText;
            }
        }
        return FormatValue(localizer, value, change.Field, culture);
    }

    private static bool TryGetRelatedStartTicks(HistoryNodeDetails details, HistoryFieldChange change, bool before, out long ticks)
    {
        var start = details.Changes.FirstOrDefault(candidate => candidate.Group == change.Group
            && candidate.ObjectId == change.ObjectId && candidate.Field == "StartTicks");
        var value = before ? start?.Before : start?.After;
        if (value is { IsPresent: true, Value: long startTicks })
        {
            ticks = startTicks;
            return true;
        }
        ticks = 0;
        return false;
    }

    private static string FormatValue(WasmLocalizer localizer, HistoryFieldValue value, string field, CultureInfo culture)
    {
        if (!value.IsPresent)
        {
            return localizer.T("History.Presence.Absent");
        }
        return value.Value switch
        {
            null => localizer.T("History.Presence.Null"),
            string { Length: 0 } => localizer.T("History.Presence.Empty"),
            long ticks when field is "StartTicks" or "EndTicks" => FormatTimeSpan(TimeSpan.FromTicks(ticks)),
            ChapterDuration duration => duration.IsKnown
                ? FormatTimeSpan(TimeSpan.FromTicks(duration.Ticks))
                : localizer.T("History.Presence.Unknown"),
            ChapterFrameRate frameRate => FormatFrameRate(localizer, frameRate),
            HistoryTrackReference reference => string.IsNullOrEmpty(reference.Label)
                ? localizer.T("History.Track.Unknown")
                : reference.Label,
            ChapterImportFormat format => localizer.T($"History.Enum.ImportFormat.{format}"),
            ChapterKind kind => localizer.T($"History.Enum.ChapterKind.{kind}"),
            FrameAccuracy accuracy => localizer.T($"History.Enum.FrameAccuracy.{accuracy}"),
            IEnumerable sequence when value.Value is not string => FormatSequence(sequence, culture),
            IFormattable formattable => formattable.ToString(null, culture) ?? string.Empty,
            _ => value.Value.ToString() ?? string.Empty
        };
    }

    private static string FormatSequence(IEnumerable sequence, CultureInfo culture)
    {
        var items = new List<string>();
        foreach (var item in sequence)
        {
            items.Add(item is HistoryOrderItem order ? order.Label : Convert.ToString(item, culture) ?? string.Empty);
        }

        return string.Join(", ", items);
    }

    private static string? FormatDelta(HistoryFieldChange change)
    {
        if (!change.Before.IsPresent || !change.After.IsPresent)
        {
            return null;
        }
        if (change.Before.Value is long before && change.After.Value is long after && change.Field is "StartTicks" or "EndTicks")
        {
            return FormatTimeSpan(TimeSpan.FromTicks(checked(after - before)), true);
        }
        if (change.Before.Value is ChapterDuration previous && change.After.Value is ChapterDuration next && previous.IsKnown && next.IsKnown)
        {
            return FormatTimeSpan(TimeSpan.FromTicks(checked(next.Ticks - previous.Ticks)), true);
        }
        return null;
    }

    private static string FormatParameter(WasmLocalizer localizer, HistoryOperationParameter parameter)
    {
        if (parameter.Value is null)
        {
            return localizer.T("History.Presence.Null");
        }
        if (parameter.Name == "field")
        {
            return localizer.T($"History.Enum.Field.{parameter.Value}");
        }
        if (parameter.ValueKind == "boolean" && bool.TryParse(parameter.Value, out var boolean))
        {
            return localizer.T(boolean ? "History.Boolean.True" : "History.Boolean.False");
        }
        if (parameter.ValueKind == "integer" && long.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer.ToString(CultureInfo.GetCultureInfo(localizer.Culture));
        }
        if (parameter.ValueKind == "decimal" && decimal.TryParse(parameter.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return parameter.Name == "displayFps" ? FormatFrameRate(localizer, decimalValue) : decimalValue.ToString(CultureInfo.GetCultureInfo(localizer.Culture));
        }
        if (parameter.Name is "deleteTiming" or "frameDisplay")
        {
            return localizer.T($"History.Enum.{parameter.Name}.{parameter.Value}");
        }
        return parameter.Value;
    }

    private static string FormatFrameRate(WasmLocalizer localizer, decimal rate)
    {
        var culture = CultureInfo.GetCultureInfo(localizer.Culture);
        return $"{rate.ToString("0.###", culture)} {localizer.T("History.Unit.Fps")}";
    }

    private static string FormatFrameRate(WasmLocalizer localizer, FrameRateValue rate) =>
        $"{rate} {localizer.T("History.Unit.Fps")}";

    private static string FormatFrameRate(WasmLocalizer localizer, ChapterFrameRate rate)
    {
        var culture = CultureInfo.GetCultureInfo(localizer.Culture);
        var value = rate.Denominator == 1 ? rate.Numerator.ToString(culture) : $"{rate.Numerator.ToString(culture)}/{rate.Denominator.ToString(culture)}";
        return $"{value} {localizer.T("History.Unit.Fps")}";
    }

    private static string FormatTimeSpan(TimeSpan value, bool signed = false)
    {
        var totalMilliseconds = (long)decimal.Round(value.Ticks / (decimal)TimeSpan.TicksPerMillisecond, 0, MidpointRounding.ToEven);
        var absoluteMilliseconds = Math.Abs(totalMilliseconds);
        var body = $"{absoluteMilliseconds / 3_600_000:00}:{absoluteMilliseconds / 60_000 % 60:00}:{absoluteMilliseconds / 1_000 % 60:00}.{absoluteMilliseconds % 1_000:000}";
        return value < TimeSpan.Zero ? "−" + body : signed && value > TimeSpan.Zero ? "+" + body : body;
    }
}

public sealed record WasmHistoryDetailsProjection(string OperationTitle, string? PublishedAtText, string EffectSummary,
    IReadOnlyList<WasmHistoryParameterRow> Parameters, IReadOnlyList<WasmHistoryChangeRow> Changes,
    IReadOnlyList<WasmHistoryRootChapterRow> RootChapterRows, string? RootTitle, int RootTrackCount, int RootChapterCount);

public sealed record WasmHistoryParameterRow(string Name, string? Value, string ValueKind);

public sealed record WasmHistoryChangeRow(string Group, string ObjectId, string Field, string ObjectLabel,
    HistoryFieldValue Before, HistoryFieldValue After, string BeforeText, string AfterText, string? DeltaText);

public sealed record WasmHistoryRootChapterRow(string TrackName, int DisplayNumber, string Name, string StartTime, string FramesInfo);

public enum WasmHistoryChangeFilter
{
    All,
    Added,
    Removed,
    Modified
}
