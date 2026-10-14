using System.Text.Json;
using System.Text.Json.Serialization;
using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Wasm.Services;

public sealed class WasmShortcutSettingsConverter : JsonConverter<ShortcutSettings>
{
    public override ShortcutSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var settings = new ShortcutSettings();
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    settings[property.Name] = property.Value.GetString()!;
                }
            }
        }
        return settings;
    }

    public override void Write(Utf8JsonWriter writer, ShortcutSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (id, gesture) in value)
        {
            writer.WriteString(id, gesture);
        }
        writer.WriteEndObject();
    }
}
