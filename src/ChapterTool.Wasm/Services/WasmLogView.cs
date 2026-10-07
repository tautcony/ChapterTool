using System.Globalization;
using System.Text.Json;

namespace ChapterTool.Wasm.Services;

public sealed class WasmLogView(IReadOnlyList<WasmLogEntry> entries)
{
    public string Search { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public Guid? SelectedId { get; set; }

    public bool InspectorOpen { get; set; }

    public IReadOnlyList<WasmLogEntry> Visible => entries.Where(entry =>
        (Severity.Length == 0 || entry.Level.Equals(Severity, StringComparison.OrdinalIgnoreCase))
        && (Search.Length == 0 || entry.Message.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || (entry.Details?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false)
            || entry.Level.Contains(Search, StringComparison.OrdinalIgnoreCase))).ToArray();

    public WasmLogEntry? Selected => Visible.FirstOrDefault(entry => entry.Id == SelectedId);

    public void Refresh()
    {
        if (Selected is null)
        {
            SelectedId = null;
            InspectorOpen = false;
        }
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
}
