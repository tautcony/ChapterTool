using System.Text.Json;
using ChapterTool.Contracts.Configuration;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

/// <summary>Serializes the browser settings contract without desktop path values.</summary>
public static class BrowserSettingsCodec
{
    public const string StorageKey = "chaptertool.wasm.settings";
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(ChapterToolSettings settings)
    {
        var normalized = ChapterToolSettings.Normalize(settings) with
        {
            Application = ChapterToolSettings.Normalize(settings).Application with
            {
                SavingPath = null,
                MainWindowLocation = null,
                MkvToolnixPath = null,
                FfprobePath = null
            }
        };
        return JsonSerializer.Serialize(normalized with { SchemaVersion = SchemaVersion }, Options);
    }

    public static bool TryDeserialize(string? json, out ChapterToolSettings settings)
    {
        settings = ChapterToolSettings.Default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schemaVersion)
                || schemaVersion != SchemaVersion)
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<ChapterToolSettings>(json, Options);
            settings = ChapterToolSettings.Normalize(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
