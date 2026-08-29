using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
        Assert.Equal("Load", entry.Operation);
        Assert.Contains("stderr=failed", entry.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("TechnicalDetail=stderr=failed", entry.Details, StringComparison.Ordinal);
        Assert.Contains(entry.StructuredProperties, property => property is { Name: "Code", Value: "Import.Partial" });
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
        Assert.True(index.IsInitiallyExpanded);
        var header = Assert.Single(index.Children, node => node.Name == "Header");
        Assert.Equal("0200", Assert.Single(header.Children).Value);
        var titles = Assert.Single(index.Children, node => node.Name == "Titles");
        Assert.Equal("00002", Assert.Single(Assert.Single(titles.Children).Children).Value);
        Assert.Contains("  Header", entry.Details, StringComparison.Ordinal);
        Assert.Contains("    Version = 0200", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModel_formats_disc_import_summary_in_eac3to_like_overview_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.ImportSummary",
            ["operation"] = "Load",
            ["result"] = "completed",
            ["entries"] = 2,
            ["chapters"] = 10,
            ["importOverview"] = string.Join(Environment.NewLine,
                "1) 00015.mpls, 00007.m2ts, 0:23:41",
                "   - Chapters, 3 chapters",
                "   - h264/AVC, 1080p24/1.001 (16:9)",
                "   - RAW/PCM, [jpn], stereo, 48kHz",
                "   - RAW/PCM, [jpn], stereo, 48kHz",
                string.Empty,
                "2) VTS_05_0.IFO, VTS_05_1, 1:49:12",
                "   - Chapters, 7 chapters",
                "   - Format, DVD IFO",
                string.Empty,
                "Diagnostics:",
                "- Info: Loaded 12 CLPI files for 12 unique clips."),
            ["details"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["groups"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["sourcePath"] = @"C:\disc\00015.mpls",
                        ["entries"] = new object?[]
                        {
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["label"] = "00007.m2ts",
                                ["source"] = "00007",
                                ["sourceType"] = "Blu-ray MPLS",
                                ["chapters"] = 3,
                                ["duration"] = "0:23:41",
                                ["fps"] = "23.976",
                                ["mediaTracks"] = new object?[]
                                {
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["kind"] = "video",
                                        ["summary"] = "h264/AVC, 1080p24/1.001 (16:9)"
                                    },
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["kind"] = "audio",
                                        ["summary"] = "RAW/PCM, [jpn], stereo, 48kHz"
                                    },
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["kind"] = "audio",
                                        ["summary"] = "RAW/PCM, [jpn], stereo, 48kHz"
                                    }
                                }
                            }
                        }
                    },
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["sourcePath"] = @"C:\dvd\VTS_05_0.IFO",
                        ["entries"] = new object?[]
                        {
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["label"] = "VTS_05_1",
                                ["source"] = "VTS_05_1",
                                ["sourceType"] = "DVD IFO",
                                ["chapters"] = 7,
                                ["duration"] = "1:49:12",
                                ["fps"] = "29.97"
                            }
                        }
                    }
                },
                ["diagnostics"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["severity"] = "Info",
                        ["code"] = "Clpi.Available",
                        ["message"] = "Loaded 12 CLPI files for 12 unique clips."
                    }
                }
            }
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.ImportSummary"), state, null,
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        var importProperty = Assert.Single(entry.StructuredProperties, static property => property.Name == "Import Entries");
        Assert.Contains("1) 00015.mpls, 00007.m2ts, 0:23:41", importProperty.Value, StringComparison.Ordinal);
        Assert.Contains("   - Chapters, 3 chapters", importProperty.Value, StringComparison.Ordinal);
        Assert.Contains("   - h264/AVC, 1080p24/1.001 (16:9)", importProperty.Value, StringComparison.Ordinal);
        Assert.Equal(2, importProperty.Value.Split("RAW/PCM, [jpn], stereo, 48kHz", StringSplitOptions.None).Length - 1);
        Assert.Contains("2) VTS_05_0.IFO, VTS_05_1, 1:49:12", importProperty.Value, StringComparison.Ordinal);
        Assert.Contains("   - Chapters, 7 chapters", importProperty.Value, StringComparison.Ordinal);
        Assert.Contains("   - Format, DVD IFO", importProperty.Value, StringComparison.Ordinal);
        Assert.Contains("Diagnostics:", entry.Details, StringComparison.Ordinal);
        Assert.Contains("- Info: Loaded 12 CLPI files for 12 unique clips.", entry.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("Group Index", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModel_formats_bdmv_import_summary_without_duplicate_duration_suffix()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.ImportSummary",
            ["operation"] = "Load",
            ["result"] = "completed",
            ["importOverview"] = "1) 00001.mpls (1:38:41) 00002.m2ts",
            ["details"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["groups"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["sourcePath"] = @"C:\disc\DISC2",
                        ["entries"] = new object?[]
                        {
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["label"] = "00001.mpls (1:38:41) 00002.m2ts",
                                ["source"] = "00001.mpls",
                                ["sourceType"] = "Blu-ray MPLS",
                                ["chapters"] = 12,
                                ["duration"] = "1:38:41",
                                ["fps"] = "23.976",
                                ["mediaTracks"] = new object?[]
                                {
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["kind"] = "video",
                                        ["summary"] = "h264/AVC, 1080p24/1.001 (16:9)"
                                    }
                                }
                            }
                        }
                    }
                },
                ["diagnostics"] = Array.Empty<object?>()
            }
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.ImportSummary"), state, null,
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        var importProperty = Assert.Single(entry.StructuredProperties, static property => property.Name == "Import Entries");
        Assert.Contains("1) 00001.mpls (1:38:41) 00002.m2ts", importProperty.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("00002.m2ts, 1:38:41", importProperty.Value, StringComparison.Ordinal);
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

    [Fact]
    public void ViewModel_extracts_operation_from_structured_state()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.SavingChapters",
            ["format"] = "ogm",
            ["Operation"] = "Save"
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.SavingChapters"), state, null,
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("Save", entry.Operation);
        Assert.True(entry.HasOperation);
        Assert.DoesNotContain(entry.StructuredProperties, property => property.Name == "Operation");
    }

    [Fact]
    public void ViewModel_filters_entries_by_search_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["operation"] = "Load",
            ["message"] = "Import failed"
        };
        logger.Log(LogLevel.Error, new EventId(0, "Log.Diagnostic"), state, null, static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);
        logger.LogInformation("Saved 10 chapters");
        using var viewModel = new LogToolViewModel(service, localizer);
        Assert.Equal(2, viewModel.FilteredEntries.Count);

        viewModel.SearchText = "import";

        var entry = Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("Import failed", entry.Summary);

        viewModel.SearchText = "missing-keyword";
        Assert.Empty(viewModel.FilteredEntries);

        viewModel.SearchText = string.Empty;
        Assert.Equal(2, viewModel.FilteredEntries.Count);
    }

    [Fact]
    public void ViewModel_searches_nested_state_and_exception_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["message"] = "Import failed",
            ["details"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourcePath"] = "/disc/BDMV/PLAYLIST/00042.mpls"
            }
        };
        logger.Log(
            LogLevel.Error,
            new EventId(9, "Log.Diagnostic"),
            state,
            new InvalidOperationException("decoder rejected packet 17"),
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);
        using var viewModel = new LogToolViewModel(service, localizer);

        viewModel.SearchText = "00042.mpls";
        Assert.Single(viewModel.FilteredEntries);

        viewModel.SearchText = "packet 17";
        Assert.Single(viewModel.FilteredEntries);
    }

    [Fact]
    public void ViewModel_preserves_selection_when_new_entries_arrive()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        logger.LogInformation("First");
        using var viewModel = new LogToolViewModel(service, localizer);
        var first = Assert.Single(viewModel.FilteredEntries);
        viewModel.SelectedEntry = first;

        logger.LogInformation("Second");

        Assert.Equal(2, viewModel.FilteredEntries.Count);
        Assert.Same(first, viewModel.SelectedEntry);
    }

    [Fact]
    public void ViewModel_prunes_visible_entries_at_the_provider_capacity()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider(capacity: 2);
        var logger = service.CreateLogger("ChapterTool.Tests");
        logger.LogInformation("First");
        logger.LogInformation("Second");
        using var viewModel = new LogToolViewModel(service, localizer);
        viewModel.SelectedEntry = viewModel.FilteredEntries[0];

        logger.LogInformation("Third");

        Assert.Equal(["Second", "Third"], viewModel.FilteredEntries.Select(static item => item.Summary));
        Assert.Equal("Third", viewModel.SelectedEntry?.Summary);
        Assert.Equal("2 entries", viewModel.EntryCountText);
    }

    [Fact]
    public async Task ViewModel_updates_entry_count_text()
    {
        var localizer = new AppLocalizationManager("en-US");
        var service = new ApplicationLogPanelProvider();
        service.CreateLogger("ChapterTool.Tests").LogInformation("First");
        using var viewModel = new LogToolViewModel(service, localizer);

        Assert.Equal("1 entries", viewModel.EntryCountText);

        service.CreateLogger("ChapterTool.Tests").LogInformation("Second");
        Assert.Equal("2 entries", viewModel.EntryCountText);

        await viewModel.ClearCommand.ExecuteAsync();
        Assert.Equal("0 entries", viewModel.EntryCountText);
    }

    [Fact]
    public void RawText_is_complete_indented_json_and_preserves_non_ascii_text()
    {
        var localizer = new AppLocalizationManager("zh-CN");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.Diagnostic",
            ["operation"] = "Load",
            ["message"] = "正在加载源：index.bdmv",
            ["code"] = "Import.Partial",
            ["TechnicalDetail"] = "stderr=failed"
        };
        logger.Log(LogLevel.Error, new EventId(7, "Log.Diagnostic"), state, null, static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);
        var raw = Assert.Single(viewModel.FilteredEntries).RawText;

        Assert.Contains("正在加载源", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u6B63", raw, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        Assert.Equal("Error", root.GetProperty("level").GetString());
        Assert.Equal("Load", root.GetProperty("operation").GetString());
        Assert.Equal("ChapterTool.Tests", root.GetProperty("category").GetString());
        Assert.Equal(7, root.GetProperty("eventId").GetInt32());
        Assert.Equal("Log.Diagnostic", root.GetProperty("messageKey").GetString());
        Assert.Equal("正在加载源：index.bdmv", root.GetProperty("message").GetString());
        Assert.Equal("Import.Partial", root.GetProperty("structuredState").GetProperty("code").GetString());
    }

    [Fact]
    public void Localized_shell_keeps_log_content_in_english()
    {
        var localizer = new AppLocalizationManager("zh-CN");
        var service = new ApplicationLogPanelProvider();
        var logger = service.CreateLogger("ChapterTool.Tests");
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["MessageKey"] = "Log.LoadingSource",
            ["path"] = "index.bdmv",
            ["Operation"] = "Load"
        };
        logger.Log(LogLevel.Information, new EventId(0, "Log.LoadingSource"), state, null,
            static (values, _) => values["MessageKey"]?.ToString() ?? string.Empty);

        using var viewModel = new LogToolViewModel(service, localizer);

        var entry = Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("Loading source: path='index.bdmv'", entry.Summary);
        Assert.Equal("信息", entry.LevelText);
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
