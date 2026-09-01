using ChapterTool.Avalonia.UI.Workflows;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains status and application-log behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    internal void SetStatus(string? key, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetStatus(key, arguments);

    internal void SetStatus(string? key, ChapterDiagnostic? diagnostic, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetStatus(key, diagnostic, arguments);

    private void SetProgressStatus(ChapterImportProgressPhase? phase, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.SetProgress(phase, arguments);

    private void ClearProgressStatus() => statusDiagnosticsPresenter.ClearProgress();

    internal string LocalizeDiagnostic(ChapterDiagnostic diagnostic) => statusDiagnosticsPresenter.LocalizeDiagnostic(diagnostic);

    internal void Log(string message, string? operation, params (string Name, object? Value)[] arguments)
        => Log(LogLevel.Information, message, operation, arguments);

    internal void Log(LogLevel level, string message, string? operation, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.Log(level, message, operation, technicalDetail: null, arguments: arguments);

    internal void Log(LogLevel level, string message, string? operation, string technicalDetail, params (string Name, object? Value)[] arguments)
        => statusDiagnosticsPresenter.Log(level, message, operation, technicalDetail, arguments: arguments);

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

    private void LogImportDiagnostics(string operation, IReadOnlyList<ChapterDiagnostic> diagnostics)
        => statusDiagnosticsPresenter.LogDiagnostics(
            operation,
            [.. diagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)]);

    public ValueTask ReportUnexpectedUiException(Exception exception)
    {
        SetStatus("Status.UnexpectedError");
        statusDiagnosticsPresenter.Log(
            LogLevel.Error,
            $"Unexpected UI operation failure: {exception.Message}",
            technicalDetail: exception.ToString(),
            exception: exception);
        return ValueTask.CompletedTask;
    }

    internal static LogLevel LogLevelFor(DiagnosticSeverity severity) => StatusDiagnosticsPresenter.LogLevelFor(severity);
}
