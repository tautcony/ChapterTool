using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Workflows;

/// <summary>Builds and synchronizes localized selector projections used by the shell.</summary>
internal sealed class DisplayOptionCoordinator(IAppLocalizer localizer, IFrameRateService frameRateService)
{
    public void SyncClipDisplayOptions(
        NotifyCollectionChangedEventArgs args,
        IReadOnlyList<ChapterImportEntry> clipOptions,
        ObservableCollection<SelectorDisplayOption> displayOptions)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add when args.NewItems is not null:
            {
                var index = args.NewStartingIndex;
                foreach (ChapterImportEntry entry in args.NewItems)
                {
                    displayOptions.Insert(index++, ToClipDisplayOption(entry));
                }

                break;
            }
            case NotifyCollectionChangedAction.Remove when args.OldItems is not null:
                for (var i = 0; i < args.OldItems.Count; i++)
                {
                    displayOptions.RemoveAt(args.OldStartingIndex);
                }

                break;
            case NotifyCollectionChangedAction.Replace when args.NewItems is not null:
            {
                var index = args.NewStartingIndex;
                foreach (ChapterImportEntry entry in args.NewItems)
                {
                    displayOptions[index++] = ToClipDisplayOption(entry);
                }

                break;
            }
            case NotifyCollectionChangedAction.Move when args is { OldStartingIndex: >= 0, NewStartingIndex: >= 0 }:
                displayOptions.Move(args.OldStartingIndex, args.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
            default:
                RebuildClipDisplayOptions(clipOptions, displayOptions);
                break;
        }
    }

    public static void RebuildClipDisplayOptions(
        IReadOnlyList<ChapterImportEntry> clipOptions,
        ObservableCollection<SelectorDisplayOption> displayOptions)
    {
        displayOptions.Clear();
        foreach (var entry in clipOptions)
        {
            displayOptions.Add(ToClipDisplayOption(entry));
        }
    }

    public void RefreshChapterNameModeOptions(ObservableCollection<SelectorDisplayOption> options)
    {
        UpdateOptions(options,
        [
            new SelectorDisplayOption("keep-original", string.Empty, localizer.GetString("Main.KeepOriginalName")),
            new SelectorDisplayOption("standard-template", string.Empty, localizer.GetString("Main.StandardTemplate")),
            new SelectorDisplayOption("template-file", string.Empty, localizer.GetString("Main.TemplateFile"))
        ]);
    }

    public void RefreshFrameRateDisplayOptions(ObservableCollection<SelectorDisplayOption> options)
    {
        var entries = frameRateService.Options
            .Select((entry, index) => new SelectorDisplayOption(
                entry.Code,
                string.Empty,
                index == 0 ? localizer.GetString("Main.AutoFrameRate") : entry.DisplayName))
            .ToArray();
        UpdateOptions(options, entries);
    }

    public void RefreshXmlLanguageDisplayOptions(ObservableCollection<SelectorDisplayOption> options) =>
        UpdateOptions(options, XmlLanguageDisplay.Options(localizer));

    public static int ComboIndexFor(FrameRateOption entry) =>
        entry.LegacyMplsCode == 0 ? 0 : entry.IsValid ? entry.LegacyMplsCode : -1;

    public FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
    {
        if (frameRateIndex == 0)
        {
            return frameRateService.Options[0];
        }

        if (frameRateIndex is < 1 or 5)
        {
            return null;
        }

        return frameRateService.Options.FirstOrDefault(entry => entry.LegacyMplsCode == frameRateIndex);
    }

    private static void UpdateOptions(
        ObservableCollection<SelectorDisplayOption> target,
        IReadOnlyList<SelectorDisplayOption> entries)
    {
        if (target.Count != entries.Count)
        {
            target.Clear();
            foreach (var entry in entries)
            {
                target.Add(entry);
            }

            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            target[index].UpdateFrom(entries[index]);
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
}
