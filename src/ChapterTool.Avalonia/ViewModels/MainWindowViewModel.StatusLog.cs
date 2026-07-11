using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void SetStatus(string? key, params (string Name, object? Value)[] arguments)
    {
        currentStatusMessage = key is null
            ? null
            : new LocalizedMessage(
                key,
                arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
        StatusText = currentStatusMessage is null ? string.Empty : Localizer.Format(currentStatusMessage);
    }

    private void SetStatus(string? key, ChapterDiagnostic? diagnostic, params (string Name, object? Value)[] arguments)
    {
        if (diagnostic is not null)
        {
            currentStatusMessage = null;
            StatusText = LocalizeDiagnostic(diagnostic);
            return;
        }

        SetStatus(key, arguments);
    }

    private void SetProgressStatus(ChapterImportProgressPhase? phase, params (string Name, object? Value)[] arguments)
    {
        currentStatusMessage = null;
        var messageKey = phase is null ? null : ProgressStatusKey(phase.Value);
        currentProgressMessage = messageKey is null
            ? null
            : new LocalizedMessage(
                messageKey,
                arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
        StatusText = currentProgressMessage is null ? string.Empty : Localizer.Format(currentProgressMessage);
    }

    private static string ProgressStatusKey(ChapterImportProgressPhase phase) => phase switch
    {
        ChapterImportProgressPhase.LoadingSource => "Status.LoadingSource",
        ChapterImportProgressPhase.ValidatingSource => "Status.LoadingSource.Validate",
        ChapterImportProgressPhase.DiscoveringTitles => "Status.LoadingSource.Discover",
        ChapterImportProgressPhase.ExportingChapters => "Status.LoadingSource.Export",
        ChapterImportProgressPhase.ParsingChapters => "Status.LoadingSource.Parse",
        _ => "Status.LoadingSource"
    };

    private string LocalizeDiagnostic(ChapterDiagnostic diagnostic)
    {
        var diagnosticKey = $"Diagnostic.{diagnostic.DisplayCode}";
        if (!Localizer.TryGetString(diagnosticKey, out var template))
        {
            return diagnostic.Message;
        }

        var arguments = diagnostic.Arguments;
        if (arguments is null && template.Contains("{message}", StringComparison.Ordinal))
        {
            arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = diagnostic.Message };
        }

        var formatted = Localizer.Format(diagnosticKey, arguments);
        // Strip any unresolved {token} placeholders that may remain when
        // the Arguments dictionary is missing keys expected by the template.
        return UnresolvedPlaceholderPattern().Replace(formatted, "[?]");
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex UnresolvedPlaceholderPattern();

    private void LogStatus(LogLevel level = LogLevel.Information) => Log(level, "Log.Status", ("status", StatusText));

    private void Log(string key, params (string Name, object? Value)[] arguments) =>
        Log(LogLevel.Information, key, technicalDetail: null, arguments);

    private void Log(LogLevel level, string key, params (string Name, object? Value)[] arguments)
    {
        Log(level, key, technicalDetail: null, arguments);
    }

    private void Log(LogLevel level, string key, string? technicalDetail, params (string Name, object? Value)[] arguments)
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

        logger.Log(
            level,
            new EventId(0, key.Trim()),
            state,
            exception: null,
            static (values, _) => values.TryGetValue("MessageKey", out var value) ? value?.ToString() ?? string.Empty : string.Empty);
    }

    private string FormatLogEntry(ApplicationLogEntry entry)
    {
        if (entry.MessageKey is null)
        {
            return entry.Message;
        }

        var message = Localizer.Format(entry.MessageKey, entry.Arguments);
        return string.IsNullOrWhiteSpace(entry.TechnicalDetail)
            ? message
            : $"{message} {entry.TechnicalDetail}";
    }

    private void RefreshLocalizedState()
    {
        RefreshChapterNameModeOptions();
        RefreshFrameRateDisplayOptions();
        RefreshXmlLanguageDisplayOptions(notify: true);

        if (string.IsNullOrEmpty(chapterNameTemplateText))
        {
            ChapterNameTemplateStatus = Localizer.GetString("Status.TemplateNotSelected");
        }

        if (currentStatusMessage is not null)
        {
            StatusText = Localizer.Format(currentStatusMessage);
            return;
        }

        if (currentProgressMessage is not null)
        {
            StatusText = Localizer.Format(currentProgressMessage);
        }
    }

    private void RefreshXmlLanguageDisplayOptions(bool notify)
    {
        var entries = XmlLanguageDisplay.Options(Localizer);
        if (xmlLanguageDisplayOptions.Count != entries.Count)
        {
            xmlLanguageDisplayOptions.Clear();
            foreach (var entry in entries)
            {
                xmlLanguageDisplayOptions.Add(entry);
            }
        }
        else
        {
            for (var index = 0; index < entries.Count; index++)
            {
                xmlLanguageDisplayOptions[index].UpdateFrom(entries[index]);
            }
        }

        if (notify)
        {
            OnPropertyChanged(nameof(XmlLanguageDisplayOptions));
            OnPropertyChanged(nameof(SelectedXmlLanguageDisplayOption));
        }
    }

    private void RefreshChapterNameModeOptions()
    {
        var entries = new[]
        {
            new SelectorDisplayOption("keep-original", string.Empty, Localizer.GetString("Main.KeepOriginalName")),
            new SelectorDisplayOption("standard-template", string.Empty, Localizer.GetString("Main.StandardTemplate")),
            new SelectorDisplayOption("template-file", string.Empty, Localizer.GetString("Main.TemplateFile"))
        };

        isRefreshingChapterNameModeOptions = true;
        try
        {
            if (ChapterNameModeOptions.Count != entries.Length)
            {
                ChapterNameModeOptions.Clear();
                foreach (var entry in entries)
                {
                    ChapterNameModeOptions.Add(entry);
                }
            }
            else
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    ChapterNameModeOptions[index].UpdateFrom(entries[index]);
                }
            }
        }
        finally
        {
            isRefreshingChapterNameModeOptions = false;
        }

        OnPropertyChanged(nameof(ChapterNameModeIndex));
    }

    private void RefreshFrameRateDisplayOptions()
    {
        var entries = frameRateService.Options
            .Select((entry, index) => new SelectorDisplayOption(
                entry.Code,
                string.Empty,
                index == 0 ? Localizer.GetString("Main.AutoFrameRate") : entry.DisplayName))
            .ToArray();

        if (FrameRateDisplayOptions.Count != entries.Length)
        {
            FrameRateDisplayOptions.Clear();
            foreach (var entry in entries)
            {
                FrameRateDisplayOptions.Add(entry);
            }
        }
        else
        {
            for (var index = 0; index < entries.Length; index++)
            {
                FrameRateDisplayOptions[index].UpdateFrom(entries[index]);
            }
        }

        OnPropertyChanged(nameof(FrameRateDisplayOptions));
    }

    private void LogImportSummary(string operation, ChapterImportResult result)
    {
        var entryCount = result.Groups.Sum(static group => group.Entries.Count);
        var chapterCount = result.Groups
            .SelectMany(static group => group.Entries)
            .Sum(static entry => entry.ChapterSet.Chapters.Count);
        Log(result.Success ? LogLevel.Information : LogLevel.Error, "Log.ImportSummary",
            ("operation", operation),
            ("success", result.Success),
            ("partial", result.IsPartial),
            ("groups", result.Groups.Count),
            ("entries", entryCount),
            ("chapters", chapterCount),
            ("diagnostics", result.Diagnostics.Count));
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            Log("Log.ImportGroup",
                ("operation", operation),
                ("groupIndex", groupIndex + 1),
                ("sourcePath", group.SourcePath),
                ("defaultEntryIndex", group.DefaultEntryIndex),
                ("entries", group.Entries.Count));
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var info = entry.ChapterSet;
                Log("Log.ImportEntry",
                    ("operation", operation),
                    ("entryIndex", entryIndex + 1),
                    ("id", entry.Id),
                    ("label", entry.DisplayName),
                    ("source", info.SourceName ?? string.Empty),
                    ("sourceType", ChapterImportFormats.DisplayName(info.ImportFormat)),
                    ("chapters", info.Chapters.Count),
                    ("duration", formatter.Format(info.Duration)),
                    ("fps", $"{info.FramesPerSecond:0.###}"));
            }
        }
    }

    private void LogDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var location = string.IsNullOrWhiteSpace(diagnostic.Location) ? string.Empty : $" location='{diagnostic.Location}'";
            var details = string.IsNullOrWhiteSpace(diagnostic.Details) ? string.Empty : $" details='{diagnostic.Details}'";
            Log(LogLevelFor(diagnostic.Severity), "Log.Diagnostic",
                diagnostic.Details,
                ("operation", operation),
                ("severity", diagnostic.Severity),
                ("code", diagnostic.DisplayCode),
                ("location", location),
                ("message", LocalizeDiagnostic(diagnostic)),
                ("details", details));
        }
    }

    private static LogLevel LogLevelFor(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => LogLevel.Error,
            DiagnosticSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };
}
