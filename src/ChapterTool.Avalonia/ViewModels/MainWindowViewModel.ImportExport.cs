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
    private ChapterExportOptions CurrentExportOptions() =>
        new(
            Format: SaveFormat,
            XmlLanguage: XmlLanguage,
            SourceFileName: currentInfo?.SourceName,
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

    private async ValueTask LoadPathAsync(string path, CancellationToken cancellationToken)
    {
        var operationId = Interlocked.Increment(ref loadOperationVersion);
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
            if (operationId != Volatile.Read(ref loadOperationVersion))
            {
                return;
            }

            Progress = Math.Clamp(update.Fraction ?? Progress, 0, 0.98);
            SetProgressStatus(update.Phase);
        });
        var result = await loadService.LoadAsync(path, progress, cancellationToken);
        if (operationId != Volatile.Read(ref loadOperationVersion))
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

        CurrentPath = path;
        DisplayPath = Path.GetFileName(path);
        currentGroup = result.Groups[0];
        splitClipGroup = null;
        combinedClipOption = null;
        IsClipCombineChecked = false;
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in currentGroup.Entries)
        {
            ClipOptions.Add(entry);
        }

        SelectClip(Math.Clamp(currentGroup.DefaultEntryIndex, 0, ClipOptions.Count - 1));
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
        var operationId = Volatile.Read(ref loadOperationVersion);
        var expectedGroup = currentGroup;
        var expectedSplitGroup = splitClipGroup;
        if (expectedGroup is null)
        {
            SetStatus("Status.NoCurrentMplsGroup");
            LogStatus();
            NotifyStateChanged();
            return;
        }

        Log("Log.AppendingMpls", ("path", path));
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (operationId != Volatile.Read(ref loadOperationVersion)
            || !ReferenceEquals(expectedGroup, currentGroup)
            || !ReferenceEquals(expectedSplitGroup, splitClipGroup))
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

        var baseGroup = expectedSplitGroup ?? expectedGroup;
        var edit = ChapterSegmentService.Append(baseGroup, result.Groups[0]);
        if (edit.Diagnostics.Count > 0)
        {
            SetStatus(null, diagnostic: edit.Diagnostics[0]);
            LogStatus();
            LogDiagnostics(Localizer.GetString("Operation.AppendEdit"), edit.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var entries = baseGroup.Entries.ToList();
        entries.AddRange(result.Groups[0].Entries);
        var appendedGroup = baseGroup with { Entries = entries };
        var combinedOption = CreateCombinedClipOption(appendedGroup, edit.ChapterSet);

        splitClipGroup = appendedGroup;
        combinedClipOption = combinedOption;
        currentGroup = appendedGroup with { Entries = [combinedOption], DefaultEntryIndex = 0 };
        IsClipCombineChecked = true;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in currentGroup.Entries)
        {
            ClipOptions.Add(entry);
        }

        currentInfo = edit.ChapterSet;
        currentInfoBelongsToSelectedClip = false;
        SelectClip(0);
        SetStatus("Status.AppendedMplsSegments", ("count", result.Groups[0].Entries.Count));
        LogStatus();
        LogDiagnostics(Localizer.GetString("Operation.AppendLoad"), result.Diagnostics);
        NotifyStateChanged();
    }

}
