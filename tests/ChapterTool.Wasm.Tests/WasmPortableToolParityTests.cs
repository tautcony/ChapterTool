using System.Runtime.Versioning;
using System.Text.Json;
using ChapterTool.Wasm.Components;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmPortableToolParityTests
{
    [Fact]
    public void ExportDraftValidatesEncodingAndOnlyRequiresXmlLanguageForXmlOutput()
    {
        var languages = new[] { "und", "eng", "jpn" };

        Assert.True(new ExportOptionsDraft("jpn", 1, true).IsValid(languages, 2, xmlLanguageEnabled: true));
        Assert.False(new ExportOptionsDraft("missing", 1, true).IsValid(languages, 2, xmlLanguageEnabled: true));
        Assert.True(new ExportOptionsDraft("und", 1, false).IsValid(languages, 2, xmlLanguageEnabled: false));
        Assert.False(new ExportOptionsDraft("und", 2, false).IsValid(languages, 2, xmlLanguageEnabled: false));
        Assert.False(new ExportOptionsDraft("und", -1, false).IsValid(languages, 2, xmlLanguageEnabled: false));
    }

    [Fact]
    public void LogViewSearchesEveryBrowserRecordFieldAndAppliesSeveritySemantics()
    {
        var info = new WasmLogEntry(DateTimeOffset.Parse("2026-10-10T01:02:03Z"), "Info", "Loaded chapter file", "path=clip.mpls");
        var warning = new WasmLogEntry(DateTimeOffset.Parse("2026-10-10T01:02:04Z"), "Warning", "Partial import");
        var error = new WasmLogEntry(DateTimeOffset.Parse("2026-10-10T01:02:05Z"), "Error", "Failed import");
        var critical = new WasmLogEntry(DateTimeOffset.Parse("2026-10-10T01:02:06Z"), "Critical", "Unhandled browser failure");
        var view = new WasmLogView([info, warning, error, critical]);

        view.Search = "clip.mpls";
        Assert.Equal(info, Assert.Single(view.Visible));
        view.Search = info.Timestamp.ToString("O");
        Assert.Equal(info, Assert.Single(view.Visible));
        view.Search = critical.Id.ToString("N");
        Assert.Equal(critical, Assert.Single(view.Visible));

        view.Search = string.Empty;
        view.Severity = "Error";
        Assert.Equal(new[] { error, critical }, view.Visible);
        view.Severity = "Information";
        Assert.Equal(info, Assert.Single(view.Visible));
        view.Severity = "Warning";
        Assert.Equal(warning, Assert.Single(view.Visible));
    }

    [Fact]
    public void LogInspectorRetainsSelectionWhenFiltersHideItAndClosesAfterEviction()
    {
        var selected = new WasmLogEntry(DateTimeOffset.UtcNow, "Info", "Loaded file", "detail");
        var retained = new List<WasmLogEntry> { selected, new(DateTimeOffset.UtcNow, "Error", "Failed file") };
        var view = new WasmLogView(retained) { SelectedId = selected.Id, InspectorOpen = true };

        view.Search = "Failed";
        view.Refresh();
        Assert.Equal("Failed file", Assert.Single(view.Visible).Message);
        Assert.Equal(selected, view.Selected);
        Assert.True(view.InspectorOpen);

        retained.Remove(selected);
        view.Refresh();
        Assert.Null(view.SelectedId);
        Assert.False(view.InspectorOpen);
    }

    [Fact]
    public void LogSummariesStayCompactWhileRawJsonAndCsvKeepFullBrowserRecords()
    {
        var message = new string('x', 240);
        var entry = new WasmLogEntry(DateTimeOffset.UtcNow, "Error", message, "details, with \"quotes\"");
        var view = new WasmLogView([entry]);

        Assert.Equal(180, WasmLogView.CompactSummary(message).Length);
        Assert.EndsWith("…", WasmLogView.CompactSummary(message), StringComparison.Ordinal);
        Assert.Equal(message, Assert.Single(JsonSerializer.Deserialize<WasmLogEntry[]>(view.Export(csv: false))!).Message);
        Assert.Contains("\"details, with \"\"quotes\"\"\"", view.Export(csv: true), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentToolCandidateShowsExactTimesAndFramesAndCancelDoesNotCommit()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadAsync("repeating.txt", [
            .. "CHAPTER01=00:00:00.000\nCHAPTER01NAME=Opening\nCHAPTER02=00:00:52.094\nCHAPTER02NAME=Later\n"u8
        ]);
        var originalRows = workspace.Rows.ToArray();
        var originalHistoryCount = workspace.HistoryEntries.Count;

        var preview = Assert.IsType<ChapterTool.Core.Session.ChapterContentPreview>(workspace.PrepareFrameShiftPreview(-1));
        Assert.True(preview.IsValid, string.Join(Environment.NewLine, preview.Errors));
        var comparison = Assert.Single(workspace.ContentPreviewProjection!.Chapters,
            row => row.Before?.DisplayNumber == 2);
        Assert.Equal(417_083L, comparison.DeltaTicks);
        Assert.Equal("00:00:52.094", workspace.FormatExpressionTimeTicks(comparison.Before!.StartTicks));
        Assert.Equal("00:00:52.1357083", workspace.FormatExpressionTimeTicks(comparison.Candidate!.StartTicks));
        Assert.Equal(workspace.FormatRowFramePresentation(1, workspace.Rows[1].FramesInfo, false),
            workspace.FormatPreviewFramePresentation(comparison, false, false));
        Assert.NotEqual(workspace.FormatPreviewFramePresentation(comparison, false, false).PlainNumericText,
            workspace.FormatPreviewFramePresentation(comparison, true, false).PlainNumericText);

        workspace.CancelContentPreview();

        Assert.Null(workspace.ContentPreview);
        Assert.Equal(originalRows, workspace.Rows);
        Assert.Equal(originalHistoryCount, workspace.HistoryEntries.Count);
    }
}
