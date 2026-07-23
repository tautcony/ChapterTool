using ChapterTool.Avalonia.Workflows;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;

namespace ChapterTool.Avalonia.ViewModels;

/// <summary>Contains chapter load and save behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private ChapterExportOptions CurrentExportOptions() =>
        projectionFacade.CreateExportOptions();

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status.NoSourceSelected");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.LoadingSource", ("path", path));
        Progress = 0.05;
        SetProgressStatus(ChapterImportProgressPhase.LoadingSource);
        var outcome = await loadSaveWorkflow.LoadAsync(path, update =>
        {
            Progress = Math.Clamp(update.Fraction ?? Progress, 0, 0.98);
            SetProgressStatus(update.Phase);
        }, cancellationToken);
        if (outcome.State == LoadWorkflowState.Stale)
        {
            return;
        }

        if (outcome.State == LoadWorkflowState.EmptyPath)
        {
            SetStatus("Status.NoSourceSelected");
            LogStatus();
            NotifyStateChanged();
            return;
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
        SourcePath = path;
        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(DisplayPath));
        ApplyClipSessionUi(session, selectIndex: session.SelectedIndex);
        SetStatus("Status.LoadedChapters", ("count", Rows.Count));
        ClearProgressStatus();
        Progress = 1;
        Log("Log.StatusFromPath", ("status", StatusText), ("path", path));
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

    private async ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken)
    {
        if (Workspace.ClipSession is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", path));
        var outcome = await loadSaveWorkflow.AppendAsync(path, cancellationToken);
        if (outcome.State == AppendWorkflowState.Stale)
        {
            return;
        }

        if (outcome.State == AppendWorkflowState.NoSession)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
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
