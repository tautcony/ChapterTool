using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class LogToolViewModelTests
{
    [Fact]
    public void ViewModel_filters_structured_entries_and_preserves_technical_details()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["operation"] = "Load",
            ["code"] = "Import.Partial",
            ["message"] = "Import failed",
            ["TechnicalDetail"] = "stderr=failed"
        };
        logger.Log(LogLevel.Error, new EventId(7, "Log.Diagnostic"), state, null, static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("Log.Diagnostic", entry.Entry.MessageKey);
        Assert.Equal("Import failed", entry.Summary);
        Assert.Contains("Load", entry.Details, StringComparison.Ordinal);
        Assert.Contains("stderr=failed", entry.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("TechnicalDetail=stderr=failed", entry.Details, StringComparison.Ordinal);
        Assert.Contains(entry.StructuredProperties, property => property.Name == "code" && property.Value == "Import.Partial");
        Assert.Equal("Errors", entry.LevelText);

        viewModel.SelectedFilter = viewModel.FilterOptions.Single(option => option.Value == LogSeverityFilter.Information);
        Assert.Empty(viewModel.FilteredEntries);

        viewModel.SelectedFilter = viewModel.FilterOptions[0];
        Assert.Single(viewModel.FilteredEntries);
    }

    [Fact]
    public void ViewModel_exposes_distinct_severity_states()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        logger.LogInformation("info");
        logger.LogWarning("warning");
        logger.LogError("error");

        using var viewModel = new LogToolViewModel(service, localizer);

        Assert.Equal(3, viewModel.FilteredEntries.Count);
        var info = Assert.Single(viewModel.FilteredEntries, entry => entry.IsInformation);
        var warning = Assert.Single(viewModel.FilteredEntries, entry => entry.IsWarning);
        var error = Assert.Single(viewModel.FilteredEntries, entry => entry.IsError);
        Assert.Equal("Information", info.LevelText);
        Assert.Equal("Warnings", warning.LevelText);
        Assert.Equal("Errors", error.LevelText);
    }

    [Fact]
    public void ViewModel_builds_a_tree_for_nested_structured_values()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["index"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["header"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = "0200"
                },
                ["titles"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["objectData"] = "00002"
                    }
                }
            }
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.Diagnostic"), state, null,
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        var index = Assert.Single(entry.StructuredTree);
        var header = Assert.Single(index.Children, node => node.Name == "header");
        Assert.Equal("0200", Assert.Single(header.Children).Value);
        var titles = Assert.Single(index.Children, node => node.Name == "titles");
        Assert.Equal("00002", Assert.Single(Assert.Single(titles.Children).Children).Value);
        Assert.Contains("  header", entry.Details, StringComparison.Ordinal);
        Assert.Contains("    version = 0200", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewModel_copies_selected_summary_and_details_through_clipboard_service()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Status",
            ["status"] = "Ready",
            ["TechnicalDetail"] = "path=/tmp/source"
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.Status"), state, null, static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);
        var clipboard = new FakeClipboardService();
        using var viewModel = new LogToolViewModel(service, localizer, clipboard);
        viewModel.SelectedEntry = Assert.Single(viewModel.FilteredEntries);

        await viewModel.CopySummaryCommand.ExecuteAsync();
        Assert.Contains("Ready", clipboard.Text, StringComparison.Ordinal);

        await viewModel.CopyDetailsCommand.ExecuteAsync();
        Assert.Contains("path=/tmp/source", clipboard.Text, StringComparison.Ordinal);

        await viewModel.ClearCommand.ExecuteAsync();
        Assert.True(viewModel.IsEmpty);
        logger.LogInformation("After clear");
        Assert.Single(viewModel.FilteredEntries);
    }

    [Fact]
    public void ViewModel_refreshes_localized_levels_and_filters_when_culture_changes()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        service.CreateLogger("ChapterTool.Tests").LogError("Failed");
        using var viewModel = new LogToolViewModel(service, localizer);

        Assert.Equal("Errors", Assert.Single(viewModel.FilteredEntries).LevelText);

        localizer.SetCulture("zh-CN");

        Assert.Equal("错误", Assert.Single(viewModel.FilteredEntries).LevelText);
        Assert.Contains(viewModel.FilterOptions, option => option.DisplayName == "全部级别");
    }

    [Fact]
    public void Dispose_stops_live_entry_updates()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        logger.LogWarning("Before dispose");
        var viewModel = new LogToolViewModel(service, localizer);
        Assert.Single(viewModel.FilteredEntries);

        viewModel.Dispose();
        logger.LogError("After dispose");

        Assert.Single(viewModel.FilteredEntries);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public ValueTask<string?> GetTextAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Text);

        public ValueTask SetTextAsync(string value, CancellationToken cancellationToken)
        {
            Text = value;
            return ValueTask.CompletedTask;
        }
    }
}
