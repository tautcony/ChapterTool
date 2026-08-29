using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class ImportSummaryFormatterTests
{
    [Fact]
    public void Formats_disc_entries_with_tracks_and_diagnostics()
    {
        var entry = CreateEntry(
            new Dictionary<string, object?>(StringComparer.Ordinal)
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
                                ["sourceType"] = "Blu-ray MPLS",
                                ["chapters"] = 3,
                                ["duration"] = "0:23:41",
                                ["fps"] = "23.976",
                                ["mediaTracks"] = new object?[]
                                {
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["summary"] = "h264/AVC, 1080p24/1.001 (16:9)"
                                    }
                                }
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
                        ["message"] = "Loaded CLPI files."
                    }
                }
            });

        var formatted = ImportSummaryFormatter.Format(entry);

        Assert.Contains("1) 00015.mpls, 00007.m2ts, 0:23:41", formatted, StringComparison.Ordinal);
        Assert.Contains("   - Chapters, 3 chapters", formatted, StringComparison.Ordinal);
        Assert.Contains("   - h264/AVC, 1080p24/1.001 (16:9)", formatted, StringComparison.Ordinal);
        Assert.Contains($"Diagnostics:{Environment.NewLine}- Info: Loaded CLPI files.", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_duplicate_duration_already_present_in_label()
    {
        var entry = CreateEntry(
            new Dictionary<string, object?>(StringComparer.Ordinal)
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
                                ["sourceType"] = "Blu-ray MPLS",
                                ["duration"] = "1:38:41"
                            }
                        }
                    }
                }
            });

        var formatted = ImportSummaryFormatter.Format(entry);

        Assert.StartsWith("1) 00001.mpls (1:38:41) 00002.m2ts", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("00002.m2ts, 1:38:41", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_empty_for_non_import_summary_or_without_disc_entries()
    {
        var nonImport = new ApplicationLogEntry(DateTimeOffset.UnixEpoch, LogLevel.Information, "message", "Log.Status");
        Assert.Empty(ImportSummaryFormatter.Format(nonImport));

        var entry = CreateEntry(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["groups"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["sourcePath"] = @"C:\media\movie.mkv",
                        ["entries"] = new object?[]
                        {
                            new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["label"] = "movie.mkv",
                                ["sourceType"] = "Matroska"
                            }
                        }
                    }
                }
            });

        Assert.Empty(ImportSummaryFormatter.Format(entry));
    }

    private static ApplicationLogEntry CreateEntry(IReadOnlyDictionary<string, object?> details) =>
        new(
            DateTimeOffset.UnixEpoch,
            LogLevel.Information,
            "Import summary",
            "Log.ImportSummary",
            StructuredState: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["details"] = details
            });
}
