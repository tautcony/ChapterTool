using System.Runtime.Versioning;
using System.Text.Json;
using ChapterTool.Contracts.Shortcuts;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmToolParityTests
{
    [Theory]
    [InlineData("en-US", "ja", "Japanese")]
    [InlineData("zh-CN", "deu", "德语")]
    [InlineData("ja-JP", "zh", "中国語")]
    public void XML_language_labels_use_shared_translations_and_preserve_codes(string culture, string code, string label)
    {
        var localizer = new WasmLocalizer();
        localizer.SetCulture(culture);
        using var workspace = new WasmWorkspace(new WasmChapterService(), localizer);
        Assert.Equal($"{code} — {label}", workspace.XmlLanguageName(code));
        Assert.StartsWith("und — ", workspace.XmlLanguageName("und"), StringComparison.Ordinal);
    }

    [Fact]
    public void ShortcutMappingRestoresDefaultsAndRejectsConflictingDrafts()
    {
        var old = JsonSerializer.Deserialize<WasmSettings>("{\"SchemaVersion\":1,\"Theme\":{\"PresetId\":\"ayu-dark\"}}");
        var defaults = WasmShortcuts.Normalize(old!.Shortcuts);
        Assert.Equal("load", WasmShortcuts.Bindings(defaults)["Ctrl+O"]);
        var draft = new ShortcutSettings(defaults) { ["save"] = "Ctrl+O" };
        Assert.Contains("save", WasmShortcuts.InvalidActions(draft));
        Assert.Equal(defaults, WasmShortcuts.Normalize(draft));
        draft["save"] = "Ctrl+Shift+S";
        Assert.Empty(WasmShortcuts.InvalidActions(draft));
        Assert.False(WasmShortcuts.Bindings(draft).ContainsKey("Ctrl+S"));
        Assert.Equal("save", WasmShortcuts.Bindings(draft)["Ctrl+Shift+S"]);
        Assert.Equal("insert", WasmShortcuts.Bindings(draft)["Insert"]);
        Assert.Equal("delete", WasmShortcuts.Bindings(draft)["Delete"]);
        Assert.Equal("next-clip", WasmShortcuts.Bindings(draft)["PageDown"]);
        Assert.False(WasmShortcuts.IsSupported("Ctrl+W"));
        Assert.Equal("undo", WasmShortcuts.Bindings(draft)["Meta+Z"]);
        Assert.Equal("redo", WasmShortcuts.Bindings(draft)["Ctrl+Shift+Z"]);
        draft["save"] = "Meta+O";
        Assert.Contains("save", WasmShortcuts.InvalidActions(draft));
        draft["save"] = "F9";
        Assert.Contains("save", WasmShortcuts.InvalidActions(draft));
    }

    [Fact]
    public void MalformedShortcutChildrenDoNotDiscardOtherVersionOnePreferences()
    {
        foreach (var child in new[] { "null", "[]", "42", "{\"save\":null,\"load\":42}" })
        {
            var settings = JsonSerializer.Deserialize<WasmSettings>("{\"SchemaVersion\":1,\"Theme\":{\"PresetId\":\"ayu-dark\"},\"Shortcuts\":" + child + "}")!;
            Assert.Equal("ayu-dark", settings.Theme!.PresetId);
            Assert.Equal("Ctrl+S", WasmShortcuts.Normalize(settings.Shortcuts)["save"]);
        }
    }

    [Fact]
    public void FilteredLogExportUsesCapturedMembershipAndQuotesUnicodeDetails()
    {
        var entries = new List<WasmLogEntry>
        {
            new(DateTimeOffset.UtcNow, "Info", "Unrelated"),
            new(DateTimeOffset.UtcNow, "Error", "失败,\"message\"", "hidden\n日本語")
        };
        var view = new WasmLogView(entries) { Search = "HIDDEN", Severity = "Error" };
        var visible = Assert.Single(view.Visible);
        view.SelectedId = visible.Id;
        view.InspectorOpen = true;
        var json = view.Export(false);
        var csv = view.Export(true);
        Assert.Single(JsonSerializer.Deserialize<WasmLogEntry[]>(json)!);
        Assert.Contains("\"失败,\"\"message\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"hidden\n日本語\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrelated", csv, StringComparison.Ordinal);
        entries.Add(new(DateTimeOffset.UtcNow, "Error", "Later", "hidden"));
        Assert.Equal(2, view.Visible.Count);
        Assert.Single(JsonSerializer.Deserialize<WasmLogEntry[]>(json)!);
        entries.Remove(visible);
        view.Refresh();
        Assert.Null(view.SelectedId);
        Assert.False(view.InspectorOpen);
        Assert.Equal("HIDDEN", view.Search);
        entries.Clear();
        view.Refresh();
        Assert.Empty(view.Visible);
    }

    [Fact]
    public async Task LiveBoundedSourceEvictionClosesAnUnavailableInspector()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        var view = new WasmLogView(workspace.Logs);
        var first = view.Visible[0];
        view.SelectedId = first.Id;
        view.InspectorOpen = true;
        var count = view.Visible.Count;
        workspace.RefreshRows();
        Assert.Equal(count + 1, view.Visible.Count);
        for (var index = 0; index < 205; index++)
        {
            workspace.RefreshRows();
        }
        view.Refresh();
        Assert.Equal(200, view.Visible.Count);
        Assert.Null(view.SelectedId);
        Assert.False(view.InspectorOpen);
    }
}
