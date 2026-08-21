using System.Collections.Specialized;
using ChapterTool.Avalonia.UI.Workflows;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

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
            Log("Log.SelectedSourceOption",
                ("index", index),
                ("label", ClipOptions[index].DisplayName),
                ("source", CurrentInfo.SourceName ?? string.Empty),
                ("sourceType", ChapterImportFormats.DisplayName(CurrentInfo.ImportFormat)),
                ("chapters", CurrentInfo.Chapters.Count),
                ("fps", $"{CurrentInfo.FramesPerSecond:0.###}"));
        }

        selectedFrameRateOption = frameRateService.FindByValue((decimal)CurrentInfo.FramesPerSecond);
        SetSelectedFrameRateIndexSilent(ComboIndexFor(selectedFrameRateOption));
        ApplyFrameInfo(logSelection);
    }

    private ValueTask EditCell(object? parameter, EditKind kind)
    {
        if (CurrentInfo is null || parameter is not ChapterCellEdit edit)
        {
            return ValueTask.CompletedTask;
        }

        var result = ClipEditingCoordinator.Edit(CurrentInfo, edit, kind switch
        {
            EditKind.Time => ChapterEditKind.Time,
            EditKind.Name => ChapterEditKind.Name,
            EditKind.Frame => ChapterEditKind.Frame,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });
        ApplyEdit(result, EnglishLogText("Action.EditCell", ("kind", logContentLocalizer.GetString($"EditKind.{kind}")), ("row", edit.Index), ("value", edit.Value)));
        return ValueTask.CompletedTask;
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
                EnglishLogText(
                    "Action.CombineSegments",
                    ("entries", originalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(originalGroup.Entries[0].ChapterSet.ImportFormat))));
            return;
        }

        ApplyClipSessionUi(transition.Session, selectIndex: transition.Session.SelectedIndex);
        SetStatus("Status.Updated");

        if (transition.Restored)
        {
            Log("Log.EditChapters",
                ("action", EnglishLogText("Action.SplitCombinedSegments",
                    ("entries", Workspace.ClipSession.OriginalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(Workspace.ClipSession.OriginalGroup.Entries[0].ChapterSet.ImportFormat)))),
                ("before", beforeCount),
                ("after", CurrentInfo?.Chapters.Count ?? 0));
        }
        else
        {
            Log("Log.EditChapters",
                ("action", EnglishLogText(
                    "Action.CombineSegments",
                    ("entries", originalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(originalGroup.Entries[0].ChapterSet.ImportFormat)))),
                ("before", beforeCount),
                ("after", CurrentInfo?.Chapters.Count ?? 0));
        }

        NotifyStateChanged();
    }

    private void ApplyEdit(ChapterEditResult result, string? action = null)
    {
        var effectiveAction = action ?? EnglishLogText("Action.EditChapters");
        var before = CurrentInfo?.Chapters.Count ?? 0;
        CurrentInfo = result.ChapterSet;
        ApplyFrameInfo(logResult: false);
        SetStatus(result.Diagnostics.Count == 0 ? "Status.Updated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.EditChapters", ("action", effectiveAction), ("before", before), ("after", CurrentInfo.Chapters.Count));
        LogDiagnostics("Edit", result.Diagnostics);
        NotifyStateChanged();
    }

    internal void ApplyEditFromPort(ChapterEditResult result, string? action = null) => ApplyEdit(result, action);

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
            RoundFrames ? 0 : EditingOptions.EffectiveFrameDecimalPlaces,
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
            Log("Log.FrameInfoUpdated",
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

    private void ChangeFpsToSelectedOption()
    {
        if (CurrentInfo is null || !selectedFrameRateOption.IsValid)
        {
            return;
        }

        var sourceFps = configuredFrameRate ?? (decimal)CurrentInfo.FramesPerSecond;
        var targetOption = selectedFrameRateOption;
        var targetFps = targetOption.Value;
        var result = ChapterFpsTransformService.ChangeFps(CurrentInfo, sourceFps, targetFps);
        if (!result.Success)
        {
            SetStatus(null, diagnostic: result.Diagnostics.FirstOrDefault());
            LogDiagnostics("Change FPS", result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var beforeCount = CurrentInfo.Chapters.Count;
        CurrentInfo = result.Info;
        configuredFrameRate = targetFps;
        ApplyFrameInfo(logResult: false);
        SetStatus("Status.Updated");
        Log("Log.ChangeFps",
            ("sourceFps", $"{sourceFps:0.###}"),
            ("targetFps", $"{targetFps:0.###}"),
            ("before", beforeCount),
            ("after", result.Info.Chapters.Count));
        NotifyStateChanged();
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

    private int ComboIndexFor(FrameRateOption entry)
        => DisplayOptionCoordinator.ComboIndexFor(entry);

    private FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
        => displayOptionCoordinator.FrameRateOptionForComboIndex(frameRateIndex);
}
