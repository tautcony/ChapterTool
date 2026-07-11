using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Session;
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
    private ExportPreferenceInputs CurrentExportPreferenceInputs() =>
        new(
            Format: SaveFormat,
            XmlLanguage: XmlLanguage,
            AutoGenerateNames: AutoGenerateNames,
            UseTemplateNames: UseTemplateNames,
            ChapterNameTemplateText: ChapterNameTemplateText,
            OrderShift: OrderShift,
            ApplyExpression: ApplyExpression,
            Expression: Expression,
            ExpressionPresetId: ExpressionPresetId,
            ExpressionSourceName: ExpressionSourceName,
            TextEncoding: OutputTextEncoding,
            EmitBom: EmitBom);

    private ChapterExportOptions CurrentExportOptions() =>
        workspace.CreateExportOptions(CurrentExportPreferenceInputs());

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        var operationId = workspace.BeginLoadOperation();
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
        var progress = new ChapterImportProgressSink(update =>
        {
            if (!workspace.IsCurrentRevision(operationId))
            {
                return;
            }

            Progress = Math.Clamp(update.Fraction ?? Progress, 0, 0.98);
            SetProgressStatus(update.Phase);
        });
        var result = await loadService.LoadAsync(path, progress, cancellationToken);
        if (!workspace.IsCurrentRevision(operationId))
        {
            return;
        }

        LogImportSummary("Load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.LoadFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            currentProgressMessage = null;
            Progress = 0;
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        // Load always starts in split mode; previous combined backup/mode is discarded.
        var session = ClipSessionTransitions.FromLoad(result.Groups[0]);
        if (!workspace.TryCommitLoad(operationId, path, session))
        {
            return;
        }

        OnPropertyChanged(nameof(CurrentPath));
        OnPropertyChanged(nameof(DisplayPath));
        ApplyClipSessionUi(session, selectIndex: session.SelectedIndex);
        SetStatus("Status.LoadedChapters", ("count", Rows.Count));
        currentProgressMessage = null;
        Progress = 1;
        Log("Log.StatusFromPath", ("status", StatusText), ("path", path));
        LogDiagnostics(Localizer.GetString("Operation.Load"), result.Diagnostics);
        NotifyStateChanged();
    }

    private async ValueTask SaveAsync(string? directoryOverride, CancellationToken cancellationToken)
    {
        if (currentInfo is null)
        {
            return;
        }

        var directory = ResolveSaveDirectory(directoryOverride);
        var projection = CurrentOutputProjection();
        var entries = CurrentExportOptionsForProjectedInfo();
        Log("Log.SavingChapters",
            ("format", entries.Format),
            ("directory", directory ?? string.Empty),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("chapters", projection.Info.Chapters.Count),
            ("applyExpression", ApplyExpression),
            ("expression", Expression));
        LogDiagnostics(Localizer.GetString("Operation.OutputProjection"), projection.Diagnostics);
        var result = await saveService.SaveAsync(projection.Info, entries, directory, cancellationToken, CurrentPath);
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

    private static string? NormalizeConfiguredDirectory(string? path) =>
        ChapterSavePath.CleanOptionalPath(path);

    private async ValueTask AppendMplsAsync(string path, CancellationToken cancellationToken)
    {
        var operationId = workspace.CaptureRevision();
        // Session-token identity: discard late append if load/combine/restore replaced the session.
        var expectedSession = workspace.ClipSession;
        var expectedSessionId = expectedSession?.SessionId;
        if (expectedSession is null || expectedSessionId is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", path));
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (!workspace.IsCurrentRevision(operationId)
            || workspace.ClipSession?.SessionId != expectedSessionId)
        {
            return;
        }

        LogImportSummary("Append load", result);
        if (!result.Success || result.Groups.Count == 0)
        {
            SetStatus("Status.AppendFailed", diagnostic: result.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        // Use the live session snapshot only when identity still matches (select/write-back may have updated entries).
        var sessionForAppend = workspace.ClipSession ?? expectedSession;
        var transition = ClipSessionTransitions.Append(sessionForAppend, result.Groups[0]);
        if (!transition.Succeeded || transition.Session is null)
        {
            SetStatus(null, diagnostic: transition.EditResult.Diagnostics.FirstOrDefault());
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendEdit"), transition.EditResult.Diagnostics);
            NotifyStateChanged();
            return;
        }

        if (!workspace.TryCommitAppend(operationId, expectedSessionId.Value, transition.Session))
        {
            return;
        }

        ApplyClipSessionUi(transition.Session, selectIndex: 0);
        SetStatus("Status.AppendedMplsSegments", ("count", result.Groups[0].Entries.Count));
        LogStatus();
        LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
        NotifyStateChanged();
    }

}
