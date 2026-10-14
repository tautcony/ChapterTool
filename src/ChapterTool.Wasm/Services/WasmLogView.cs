using System.Globalization;
using System.Text.Json;

namespace ChapterTool.Wasm.Services;

public sealed class WasmLogView(IReadOnlyList<WasmLogEntry> entries)
{
    private const int MaxSummaryLength = 180;

    public string Search { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public Guid? SelectedId { get; set; }

    public bool InspectorOpen { get; set; }

    public bool HasAnyEntries => entries.Count > 0;

    public IReadOnlyList<WasmLogEntry> Visible => entries.Where(entry =>
        MatchesSeverity(entry.Level, Severity) && MatchesSearch(entry, Search)).ToArray();

    public WasmLogEntry? Selected => SelectedId is { } id
        ? entries.FirstOrDefault(entry => entry.Id == id)
        : null;

    public static string CompactSummary(string message) => message.Length <= MaxSummaryLength
        ? message
        : message[..(MaxSummaryLength - 1)].TrimEnd() + "…";

    public static bool SearchesAdditionalFields(WasmLogEntry entry, string query, string summary, string time, string level)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var fullTimestamp = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture);
        return (entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !summary.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (entry.Details?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || entry.Id.ToString("D").Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Id.ToString("N").Contains(query, StringComparison.OrdinalIgnoreCase)
            || (fullTimestamp.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !time.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (entry.Level.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !level.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public void Refresh()
    {
        if (Selected is null)
        {
            SelectedId = null;
            InspectorOpen = false;
        }
    }

    public void ClearSelection()
    {
        SelectedId = null;
        InspectorOpen = false;
    }

    public string Export(bool csv)
    {
        var snapshot = Visible.OrderBy(entry => entry.Timestamp).ToArray();
        if (!csv)
        {
            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        }
        return "Timestamp,Level,Message,Details\r\n" + string.Join("\r\n", snapshot.Select(entry =>
            string.Join(",", Quote(entry.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                Quote(entry.Level), Quote(entry.Message), Quote(entry.Details ?? string.Empty)))) + "\r\n";
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static bool MatchesSeverity(string level, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return filter.ToUpperInvariant() switch
        {
            "INFO" or "INFORMATION" => level.Equals("Info", StringComparison.OrdinalIgnoreCase)
                || level.Equals("Information", StringComparison.OrdinalIgnoreCase),
            "WARNING" => level.Equals("Warning", StringComparison.OrdinalIgnoreCase),
            "ERROR" => level.Equals("Error", StringComparison.OrdinalIgnoreCase)
                || level.Equals("Critical", StringComparison.OrdinalIgnoreCase)
                || level.Equals("Fatal", StringComparison.OrdinalIgnoreCase),
            _ => level.Equals(filter, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool MatchesSearch(WasmLogEntry entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var timestamp = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture);
        var localTime = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        return entry.Id.ToString("D").Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Id.ToString("N").Contains(query, StringComparison.OrdinalIgnoreCase)
            || timestamp.Contains(query, StringComparison.OrdinalIgnoreCase)
            || localTime.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Level.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (entry.Details?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
