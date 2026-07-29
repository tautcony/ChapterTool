using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.Workflows;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Session;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains chapter load and save behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private ChapterExportOptions CurrentExportOptions() =>
        projectionFacade.CreateExportOptions();

    /// <summary>
    /// Loads a chapter name template from a path selected by the UI.
    /// Owns file read, naming mode, status, and failure handling.
    /// </summary>
    public async ValueTask LoadChapterNameTemplateFromPathAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var previousText = ChapterNameTemplateText;
        var previousStatus = ChapterNameTemplateStatus;
        var previousMode = ChapterNameModeIndex;

        try
        {
            var text = await ChapterNameTemplateReader.ReadAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Status.TemplateLoadFailed", ("path", Path.GetFileName(path)));
                Log(LogLevel.Warning, "Log.TemplateLoadFailed", ("path", path), ("reason", "empty"));
                LogStatus(LogLevel.Warning);
                return;
            }

            ChapterNameTemplateText = text;
            ChapterNameTemplateStatus = Path.GetFileName(path);
            ChapterNameModeIndex = 2;
            SetStatus("Status.TemplateLoaded", ("name", ChapterNameTemplateStatus));
            Log("Log.TemplateLoaded", ("path", path), ("name", ChapterNameTemplateStatus));
            LogStatus();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Restore mode first: a non-template mode clears template fields in the mode setter.
            ChapterNameModeIndex = previousMode;
            ChapterNameTemplateText = previousText;
            ChapterNameTemplateStatus = previousStatus;

            SetStatus("Status.TemplateLoadFailed", ("path", Path.GetFileName(path)));
            Log(LogLevel.Warning, "Log.TemplateLoadFailed", exception.Message, ("path", path));
            LogStatus(LogLevel.Warning);
        }
    }

    private ValueTask LoadPathAsync(string path, CancellationToken cancellationToken) =>
        LoadSourceAsync(new LocalPathChapterSource(path), cancellationToken);

    private async ValueTask LoadSourceAsync(ChapterSourceDocument source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.DisplayName))
        {
            SetStatus("Status.NoSourceSelected");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.LoadingSource", ("path", source.DisplayName));
        Progress = 0.05;
        SetProgressStatus(ChapterImportProgressPhase.LoadingSource);
        var outcome = await loadSaveWorkflow.LoadAsync(source, update =>
        {
            Progress = Math.Clamp(update.Fraction ?? Progress, 0, 0.98);
            SetProgressStatus(update.Phase);
        }, cancellationToken);
        switch (outcome.State)
        {
            case LoadWorkflowState.Stale:
                return;
            case LoadWorkflowState.EmptyPath:
                SetStatus("Status.NoSourceSelected");
                LogStatus();
                NotifyStateChanged();
                return;
            case LoadWorkflowState.Failed:
                break;
            case LoadWorkflowState.Succeeded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var result = outcome.Result!;
        LogImportSummary("Load", result);
        if (outcome.State == LoadWorkflowState.Failed)
        {
            SetStatus("Status.LoadFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            ClearProgressStatus();
            Progress = 0;
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var session = outcome.Session!;
        SourcePath = source is LocalPathChapterSource local ? local.Path : source.DisplayName;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(DisplayPath));
        ApplyClipSessionUi(session, selectIndex: session.SelectedIndex);
        SetStatus("Status.LoadedChapters", ("count", Rows.Count));
        ClearProgressStatus();
        Progress = 1;
        Log("Log.StatusFromPath", ("status", StatusText), ("path", source.DisplayName));
        LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
        NotifyStateChanged();
    }

    private async ValueTask SaveAsync(string? directoryOverride, CancellationToken cancellationToken)
    {
        if (CurrentInfo is null)
        {
            return;
        }

        var directory = ResolveSaveDirectory(directoryOverride);
        var projection = CurrentOutputProjection();
        var entries = CurrentExportOptionsForProjectedInfo();
        Log("Log.SavingChapters",
            ("format", entries.Format),
            ("directory", directory ?? string.Empty),
            ("source", CurrentInfo.SourceName ?? string.Empty),
            ("chapters", projection.Info.Chapters.Count),
            ("applyExpression", ApplyExpression),
            ("expression", Expression));
        LogDiagnostics(Localizer.GetString("Operation.OutputProjection"), projection.Diagnostics);
        var result = await loadSaveWorkflow.SaveAsync(projection.Info, entries, directory, cancellationToken);
        ApplySaveStatus(result);
        LogStatus();
        LogDiagnostics(Localizer.GetString("Operation.Save"), result.Diagnostics);
        NotifyStateChanged();
    }

    private void ApplySaveStatus(ChapterExportResult result)
    {
        if (result.Success)
        {
            var saved = result.Diagnostics.LastOrDefault(static diagnostic => diagnostic.Code == ChapterDiagnosticCode.Saved);
            if (saved is not null)
            {
                SetStatus(null, saved);
                return;
            }

            SetStatus("Status.Saved");
            return;
        }

        var failure = result.Diagnostics.LastOrDefault(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Error)
            ?? result.Diagnostics.LastOrDefault();
        SetStatus("Status.SaveFailed", failure);
    }

    internal string? ResolveSaveDirectory(string? directoryOverride) =>
        ChapterSaveDirectory.Resolve(directoryOverride, SaveDirectory, CurrentPath);

    internal static string? NormalizeConfiguredDirectory(string? path) =>
        ChapterSavePath.CleanOptionalPath(path);

    private ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken) =>
        AppendSourceAsync(new LocalPathChapterSource(path), cancellationToken);

    private async ValueTask AppendSourceAsync(ChapterSourceDocument source, CancellationToken cancellationToken)
    {
        if (Workspace.ClipSession is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", source.DisplayName));
        var outcome = await loadSaveWorkflow.AppendAsync(source, cancellationToken);
        switch (outcome.State)
        {
            case AppendWorkflowState.Stale:
                return;
            case AppendWorkflowState.NoSession:
                SetStatus("Status.NoCurrentMplsGroup");
                LogStatus();
                NotifyStateChanged();
                return;
            case AppendWorkflowState.FailedLoad:
                break;
            case AppendWorkflowState.FailedTransition:
                break;
            case AppendWorkflowState.Succeeded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var result = outcome.ImportResult!;
        LogImportSummary("Append load", result);
        if (outcome.State == AppendWorkflowState.FailedLoad)
        {
            SetStatus("Status.AppendFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var transition = outcome.Transition!;
        if (outcome.State == AppendWorkflowState.FailedTransition)
        {
            SetStatus(null, diagnostic: transition.EditResult.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendEdit"), transition.EditResult.Diagnostics);
            NotifyStateChanged();
            return;
        }

        ApplyClipSessionUi(outcome.Session!, selectIndex: 0);
        SetStatus("Status.AppendedMplsSegments", ("count", result.Groups[0].Entries.Count));
        LogStatus();
        LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
        NotifyStateChanged();
    }

}
