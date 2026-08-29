using System.Text.RegularExpressions;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.Workflows;

/// <summary>
/// Owns localized status/progress rendering and structured diagnostic logging for the main shell.
/// </summary>
internal sealed partial class StatusDiagnosticsPresenter(
    IAppLocalizer localizer,
    ILogger logger,
    IChapterTimeFormatter timeFormatter,
    Action<string> setStatusText)
{
    private static readonly IReadOnlyDictionary<string, string> OperationByMessageKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Log.LoadingSource"] = "Load",
            ["Log.StatusFromPath"] = "Load",
            ["Log.SavingChapters"] = "Save",
            ["Log.AppendingMpls"] = "Append",
            ["Log.TemplateLoaded"] = "Template",
            ["Log.TemplateLoadFailed"] = "Template",
            ["Log.EditChapters"] = "Edit",
            ["Log.ChangeFps"] = "Edit",
            ["Log.FrameInfoUpdated"] = "Edit",
            ["Log.AutoFrameRateDetection"] = "Edit",
            ["Log.SelectedSourceOption"] = "Edit",
            ["Log.CreateZones"] = "Zones",
            ["Log.OpenedPath"] = "Open",
            ["Log.RelatedMediaNotFound"] = "Open",
            ["Log.LanguageSet"] = "Settings",
            ["Log.SettingsLoaded"] = "Settings"
        };

    private LocalizedMessage? statusMessage;
    private LocalizedMessage? progressMessage;

    public void SetStatus(string? key, params (string Name, object? Value)[] arguments)
    {
        statusMessage = key is null ? null : Message(key, arguments);
        setStatusText(statusMessage is null ? string.Empty : localizer.Format(statusMessage));
    }

    public void SetStatus(string? key, ChapterDiagnostic? diagnostic, params (string Name, object? Value)[] arguments)
    {
        if (diagnostic is not null)
        {
            statusMessage = null;
            setStatusText(LocalizeDiagnostic(diagnostic));
            return;
        }

        SetStatus(key, arguments);
    }

    public void SetProgress(ChapterImportProgressPhase? phase, params (string Name, object? Value)[] arguments)
    {
        statusMessage = null;
        progressMessage = phase is null ? null : Message(ProgressStatusKey(phase.Value), arguments);
        setStatusText(progressMessage is null ? string.Empty : localizer.Format(progressMessage));
    }

    public void ClearProgress() => progressMessage = null;

    public string LocalizeDiagnostic(ChapterDiagnostic diagnostic)
    {
        var key = $"Diagnostic.{diagnostic.DisplayCode}";
        if (!localizer.TryGetString(key, out var template))
        {
            return diagnostic.Message;
        }

        var arguments = diagnostic.Arguments?.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (template.Contains("{message}", StringComparison.Ordinal) && !arguments.ContainsKey("message"))
        {
            arguments["message"] = diagnostic.Message;
        }

        return LocalizerRegex().Replace(localizer.Format(key, arguments), "[?]");
    }

    public void Log(LogLevel level, string key, string? technicalDetail = null, params (string Name, object? Value)[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var trimmedKey = key.Trim();
        var state = arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal);
        state["MessageKey"] = trimmedKey;
        if (!string.IsNullOrWhiteSpace(technicalDetail))
        {
            state["TechnicalDetail"] = technicalDetail;
        }

        // Tag the entry with its operation so the log panel can group and label entries.
        // The explicit "operation" argument (used by import/diagnostic entries) wins over
        // the key-derived fallback so localized operation names are preserved.
        var operation = arguments.FirstOrDefault(static item => item.Name is "operation" or "Operation").Value?.ToString();
        if (string.IsNullOrWhiteSpace(operation))
        {
            operation = OperationForKey(trimmedKey);
        }

        if (!string.IsNullOrWhiteSpace(operation))
        {
            state["Operation"] = operation;
        }

        logger.Log(level, new EventId(0, trimmedKey), state, null,
            static (values, _) => values.TryGetValue("MessageKey", out var value) ? value?.ToString() ?? string.Empty : string.Empty);
    }

    /// <summary>
    /// Logs an import result as a single summary entry with per-group/per-entry details
    /// folded into the structured state instead of flooding the log with one entry each.
    /// </summary>
    public void LogImportSummary(string operation, ChapterImportResult result)
    {
        var entryCount = result.Groups.Sum(static group => group.Entries.Count);
        var chapterCount = result.Groups.SelectMany(static group => group.Entries).Sum(static entry => entry.ChapterSet.Chapters.Count);
        Log(result.Success ? LogLevel.Information : LogLevel.Error, "Log.ImportSummary", null,
            ("operation", operation),
            ("result", result.Success
                ? result.IsPartial ? "completed with partial results" : "completed"
                : "failed"),
            ("success", result.Success), ("partial", result.IsPartial), ("groups", result.Groups.Count),
            ("entries", entryCount), ("chapters", chapterCount), ("diagnostics", result.Diagnostics.Count),
            ("details", ImportDetails(result)),
            ("importOverview", ImportOverview(result)));
    }

    private static string ImportOverview(ChapterImportResult result)
    {
        var lines = new List<string>();
        var index = 1;
        foreach (var entry in result.Groups.SelectMany(static group => group.Entries))
        {
            if (string.IsNullOrWhiteSpace(entry.ImportDisplayName))
            {
                continue;
            }

            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"{index++}) {entry.ImportDisplayName}");
            var chapterCount = entry.ChapterSet.Chapters.Count;
            lines.Add($"   - Chapters, {chapterCount} chapter{(chapterCount == 1 ? string.Empty : "s")}");
            if (entry.MediaTracks is { Count: > 0 })
            {
                lines.AddRange(entry.MediaTracks.Select(static track => $"   - {track.Summary}"));
                continue;
            }

            var format = ChapterImportFormats.DisplayName(entry.ChapterSet.ImportFormat);
            if (!string.IsNullOrWhiteSpace(format))
            {
                lines.Add($"   - Format, {format}");
            }
            if (entry.ChapterSet.FramesPerSecond > 0)
            {
                lines.Add($"   - FPS, {entry.ChapterSet.FramesPerSecond:0.###}");
            }
        }

        var diagnostics = result.Diagnostics
            .Select(static diagnostic => string.IsNullOrWhiteSpace(diagnostic.Message)
                ? $"- {diagnostic.Severity}"
                : $"- {diagnostic.Severity}: {diagnostic.Message}")
            .ToList();
        if (lines.Count > 0 && diagnostics.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Diagnostics:");
            lines.AddRange(diagnostics);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private Dictionary<string, object?> ImportDetails(ChapterImportResult result)
    {
        var groups = new List<object?>();
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            var entries = new List<object?>();
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var info = entry.ChapterSet;
                entries.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["entryIndex"] = entryIndex + 1,
                    ["id"] = entry.Id,
                    ["label"] = entry.DisplayName,
                    ["importDisplayName"] = entry.ImportDisplayName ?? string.Empty,
                    ["source"] = info.SourceName ?? string.Empty,
                    ["sourceType"] = ChapterImportFormats.DisplayName(info.ImportFormat),
                    ["chapters"] = info.Chapters.Count,
                    ["duration"] = timeFormatter.Format(info.Duration),
                    ["fps"] = $"{info.FramesPerSecond:0.###}",
                    ["mediaTracks"] = entry.MediaTracks?.Select(static track => new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["kind"] = track.Kind,
                        ["summary"] = track.Summary,
                        ["codec"] = track.Codec ?? string.Empty,
                        ["format"] = track.Format ?? string.Empty,
                        ["language"] = track.Language ?? string.Empty,
                        ["channels"] = track.Channels ?? string.Empty,
                        ["sampleRate"] = track.SampleRate ?? string.Empty,
                        ["aspectRatio"] = track.AspectRatio ?? string.Empty
                    }).Cast<object?>().ToList() ?? []
                });
            }

            groups.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["groupIndex"] = groupIndex + 1,
                ["sourcePath"] = group.SourcePath,
                ["defaultEntryIndex"] = group.DefaultEntryIndex,
                ["entries"] = entries
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["groups"] = groups,
            ["diagnostics"] = result.Diagnostics.Select(DiagnosticDetails).Cast<object?>().ToList()
        };
    }

    /// <summary>Maps well-known message keys to the operation that produced them.</summary>
    private static string? OperationForKey(string key) =>
        OperationByMessageKey.TryGetValue(key, out var operation) ? operation : null;

    public void LogDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var arguments = new List<(string Name, object? Value)>
            {
                ("operation", operation),
                ("severity", diagnostic.Severity),
                ("code", diagnostic.DisplayCode),
                ("location", diagnostic.Location ?? string.Empty),
                ("message", diagnostic.Message),
                ("details", diagnostic.Details ?? string.Empty)
            };
            if (diagnostic.Arguments is { Count: > 0 })
            {
                arguments.AddRange(diagnostic.Arguments
                    .Where(pair => !arguments.Any(existing => string.Equals(existing.Name, pair.Key, StringComparison.Ordinal)))
                    .Select(static pair => (pair.Key, pair.Value)));
            }

            Log(LogLevelFor(diagnostic.Severity), "Log.Diagnostic", diagnostic.Details, [.. arguments]);
        }
    }

    public void RefreshLocalizedStatus()
    {
        if (statusMessage is not null)
        {
            setStatusText(localizer.Format(statusMessage));
        }
        else if (progressMessage is not null)
        {
            setStatusText(localizer.Format(progressMessage));
        }
    }

    private static Dictionary<string, object?> DiagnosticDetails(ChapterDiagnostic diagnostic) =>
        new(StringComparer.Ordinal)
        {
            ["severity"] = diagnostic.Severity.ToString(),
            ["code"] = diagnostic.DisplayCode,
            ["message"] = diagnostic.Message,
            ["location"] = diagnostic.Location ?? string.Empty,
            ["details"] = diagnostic.Details ?? string.Empty,
            ["arguments"] = diagnostic.Arguments
        };

    public static LogLevel LogLevelFor(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => LogLevel.Error,
        DiagnosticSeverity.Warning => LogLevel.Warning,
        _ => LogLevel.Information
    };

    private static LocalizedMessage Message(string key, (string Name, object? Value)[] arguments) =>
        new(key, arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));

    private static string ProgressStatusKey(ChapterImportProgressPhase phase) => phase switch
    {
        ChapterImportProgressPhase.LoadingSource => "Status.LoadingSource",
        ChapterImportProgressPhase.ValidatingSource => "Status.LoadingSource.Validate",
        ChapterImportProgressPhase.DiscoveringTitles => "Status.LoadingSource.Discover",
        ChapterImportProgressPhase.ExportingChapters => "Status.LoadingSource.Export",
        ChapterImportProgressPhase.ParsingChapters => "Status.LoadingSource.Parse",
        _ => "Status.LoadingSource"
    };

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex LocalizerRegex();
}
