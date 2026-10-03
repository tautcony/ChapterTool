using System.Collections.Specialized;
using ChapterTool.Avalonia.UI.Workflows;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains chapter editing and clip-selection behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private void SelectClip(int index, bool logSelection = true)
    {
        if (Workspace.ClipSession is null || index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        if (!ClipEditingCoordinator.SelectClip(index))
        {
            return;
        }
        SelectedClipIndex = Workspace.ClipSession.SelectedIndex;
        if (CurrentInfo is null)
        {
            return;
        }

        configuredFrameRate = (decimal)CurrentInfo.FramesPerSecond;
        if (logSelection)
        {
            var label = ClipOptions[index].DisplayName;
            var source = CurrentInfo.SourceName ?? string.Empty;
            var sourceType = ChapterImportFormats.DisplayName(CurrentInfo.ImportFormat);
            var fps = $"{CurrentInfo.FramesPerSecond:0.###}";
            Log($"Selected source option: index={index}, label='{label}', source='{source}', sourceType={sourceType}, chapters={CurrentInfo.Chapters.Count}, fps={fps}",
                "Edit",
                ("index", index), ("label", label), ("source", source), ("sourceType", sourceType),
                ("chapters", CurrentInfo.Chapters.Count), ("fps", fps));
        }

        selectedFrameRateOption = frameRateService.FindByValue((decimal)CurrentInfo.FramesPerSecond);
        SetSelectedFrameRateIndexSilent(ComboIndexFor(selectedFrameRateOption));
        ApplyFrameInfo(logSelection);
    }

    private async ValueTask EditCell(object? parameter, EditKind kind)
    {
        if (CurrentInfo is null || parameter is not ChapterCellEdit edit)
        {
            return;
        }

        var previous = OldCellValue(edit, kind);
        var preview = ClipEditingCoordinator.Edit(edit, kind switch
        {
            EditKind.Time => ChapterEditKind.Time,
            EditKind.Name => ChapterEditKind.Name,
            EditKind.Frame => ChapterEditKind.Frame,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });
        var outcome = await ClipEditingCoordinator.ApplyCandidateAsync(preview);
        ApplyContentOutcome(outcome, $"Edit {kind.ToString().ToLowerInvariant()}: row={edit.Index}, value='{edit.Value}', previous='{previous}'");
    }

    private string OldCellValue(ChapterCellEdit edit, EditKind kind)
    {
        var chapters = CurrentInfo?.Chapters ?? [];
        if (edit.Index < 0 || edit.Index >= chapters.Count)
        {
            return string.Empty;
        }

        var chapter = chapters[edit.Index];
        return kind switch
        {
            EditKind.Time => timeFormatter.Format(chapter.StartTime),
            EditKind.Name => chapter.Name,
            EditKind.Frame => chapter.FramesInfo,
            _ => string.Empty
        };
    }

    private void CombineSegments()
    {
        if (Workspace.ClipSession is null)
        {
            return;
        }

        var originalGroup = Workspace.ClipSession.OriginalGroup;
        var wasCombined = Workspace.ClipSession.IsCombined;
        var beforeCount = wasCombined
            ? CurrentInfo?.Chapters.Count ?? 0
            : originalGroup.Entries.Sum(static entry => entry.ChapterSet.Chapters.Count);

        var transition = ClipEditingCoordinator.ToggleCombine();
        if (!transition.Succeeded || transition.Session is null)
        {
            ApplyEdit(
                transition.EditResult,
                CombineActionText("Combine segments", originalGroup));
            return;
        }

        ApplyClipSessionUi(transition.Session, selectIndex: transition.Session.SelectedIndex);
        SetStatus("Status.Updated");

        var afterCount = CurrentInfo?.Chapters.Count ?? 0;
        if (transition.Restored)
        {
            var action = CombineActionText("Split combined segments", Workspace.ClipSession.OriginalGroup);
            Log($"{action}: chapters {beforeCount} -> {afterCount}", "Edit",
                ("action", action), ("before", beforeCount), ("after", afterCount));
        }
        else
        {
            var action = CombineActionText("Combine segments", originalGroup);
            Log($"{action}: chapters {beforeCount} -> {afterCount}", "Edit",
                ("action", action), ("before", beforeCount), ("after", afterCount));
        }

        NotifyStateChanged();
    }

    private static string CombineActionText(string verb, ChapterImportSource group)
    {
        var sourceType = ChapterImportFormats.DisplayName(group.Entries[0].ChapterSet.ImportFormat);
        return $"{verb}: entries={group.Entries.Count}, sourceType={sourceType}";
    }

    private void ApplyEdit(ChapterEditResult result, string? action = null)
    {
        var effectiveAction = action ?? "Edit chapters";
        var before = CurrentInfo?.Chapters.Count ?? 0;
        CurrentInfo = result.ChapterSet;
        ApplyFrameInfo(logResult: false);
        SetStatus(result.Diagnostics.Count == 0 ? "Status.Updated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log($"{effectiveAction}: chapters {before} -> {CurrentInfo.Chapters.Count}", "Edit",
            ("action", effectiveAction), ("before", before), ("after", CurrentInfo.Chapters.Count));
        LogDiagnostics("Edit", result.Diagnostics);
        NotifyStateChanged();
    }

    internal void ApplyEditFromPort(ChapterEditResult result, string? action = null) => ApplyEdit(result, action);

    internal void ApplyContentOutcome(TransactionOutcome outcome, string action)
    {
        if (outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? outcome.Kind.ToString();
            Log(LogLevel.Warning, $"{action} rejected: {StatusText}", "Edit", ("action", action), ("outcome", outcome.Kind.ToString()));
            NotifyStateChanged();
            return;
        }

        if (CurrentInfo is null)
        {
            return;
        }

        configuredFrameRate = CurrentInfo.FramesPerSecond > 0 ? (decimal)CurrentInfo.FramesPerSecond : null;
        SetStatus("Status.Updated");
        Log($"{action}: chapters={CurrentInfo.Chapters.Count}, outcome={outcome.Kind}", "Edit",
            ("action", action), ("chapters", CurrentInfo.Chapters.Count), ("outcome", outcome.Kind));
        RefreshRows();
        NotifyStateChanged();
    }

    internal ChapterContentPreview PrepareExpressionOperation(string expression) =>
        ClipEditingCoordinator.PrepareCandidate("Apply expression", document =>
            ClipEditingCoordinator.CandidateBuilder.ApplyExpression(document, string.IsNullOrWhiteSpace(expression) ? "t" : expression));

    internal ChapterContentPreview PrepareTemplateNamesOperation(bool autoGenerateNames, bool useTemplateNames) =>
        ClipEditingCoordinator.PrepareCandidate("Apply template names", document =>
            ClipEditingCoordinator.CandidateBuilder.ApplyOutputOptions(
                document,
                autoGenerateNames,
                useTemplateNames,
                Workspace.Projection.ChapterNameTemplateText,
                orderShift: 0,
                applyExpression: false,
                expression: "t"));

    internal ChapterContentPreview PrepareContentOptionsOperation() =>
        ClipEditingCoordinator.PrepareCandidate("Apply chapter options", document =>
            ClipEditingCoordinator.CandidateBuilder.ApplyOutputOptions(
                document,
                Workspace.Projection.AutoGenerateNames,
                Workspace.Projection.UseTemplateNames,
                Workspace.Projection.ChapterNameTemplateText,
                Workspace.Projection.OrderShift,
                Workspace.Projection.ApplyExpression,
                Workspace.Projection.Expression));

    internal ChapterContentPreview PrepareFrameRateOperation(decimal sourceFps, decimal targetFps) =>
        ClipEditingCoordinator.PrepareCandidate("Change chapter frame rate", document =>
            ClipEditingCoordinator.CandidateBuilder.ChangeFrameRate(document, sourceFps, targetFps));

    internal async ValueTask<TransactionOutcome> ApplyContentPreviewAsync(
        ChapterContentPreview preview,
        CancellationToken cancellationToken = default)
    {
        var outcome = await ClipEditingCoordinator.ApplyCandidateAsync(preview, cancellationToken);
        ApplyContentOutcome(outcome, preview.Operation);
        if ((outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
            && preview.Operation is "Apply chapter options" or "Apply expression" or "Apply template names")
        {
            Workspace.ApplyExpressionFields(
                Workspace.Projection.Expression,
                applyExpression: false,
                Workspace.Projection.ExpressionPresetId,
                Workspace.Projection.ExpressionSourceName);
            Workspace.Projection.SetAutoGenerateNames(false);
            Workspace.Projection.SetUseTemplateNames(false);
            Workspace.Projection.SetOrderShift(0);
            OnPropertyChanged(nameof(AutoGenerateNames));
            OnPropertyChanged(nameof(UseTemplateNames));
            OnPropertyChanged(nameof(OrderShift));
            OnPropertyChanged(nameof(ApplyExpression));
        }

        if ((outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
            && preview.Operation == "Change chapter frame rate"
            && CurrentInfo is { } currentInfo)
        {
            configuredFrameRate = (decimal)currentInfo.FramesPerSecond;
            selectedFrameRateOption = frameRateService.FindByValue(configuredFrameRate.Value);
            SetSelectedFrameRateIndexSilent(ComboIndexFor(selectedFrameRateOption));
            var sourceFps = preview.Before.FrameRate is { } sourceRate
                ? (decimal)sourceRate.Numerator / sourceRate.Denominator
                : 0m;
            var targetFps = preview.Candidate.FrameRate is { } targetRate
                ? (decimal)targetRate.Numerator / targetRate.Denominator
                : configuredFrameRate.Value;
            var optionName = selectedFrameRateOption.DisplayName;
            Log($"Convert to current FPS: option='{optionName}', source={sourceFps:0.###}, target={targetFps:0.###}, chapters {preview.Before.Tracks.Sum(static track => track.Chapters.Length)} -> {currentInfo.Chapters.Count}",
                "Edit",
                ("option", optionName),
                ("sourceFps", $"{sourceFps:0.###}"),
                ("targetFps", $"{targetFps:0.###}"),
                ("before", preview.Before.Tracks.Sum(static track => track.Chapters.Length)),
                ("after", currentInfo.Chapters.Count));
        }

        return outcome;
    }

    internal void CancelContentPreview(ChapterContentPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
    }

    private void ApplyFrameInfo(bool logResult = true)
    {
        if (CurrentInfo is null)
        {
            RefreshRows();
            return;
        }

        var outcome = ClipEditingCoordinator.UpdateFrames(
            CurrentInfo,
            selectedFrameRateOption,
            RoundFrames
                ? 0
                : EditingOptions.FrameDisplay == FrameDisplayMode.DecimalPlaces
                    ? EditingOptions.EffectiveFrameDecimalPlaces
                    : -1,
            FrameAccuracyTolerance,
            configuredFrameRate);
        var result = outcome.FrameResult;
        var detection = outcome.Detection;
        var appliedOption = outcome.AppliedOption;
        CurrentInfo = outcome.CurrentChapterSet;

        if (detection is not null)
        {
            selectedFrameRateOption = frameRateService.Options[0];
            SetStatus("Status.DetectedFrameRate", ("displayName", detection.Option.DisplayName), ("confidence", detection.Confidence));
        }
        else
        {
            selectedFrameRateOption = result.SelectedOption;
        }

        SetSelectedFrameRateIndexSilent(ComboIndexFor(selectedFrameRateOption));
        if (logResult)
        {
            var message = $"Frame info updated: option={appliedOption.DisplayName}, fps={result.FramesPerSecond:0.###}, round={RoundFrames}, chapters={CurrentInfo.Chapters.Count}";
            if (detection is not null)
            {
                message += $", autoDetected=true, confidence={detection.Confidence}";
            }

            Log(message, "Edit",
                ("option", appliedOption.DisplayName),
                ("fps", $"{result.FramesPerSecond:0.###}"),
                ("round", RoundFrames),
                ("chapters", CurrentInfo.Chapters.Count),
                ("autoDetected", detection is not null),
                ("confidence", detection?.Confidence));
        }
        SyncClipOptionsFromSession();
        OnPropertyChanged(nameof(RelatedMediaReferences));
        RefreshRows();
        NotifyStateChanged();
    }

    private async ValueTask ChangeFpsToSelectedOption(CancellationToken cancellationToken = default)
    {
        if (CurrentInfo is null || !selectedFrameRateOption.IsValid)
        {
            return;
        }

        var sourceFps = configuredFrameRate ?? (decimal)CurrentInfo.FramesPerSecond;
        var targetOption = selectedFrameRateOption;
        var targetFps = targetOption.Value;
        pendingContentPreview = PrepareFrameRateOperation(sourceFps, targetFps);
        OnPropertyChanged(nameof(IsContentPreviewPending));
        OnPropertyChanged(nameof(CanPreviewContentOptions));
        OnPropertyChanged(nameof(CanApplyContentPreview));
        StatusText = !pendingContentPreview.IsValid
            ? string.Join("; ", pendingContentPreview.Errors)
            : string.Join("; ", pendingContentPreview.Differences.Take(4).Select(static difference =>
                $"{difference.Field}: '{difference.Before}' → '{difference.After}'"));
        ApplyContentPreviewCommand.RaiseCanExecuteChanged();
        CancelContentPreviewCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Rebuilds bindable clip options / selection after the workspace session was already updated.
    /// </summary>
    private void ApplyClipSessionUi(ClipSession session, int selectIndex)
    {
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        foreach (var entry in session.ClipOptions)
        {
            ClipOptions.Add(entry);
        }

        if (ClipOptions.Count == 0)
        {
            Workspace.SetCurrentChapterSet(null);
            Workspace.ClearProjectionCache();
            return;
        }

        SelectClip(Math.Clamp(selectIndex, 0, ClipOptions.Count - 1), logSelection: false);
    }

    private void SyncClipOptionsFromSession()
    {
        if (Workspace.ClipSession is null)
        {
            return;
        }

        var options = Workspace.ClipSession.ClipOptions;
        for (var i = 0; i < options.Count; i++)
        {
            if (i < ClipOptions.Count)
            {
                if (!ReferenceEquals(ClipOptions[i], options[i]))
                {
                    ClipOptions[i] = options[i];
                }
            }
            else
            {
                ClipOptions.Add(options[i]);
            }
        }

        while (ClipOptions.Count > options.Count)
        {
            ClipOptions.RemoveAt(ClipOptions.Count - 1);
        }
    }

    private void OnClipOptionsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        SyncClipDisplayOptions(args);
        OnPropertyChanged(nameof(IsClipSelectionVisible));
        OnPropertyChanged(nameof(RelatedMediaReferences));
        OnPropertyChanged(nameof(SelectedClipIndex));
        OnPropertyChanged(nameof(SelectedClipDisplayOption));
        NotifyCommandStates();
    }

    private void SyncClipDisplayOptions(NotifyCollectionChangedEventArgs args)
        => displayOptionCoordinator.SyncClipDisplayOptions(args, ClipOptions, ClipDisplayOptions);

    private static int ComboIndexFor(FrameRateOption entry)
        => DisplayOptionCoordinator.ComboIndexFor(entry);

    private FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
        => displayOptionCoordinator.FrameRateOptionForComboIndex(frameRateIndex);
}
