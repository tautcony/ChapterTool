using System.Collections;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

internal static class LogRawValueFormatter
{
    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Format(ApplicationLogEntry entry, string summary)
    {
        var raw = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            ["level"] = entry.Level.ToString(),
            ["operation"] = NullIfEmpty(entry.Operation),
            ["category"] = NullIfEmpty(entry.Category),
            ["eventId"] = entry.EventId,
            ["eventName"] = NullIfEmpty(entry.EventName),
            ["message"] = summary,
            ["technicalDetail"] = NullIfEmpty(entry.TechnicalDetail),
            ["exception"] = NullIfEmpty(entry.ExceptionText),
            ["structuredState"] = NormalizeRawValue(entry.StructuredState, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
        };
        return JsonSerializer.Serialize(raw, RawJsonOptions);
    }

    private static object? NormalizeRawValue(object? value, int depth, HashSet<object> path)
    {
        if (TryNormalizeScalar(value, out var scalar))
        {
            return scalar;
        }

        if (depth >= 32)
        {
            return "[depth limit]";
        }

        var runtimeType = value!.GetType();
        if (!runtimeType.IsValueType && !path.Add(value))
        {
            return "[cycle]";
        }

        try
        {
            return NormalizeContainer(value, depth, path);
        }
        finally
        {
            if (!runtimeType.IsValueType)
            {
                path.Remove(value);
            }
        }
    }

    private static bool TryNormalizeScalar(object? value, out object? normalized)
    {
        if (value is null || value is string || value is bool || IsNumeric(value))
        {
            normalized = value;
            return true;
        }

        switch (value)
        {
            case DateTime dateTime:
                normalized = dateTime.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case DateTimeOffset dateTimeOffset:
                normalized = dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case char or Enum or Guid or Uri or TimeSpan:
                normalized = value.ToString();
                return true;
            default:
                normalized = null;
                return false;
        }
    }

    private static bool IsNumeric(object value) => value switch
    {
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
        _ => false
    };

    private static object? NormalizeContainer(object value, int depth, HashSet<object> path) => value switch
    {
        IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
            static pair => pair.Key,
            pair => NormalizeRawValue(pair.Value, depth + 1, path),
            StringComparer.Ordinal),
        IDictionary dictionary => NormalizeDictionary(dictionary, depth, path),
        IEnumerable enumerable => enumerable.Cast<object?>()
            .Select(item => NormalizeRawValue(item, depth + 1, path))
            .ToList(),
        _ => value.ToString()
    };

    private static Dictionary<string, object?> NormalizeDictionary(IDictionary dictionary, int depth, HashSet<object> path)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in dictionary)
        {
            normalized[item.Key?.ToString() ?? string.Empty] = NormalizeRawValue(item.Value, depth + 1, path);
        }

        return normalized;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
