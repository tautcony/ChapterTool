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
    private void SelectClip(int index)
    {
        if (index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        SelectedClipIndex = index;
        currentInfo = ClipOptions[index].ChapterSet;
        configuredFrameRate = (decimal)currentInfo.FramesPerSecond;
        currentInfoBelongsToSelectedClip = !IsClipCombineChecked;
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
        if (currentGroup is null)
        {
            return;
        }

        if (IsClipCombineChecked)
        {
            RestoreSplitClips();
            return;
        }

        var groupToCombine = splitClipGroup ?? currentGroup;
        var result = ChapterSegmentService.Combine(groupToCombine);
        if (result.Diagnostics.Count > 0)
        {
            ApplyEdit(result, Localizer.Format(LocalizedMessage.Create("Action.CombineSegments", ("entries", groupToCombine.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(groupToCombine.Entries[0].ChapterSet.ImportFormat)))));
            return;
        }

        splitClipGroup = groupToCombine;
        combinedClipOption = CreateCombinedClipOption(groupToCombine, result.ChapterSet);
        currentGroup = groupToCombine with { Entries = [combinedClipOption], DefaultEntryIndex = 0 };
        IsClipCombineChecked = true;
        currentInfo = result.ChapterSet;
        currentInfoBelongsToSelectedClip = false;
        SelectedClipIndex = -1;
        ClipOptions.Clear();
        ClipOptions.Add(combinedClipOption);
        SelectClip(0);
        SetStatus("Status.Updated");
        Log("Log.EditChapters",
            ("action", Localizer.Format(LocalizedMessage.Create("Action.CombineSegments", ("entries", groupToCombine.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(groupToCombine.Entries[0].ChapterSet.ImportFormat))))),
            ("before", groupToCombine.Entries.Sum(static entry => entry.ChapterSet.Chapters.Count)),
            ("after", currentInfo?.Chapters.Count ?? 0));
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
        if (currentInfoBelongsToSelectedClip)
        {
            UpdateCurrentClipOption(storedInfo);
        }
        else if (IsClipCombineChecked)
        {
            UpdateCombinedClipOption(storedInfo);
        }
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

    private void UpdateCurrentClipOption(ChapterSet info)
    {
        if (currentGroup is null)
        {
            return;
        }

        var index = SelectedClipIndex;
        if (index < 0 || index >= ClipOptions.Count)
        {
            return;
        }

        var entries = currentGroup.Entries.ToList();
        if (index >= entries.Count)
        {
            return;
        }

        var updatedOption = entries[index] with { ChapterSet = info };
        entries[index] = updatedOption;
        ClipOptions[index] = updatedOption;
        currentGroup = currentGroup with { Entries = entries };

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void UpdateCombinedClipOption(ChapterSet info)
    {
        if (currentGroup is null || !IsClipCombineChecked)
        {
            return;
        }

        var entry = combinedClipOption ?? ClipOptions.FirstOrDefault();
        if (entry is null)
        {
            return;
        }

        combinedClipOption = entry with { ChapterSet = info };
        currentGroup = currentGroup with { Entries = [combinedClipOption] };
        if (ClipOptions.Count == 1)
        {
            ClipOptions[0] = combinedClipOption;
        }

        OnPropertyChanged(nameof(RelatedMediaReferences));
    }

    private void RestoreSplitClips()
    {
        if (splitClipGroup is null)
        {
            return;
        }

        var combinedChapterCount = combinedClipOption?.ChapterSet.Chapters.Count ?? currentInfo?.Chapters.Count ?? 0;
        currentGroup = splitClipGroup;
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
        SetStatus("Status.Updated");
        Log("Log.EditChapters",
            ("action", Localizer.Format(LocalizedMessage.Create("Action.SplitCombinedSegments", ("entries", currentGroup.Entries.Count), ("sourceType", ChapterImportFormats.DisplayName(currentGroup.Entries[0].ChapterSet.ImportFormat))))),
            ("before", combinedChapterCount),
            ("after", currentInfo?.Chapters.Count ?? 0));
        LogStatus();
        NotifyStateChanged();
    }

    private static ChapterImportEntry CreateCombinedClipOption(ChapterImportSource sourceGroup, ChapterSet combinedInfo)
    {
        var mediaReferences = sourceGroup.Entries
            .SelectMany(static entry => entry.ReferencedMediaFiles ?? [])
            .Distinct()
            .ToArray();
        return new ChapterImportEntry(
            "combined",
            $"{combinedInfo.Title}__{combinedInfo.Chapters.Count}",
            combinedInfo,
            CanCombine: true,
            ReferencedMediaFiles: mediaReferences);
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
