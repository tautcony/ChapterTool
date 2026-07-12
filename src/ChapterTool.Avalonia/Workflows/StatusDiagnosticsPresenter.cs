using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Workflows;

/// <summary>
/// Owns localized status/progress rendering and structured diagnostic logging for the main shell.
/// </summary>
internal sealed class StatusDiagnosticsPresenter(
    IAppLocalizer localizer,
    ILogger logger,
    Action<string> setStatusText)
{
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

        var arguments = diagnostic.Arguments;
        if (arguments is null && template.Contains("{message}", StringComparison.Ordinal))
        {
            arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = diagnostic.Message };
        }

        return Regex.Replace(localizer.Format(key, arguments), @"\{[^}]+\}", "[?]");
    }

    public void Log(LogLevel level, string key, string? technicalDetail = null, params (string Name, object? Value)[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var state = arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal);
        state["MessageKey"] = key.Trim();
        if (!string.IsNullOrWhiteSpace(technicalDetail))
        {
            state["TechnicalDetail"] = technicalDetail;
        }

        logger.Log(level, new EventId(0, key.Trim()), state, null,
            static (values, _) => values.TryGetValue("MessageKey", out var value) ? value?.ToString() ?? string.Empty : string.Empty);
    }

    public void LogImportSummary(string operation, ChapterImportResult result)
    {
        var entryCount = result.Groups.Sum(static group => group.Entries.Count);
        var chapterCount = result.Groups.SelectMany(static group => group.Entries).Sum(static entry => entry.ChapterSet.Chapters.Count);
        Log(result.Success ? LogLevel.Information : LogLevel.Error, "Log.ImportSummary", null,
            ("operation", operation), ("success", result.Success), ("partial", result.IsPartial), ("groups", result.Groups.Count),
            ("entries", entryCount), ("chapters", chapterCount), ("diagnostics", result.Diagnostics.Count));
    }

    public void LogDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Log(LogLevelFor(diagnostic.Severity), "Log.Diagnostic", diagnostic.Details,
                ("operation", operation), ("severity", diagnostic.Severity), ("code", diagnostic.DisplayCode),
                ("location", diagnostic.Location ?? string.Empty), ("message", LocalizeDiagnostic(diagnostic)), ("details", diagnostic.Details ?? string.Empty));
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

    public string FormatLogEntry(ApplicationLogEntry entry)
    {
        if (entry.MessageKey is null)
        {
            return entry.Message;
        }

        var message = localizer.Format(entry.MessageKey, entry.Arguments);
        return string.IsNullOrWhiteSpace(entry.TechnicalDetail) ? message : $"{message} {entry.TechnicalDetail}";
    }

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
}
