using System.Collections;
using System.Globalization;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

internal static class ImportSummaryFormatter
{
    public static string Format(ApplicationLogEntry entry)
    {
        if (!string.Equals(entry.MessageKey, "Log.ImportSummary", StringComparison.Ordinal)
            || entry.StructuredState is not { Count: > 0 } state
            || !TryGetDictionaryValue(state, "details", out var detailsObject)
            || AsDictionary(detailsObject) is not { Count: > 0 } details
            || !TryGetListValue(details, "groups", out var groups)
            || groups.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        var index = 1;
        var foundDiscEntry = false;

        foreach (var groupObject in groups)
        {
            var group = AsDictionary(groupObject);
            if (group is null || !TryGetListValue(group, "entries", out var entries) || entries.Count == 0)
            {
                continue;
            }

            var sourcePath = GetString(group, "sourcePath");
            foreach (var entryObject in entries)
            {
                var importEntry = AsDictionary(entryObject);
                if (importEntry is null)
                {
                    continue;
                }

                var label = FirstNonEmpty(GetString(importEntry, "label"), GetString(importEntry, "source"));
                var sourceType = GetString(importEntry, "sourceType");
                if (!IsDiscImportEntry(sourcePath, label, sourceType))
                {
                    continue;
                }

                foundDiscEntry = true;
                var duration = GetString(importEntry, "duration");
                var chapters = FormatCount(GetValue(importEntry, "chapters"), "chapter");
                var fps = GetString(importEntry, "fps");
                var mediaTrackLines = GetMediaTrackLines(importEntry);

                lines.Add($"{index++}) {FormatImportHeader(sourcePath, label, duration)}");
                if (!string.IsNullOrWhiteSpace(chapters))
                {
                    lines.Add($"   - Chapters, {chapters}");
                }

                if (mediaTrackLines.Count > 0)
                {
                    lines.AddRange(mediaTrackLines.Select(static line => $"   - {line}"));
                }

                if (mediaTrackLines.Count == 0 && !string.IsNullOrWhiteSpace(sourceType))
                {
                    lines.Add($"   - Format, {sourceType}");
                }

                if (mediaTrackLines.Count == 0 && !string.IsNullOrWhiteSpace(fps) && !string.Equals(fps, "0", StringComparison.Ordinal))
                {
                    lines.Add($"   - FPS, {fps}");
                }

                lines.Add(string.Empty);
            }
        }

        if (!foundDiscEntry)
        {
            return string.Empty;
        }

        if (TryGetListValue(details, "diagnostics", out var diagnostics) && diagnostics.Count > 0)
        {
            var diagnosticLines = FormatImportDiagnostics(diagnostics);
            if (!string.IsNullOrWhiteSpace(diagnosticLines))
            {
                lines.Add("Diagnostics:");
                lines.Add(diagnosticLines);
            }
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatImportDiagnostics(IReadOnlyList<object?> diagnostics)
    {
        var lines = new List<string>();
        foreach (var diagnosticObject in diagnostics)
        {
            var diagnostic = AsDictionary(diagnosticObject);
            if (diagnostic is null)
            {
                continue;
            }

            var code = GetString(diagnostic, "code");
            var message = GetString(diagnostic, "message");
            var severity = GetString(diagnostic, "severity");
            var summary = string.Join(": ", new[]
            {
                FirstNonEmpty(severity, code),
                message
            }.Where(static item => !string.IsNullOrWhiteSpace(item)));
            if (!string.IsNullOrWhiteSpace(summary))
            {
                lines.Add($"- {summary}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatImportHeader(string? sourcePath, string? label, string? duration)
    {
        var sourceName = Path.GetFileName(sourcePath ?? string.Empty);
        var isFileImport = HasExtension(sourceName, ".mpls") || HasExtension(sourceName, ".ifo");

        if (isFileImport)
        {
            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                segments.Add(sourceName);
            }

            if (!string.IsNullOrWhiteSpace(label) && !string.Equals(label, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(label);
            }

            if (!string.IsNullOrWhiteSpace(duration) && !ContainsDuration(label, duration))
            {
                segments.Add(duration);
            }

            return string.Join(", ", segments.Where(static item => !string.IsNullOrWhiteSpace(item)));
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            return string.IsNullOrWhiteSpace(duration) || ContainsDuration(label, duration)
                ? label
                : $"{label}, {duration}";
        }

        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            return string.IsNullOrWhiteSpace(duration)
                ? sourceName
                : $"{sourceName}, {duration}";
        }

        return duration ?? string.Empty;
    }

    private static bool IsDiscImportEntry(string? sourcePath, string? label, string? sourceType)
    {
        if (HasExtension(sourcePath, ".mpls") || HasExtension(sourcePath, ".ifo") || HasExtension(sourcePath, ".bdmv"))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sourceType)
            && (string.Equals(sourceType, "Blu-ray MPLS", StringComparison.Ordinal)
                || string.Equals(sourceType, "DVD IFO", StringComparison.Ordinal)
                || string.Equals(sourceType, "BDMV", StringComparison.Ordinal)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(label)
            && (label.Contains(".m2ts", StringComparison.OrdinalIgnoreCase)
                || label.Contains(".mpls", StringComparison.OrdinalIgnoreCase)
                || label.StartsWith("VTS_", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExtension(string? path, string extension) =>
        string.Equals(Path.GetExtension(path ?? string.Empty), extension, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsDuration(string? text, string duration) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains($"({duration})", StringComparison.Ordinal)
            || text.EndsWith($", {duration}", StringComparison.Ordinal)
            || text.EndsWith($" {duration}", StringComparison.Ordinal));

    private static string FormatCount(object? value, string noun)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
        {
            var count = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> GetMediaTrackLines(IReadOnlyDictionary<string, object?> importEntry)
    {
        if (!TryGetListValue(importEntry, "mediaTracks", out var mediaTracks))
        {
            return [];
        }

        return mediaTracks
            .Select(AsDictionary)
            .Where(static track => track is not null)
            .Select(static track => GetString(track!, "summary"))
            .Where(static summary => !string.IsNullOrWhiteSpace(summary))
            .ToList();
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string GetString(IReadOnlyDictionary<string, object?> values, string key) =>
        GetValue(values, key)?.ToString() ?? string.Empty;

    private static bool TryGetDictionaryValue(IReadOnlyDictionary<string, object?> values, string key, out object? value) =>
        values.TryGetValue(key, out value);

    private static bool TryGetListValue(IReadOnlyDictionary<string, object?> values, string key, out IReadOnlyList<object?> items)
    {
        items = [];
        return values.TryGetValue(key, out var value) && (items = AsList(value)).Count > 0;
    }

    private static IReadOnlyDictionary<string, object?>? AsDictionary(object? value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                return readOnlyDictionary;
            case IDictionary dictionary:
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry item in dictionary)
                {
                    result[item.Key?.ToString() ?? string.Empty] = item.Value;
                }

                return result;
            }
            default:
                return null;
        }
    }

    private static IReadOnlyList<object?> AsList(object? value)
    {
        if (value is null or string)
        {
            return [];
        }

        if (value is IReadOnlyList<object?> readOnlyList)
        {
            return readOnlyList;
        }

        if (value is IEnumerable enumerable)
        {
            return [.. enumerable.Cast<object?>()];
        }

        return [];
    }
}
