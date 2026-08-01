using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.Workflows;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains status and application-log behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private readonly IAppLocalizer logContentLocalizer = new AppLocalizationManager("en-US");

    internal void SetStatus(string? key, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetStatus(key, arguments);

    internal void SetStatus(string? key, ChapterDiagnostic? diagnostic, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetStatus(key, diagnostic, arguments);

    private void SetProgressStatus(ChapterImportProgressPhase? phase, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetProgress(phase, arguments);

    private void ClearProgressStatus() => statusDiagnosticsPresenter.ClearProgress();

    private static string ProgressStatusKey(ChapterImportProgressPhase phase) => phase switch
    {
        ChapterImportProgressPhase.LoadingSource => "Status.LoadingSource",
        ChapterImportProgressPhase.ValidatingSource => "Status.LoadingSource.Validate",
        ChapterImportProgressPhase.DiscoveringTitles => "Status.LoadingSource.Discover",
        ChapterImportProgressPhase.ExportingChapters => "Status.LoadingSource.Export",
        ChapterImportProgressPhase.ParsingChapters => "Status.LoadingSource.Parse",
        _ => "Status.LoadingSource"
    };

    internal string LocalizeDiagnostic(ChapterDiagnostic diagnostic) => statusDiagnosticsPresenter.LocalizeDiagnostic(diagnostic);

    internal void LogStatus(LogLevel level = LogLevel.Information) => Log(level, "Log.Status", ("status", StatusText));

    internal void Log(string key, params (string Name, object? Value)[] arguments) =>
        statusDiagnosticsPresenter.Log(LogLevel.Information, key, technicalDetail: null, arguments);

    private void Log(LogLevel level, string key, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.Log(level, key, technicalDetail: null, arguments);

    private void Log(LogLevel level, string key, string? technicalDetail, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.Log(level, key, technicalDetail, arguments);

    private string FormatLogEntry(ApplicationLogEntry entry)
        => statusDiagnosticsPresenter.FormatLogEntry(entry);

    private void RefreshLocalizedState()
    {
        RefreshChapterNameModeOptions();
        RefreshFrameRateDisplayOptions();
        RefreshXmlLanguageDisplayOptions(notify: true);
        displayOptionCoordinator.RebuildClipDisplayOptions(ClipOptions, ClipDisplayOptions);
        OnPropertyChanged(nameof(ClipDisplayOptions));
        OnPropertyChanged(nameof(SelectedClipDisplayOption));

        if (string.IsNullOrEmpty(ChapterNameTemplateText))
        {
            ChapterNameTemplateStatus = Localizer.GetString("Status.TemplateNotSelected");
        }

        statusDiagnosticsPresenter.RefreshLocalizedStatus();
    }

    private void RefreshXmlLanguageDisplayOptions(bool notify)
    {
        displayOptionCoordinator.RefreshXmlLanguageDisplayOptions(xmlLanguageDisplayOptions);

        if (notify)
        {
            OnPropertyChanged(nameof(XmlLanguageDisplayOptions));
            OnPropertyChanged(nameof(SelectedXmlLanguageDisplayOption));
        }
    }

    private void RefreshChapterNameModeOptions()
    {
        isRefreshingChapterNameModeOptions = true;
        try
        {
            displayOptionCoordinator.RefreshChapterNameModeOptions(ChapterNameModeOptions);
        }
        finally
        {
            isRefreshingChapterNameModeOptions = false;
        }

        OnPropertyChanged(nameof(ChapterNameModeIndex));
    }

    private void RefreshFrameRateDisplayOptions()
    {
        displayOptionCoordinator.RefreshFrameRateDisplayOptions(FrameRateDisplayOptions);

        OnPropertyChanged(nameof(FrameRateDisplayOptions));
    }

    private void LogImportSummary(string operation, ChapterImportResult result) => statusDiagnosticsPresenter.LogImportSummary(operation, result);

    internal void LogDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
        => statusDiagnosticsPresenter.LogDiagnostics(operation, diagnostics);

    internal string EnglishLogText(string key, params (string Name, object? Value)[] arguments)
        => logContentLocalizer.Format(LocalizedMessage.Create(key, arguments));

    private void LogImportDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
        => statusDiagnosticsPresenter.LogDiagnostics(
            operation,
            [.. diagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)]);

    public ValueTask ReportUnexpectedUiException(Exception exception)
    {
        SetStatus("Status.UnexpectedError");
        Log(LogLevel.Error, "Log.UnexpectedError", exception.ToString());
        return ValueTask.CompletedTask;
    }

    internal static LogLevel LogLevelFor(DiagnosticSeverity severity) => StatusDiagnosticsPresenter.LogLevelFor(severity);
}
