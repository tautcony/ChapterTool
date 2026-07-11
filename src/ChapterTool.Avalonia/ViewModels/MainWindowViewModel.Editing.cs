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
    private void SelectClip(int index)
    {
        if (workspace.ClipSession is null || index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        workspace.SelectClip(index);
        SelectedClipIndex = workspace.ClipSession.SelectedIndex;
        if (currentInfo is null)
        {
            return;
        }

        configuredFrameRate = (decimal)currentInfo.FramesPerSecond;
        Log("Log.SelectedSourceOption",
            ("index", index),
            ("label", ClipOptions[index].DisplayName),
            ("source", currentInfo.SourceName ?? string.Empty),
            ("sourceType", ChapterImportFormats.DisplayName(currentInfo.ImportFormat)),
            ("chapters", currentInfo.Chapters.Count),
            ("fps", $"{currentInfo.FramesPerSecond:0.###}"));
        selectedFrameRateOption = frameRateService.FindByValue((decimal)currentInfo.FramesPerSecond);
        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        ApplyFrameInfo();
    }

    private ValueTask EditCell(object? parameter, EditKind kind)
    {
        if (currentInfo is null || parameter is not ChapterCellEdit edit)
        {
            return ValueTask.CompletedTask;
        }

        var result = kind switch
        {
            EditKind.Time => editingService.EditTime(currentInfo, edit.Index, edit.Value),
            EditKind.Name => editingService.Rename(currentInfo, edit.Index, edit.Value),
            EditKind.Frame => editingService.EditFrame(currentInfo, edit.Index, edit.Value, (decimal)currentInfo.FramesPerSecond),
            _ => new ChapterEditResult(currentInfo, [])
        };
        ApplyEdit(result, Localizer.Format(LocalizedMessage.Create("Action.EditCell", ("kind", Localizer.GetString($"EditKind.{kind}")), ("row", edit.Index), ("value", edit.Value))));
        return ValueTask.CompletedTask;
    }

    private void CombineSegments()
    {
        if (workspace.ClipSession is null)
        {
            return;
        }

        var originalGroup = workspace.ClipSession.OriginalGroup;
        var wasCombined = workspace.ClipSession.IsCombined;
        var beforeCount = wasCombined
            ? currentInfo?.Chapters.Count ?? 0
            : originalGroup.Entries.Sum(static entry => entry.ChapterSet.Chapters.Count);

        var transition = ClipSessionTransitions.ToggleCombine(workspace.ClipSession);
        if (!transition.Succeeded || transition.Session is null)
        {
            ApplyEdit(
                transition.EditResult,
                Localizer.Format(LocalizedMessage.Create(
                    "Action.CombineSegments",
                    ("entries", originalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(originalGroup.Entries[0].ChapterSet.ImportFormat)))));
            return;
        }

        workspace.ReplaceSession(transition.Session);
        ApplyClipSessionUi(transition.Session, selectIndex: transition.Session.SelectedIndex);
        SetStatus("Status.Updated");

        if (transition.Restored)
        {
            Log("Log.EditChapters",
                ("action", Localizer.Format(LocalizedMessage.Create(
                    "Action.SplitCombinedSegments",
                    ("entries", workspace.ClipSession.OriginalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(workspace.ClipSession.OriginalGroup.Entries[0].ChapterSet.ImportFormat))))),
                ("before", beforeCount),
                ("after", currentInfo?.Chapters.Count ?? 0));
        }
        else
        {
            Log("Log.EditChapters",
                ("action", Localizer.Format(LocalizedMessage.Create(
                    "Action.CombineSegments",
                    ("entries", originalGroup.Entries.Count),
                    ("sourceType", ChapterImportFormats.DisplayName(originalGroup.Entries[0].ChapterSet.ImportFormat))))),
                ("before", beforeCount),
                ("after", currentInfo?.Chapters.Count ?? 0));
        }

        LogStatus();
        NotifyStateChanged();
    }

    private void ApplyEdit(ChapterEditResult result, string? action = null)
    {
        var effectiveAction = action ?? Localizer.GetString("Action.EditChapters");
        var before = currentInfo?.Chapters.Count ?? 0;
        currentInfo = result.ChapterSet;
        ApplyFrameInfo();
        SetStatus(result.Diagnostics.Count == 0 ? "Status.Updated" : null, diagnostic: result.Diagnostics.FirstOrDefault());
        Log("Log.EditChapters", ("action", effectiveAction), ("before", before), ("after", currentInfo.Chapters.Count));
        LogDiagnostics(effectiveAction, result.Diagnostics);
        LogStatus();
        NotifyStateChanged();
    }

    private void ApplyFrameInfo()
    {
        if (currentInfo is null)
        {
            RefreshRows();
            return;
        }

        FrameRateOption appliedOption;
        FrameRateDetectionResult? detection = null;

        if (selectedFrameRateOption.LegacyMplsCode == 0)
        {
            detection = frameRateService.DetectDetailed(currentInfo, FrameAccuracyTolerance);
            appliedOption = detection.Option;
        }
        else
        {
            appliedOption = selectedFrameRateOption;
        }

        var result = frameRateService.UpdateFrames(currentInfo, appliedOption, RoundFrames, FrameAccuracyTolerance);
        currentInfo = result.Info;
        var storedInfo = configuredFrameRate is null
            ? currentInfo
            : currentInfo with { FramesPerSecond = (double)configuredFrameRate.Value };

        if (detection is not null)
        {
            selectedFrameRateOption = frameRateService.Options[0];
            SetStatus("Status.DetectedFrameRate", ("displayName", detection.Option.DisplayName), ("confidence", detection.Confidence));
            Log("Log.AutoFrameRateDetection",
                ("entry", detection.Option.DisplayName),
                ("confidence", detection.Confidence),
                ("accurate", detection.AccurateChapterCount),
                ("evaluated", detection.EvaluatedChapterCount),
                ("deviation", $"{detection.CumulativeDeviation:0.######}"));
        }
        else
        {
            selectedFrameRateOption = result.SelectedOption;
        }

        SelectedFrameRateIndex = ComboIndexFor(selectedFrameRateOption);
        Log("Log.FrameInfoUpdated",
            ("entry", appliedOption.DisplayName),
            ("fps", $"{result.FramesPerSecond:0.###}"),
            ("round", RoundFrames),
            ("chapters", currentInfo.Chapters.Count));
        WriteBackCurrentInfo(storedInfo);
        RefreshRows();
        NotifyStateChanged();
    }

    private void ChangeFpsToSelectedOption()
    {
        if (currentInfo is null || !selectedFrameRateOption.IsValid)
        {
            return;
        }

        var sourceFps = configuredFrameRate ?? (decimal)currentInfo.FramesPerSecond;
        var targetOption = selectedFrameRateOption;
        var targetFps = targetOption.Value;
        var result = ChapterFpsTransformService.ChangeFps(currentInfo, sourceFps, targetFps);
        if (!result.Success)
        {
            SetStatus(null, diagnostic: result.Diagnostics.FirstOrDefault());
            LogDiagnostics(Localizer.GetString("Main.ChangeFps"), result.Diagnostics);
            NotifyStateChanged();
            return;
        }

        var beforeCount = currentInfo.Chapters.Count;
        currentInfo = result.Info;
        configuredFrameRate = targetFps;
        ApplyFrameInfo();
        SetStatus("Status.Updated");
        Log("Log.ChangeFps",
            ("sourceFps", $"{sourceFps:0.###}"),
            ("targetFps", $"{targetFps:0.###}"),
            ("before", beforeCount),
            ("after", result.Info.Chapters.Count));
        LogStatus();
        NotifyStateChanged();
    }

    /// <summary>
    /// Writes the working chapter set back into the typed clip session by mode
    /// (selected split entry vs combined entry).
    /// </summary>
    private void WriteBackCurrentInfo(ChapterSet info)
    {
        if (workspace.ClipSession is null)
        {
            workspace.SetCurrentChapterSet(info);
            return;
        }

        workspace.WriteBackCurrentChapterSet(info);
        SyncClipOptionsFromSession();
        OnPropertyChanged(nameof(RelatedMediaReferences));
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
            workspace.SetCurrentChapterSet(null);
            workspace.ClearProjectionCache();
            return;
        }

        SelectClip(Math.Clamp(selectIndex, 0, ClipOptions.Count - 1));
    }

    private void SyncClipOptionsFromSession()
    {
        if (workspace.ClipSession is null)
        {
            return;
        }

        var options = workspace.ClipSession.ClipOptions;
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
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.NewItems is not null)
                {
                    var index = args.NewStartingIndex;
                    foreach (ChapterImportEntry entry in args.NewItems)
                    {
                        ClipDisplayOptions.Insert(index++, ToClipDisplayOption(entry));
                    }
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                if (args.OldItems is not null)
                {
                    for (var i = 0; i < args.OldItems.Count; i++)
                    {
                        ClipDisplayOptions.RemoveAt(args.OldStartingIndex);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (args.NewItems is not null)
                {
                    var index = args.NewStartingIndex;
                    foreach (ChapterImportEntry entry in args.NewItems)
                    {
                        ClipDisplayOptions[index++] = ToClipDisplayOption(entry);
                    }
                }

                break;
            case NotifyCollectionChangedAction.Move:
                if (args is { OldStartingIndex: >= 0, NewStartingIndex: >= 0 })
                {
                    ClipDisplayOptions.Move(args.OldStartingIndex, args.NewStartingIndex);
                }

                break;
            default:
                RebuildClipDisplayOptions();
                break;
        }
    }

    private void RebuildClipDisplayOptions()
    {
        ClipDisplayOptions.Clear();
        foreach (var entry in ClipOptions)
        {
            ClipDisplayOptions.Add(ToClipDisplayOption(entry));
        }
    }

    private static SelectorDisplayOption ToClipDisplayOption(ChapterImportEntry entry)
    {
        var mainText = entry.DisplayName;
        var remarkParts = new List<string>();
        var markerIndex = entry.DisplayName.LastIndexOf("__", StringComparison.Ordinal);
        if (markerIndex > 0 && markerIndex + 2 < entry.DisplayName.Length)
        {
            mainText = entry.DisplayName[..markerIndex];
            remarkParts.Add($"{entry.DisplayName[(markerIndex + 2)..]} chapters");
        }
        else if (entry.ChapterSet.Chapters.Count > 0)
        {
            remarkParts.Add($"{entry.ChapterSet.Chapters.Count} chapters");
        }

        var remarkText = string.Join(", ", remarkParts.Where(static part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
        var displayText = string.IsNullOrWhiteSpace(remarkText) ? mainText : $"{mainText}（{remarkText}）";
        return new SelectorDisplayOption(mainText, remarkText, displayText);
    }

    private static int ComboIndexFor(FrameRateOption entry)
    {
        if (entry.LegacyMplsCode == 0)
        {
            return 0;
        }

        return entry.IsValid ? entry.LegacyMplsCode : -1;
    }

    private FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
    {
        if (frameRateIndex == 0)
        {
            return frameRateService.Options[0];
        }

        if (frameRateIndex < 1)
        {
            return null;
        }

        var legacyCode = frameRateIndex;
        if (legacyCode == 5)
        {
            return null;
        }

        return frameRateService.Options.FirstOrDefault(entry => entry.LegacyMplsCode == legacyCode);
    }
}
