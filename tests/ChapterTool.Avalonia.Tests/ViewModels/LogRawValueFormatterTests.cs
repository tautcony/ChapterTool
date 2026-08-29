using System.Text.Json;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class LogRawValueFormatterTests
{
    [Fact]
    public void Replaces_cycles_with_a_marker_in_raw_json()
    {
        var state = new Dictionary<string, object?>(StringComparer.Ordinal);
        state["self"] = state;
        var entry = new ApplicationLogEntry(
            DateTimeOffset.UnixEpoch,
            LogLevel.Information,
            "message",
            StructuredState: state);

        using var document = JsonDocument.Parse(LogRawValueFormatter.Format(entry, "message"));

        Assert.Equal("[cycle]", document.RootElement.GetProperty("structuredState").GetProperty("self").GetString());
    }

    [Fact]
    public void Preserves_deterministic_metadata_and_unicode_text()
    {
        var entry = new ApplicationLogEntry(
            DateTimeOffset.UnixEpoch,
            LogLevel.Error,
            "message",
            Category: "Test",
            EventId: 7,
            EventName: "Diagnostic",
            TechnicalDetail: " detail ",
            ExceptionText: " exception ",
            StructuredState: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "章节"
            });

        var raw = LogRawValueFormatter.Format(entry, "章节");

        Assert.Contains("章节", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u7AE0", raw, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(raw);
        Assert.Equal("Test", document.RootElement.GetProperty("category").GetString());
        Assert.Equal(7, document.RootElement.GetProperty("eventId").GetInt32());
        Assert.Equal("detail", document.RootElement.GetProperty("technicalDetail").GetString());
    }
}
