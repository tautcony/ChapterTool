using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.Workflows;

/// <summary>Builds and synchronizes localized selector projections used by the shell.</summary>
internal sealed class DisplayOptionCoordinator(IAppLocalizer localizer, IFrameRateService frameRateService)
{
    private FrameRateOption? customRate;

    public void SetContentRate(FrameRateValue rate, ObservableCollection<SelectorDisplayOption> options)
    {
        customRate = rate.IsKnown && frameRateService.Options.All(option => option.Rate != rate)
            ? frameRateService.FindByRate(rate) : null;
        RefreshFrameRateDisplayOptions(options);
    }

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

    public void RebuildClipDisplayOptions(
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
        var entries = frameRateService.Options.Concat(customRate is null ? [] : new[] { customRate })
            .Select((entry, index) => new SelectorDisplayOption(
                entry.Code,
                string.Empty,
                index == 0 ? localizer.GetString("Main.AutoFrameRate") : entry.DisplayName))
            .ToArray();
        UpdateOptions(options, entries);
    }

    public void RefreshXmlLanguageDisplayOptions(ObservableCollection<SelectorDisplayOption> options) =>
        UpdateOptions(options, XmlLanguageDisplay.Options(localizer));

    public int ComboIndexFor(FrameRateOption entry) => entry.MplsFrameRateCode == -1
        ? frameRateService.Options.Count : entry.MplsFrameRateCode == 0 ? 0 : entry.IsValid ? entry.MplsFrameRateCode : -1;

    public FrameRateOption? FrameRateOptionForComboIndex(int frameRateIndex)
    {
        if (frameRateIndex == frameRateService.Options.Count)
        {
            return customRate;
        }

        return frameRateIndex switch
        {
            0 => frameRateService.Options[0],
            < 1 or 5 => null,
            _ => frameRateService.Options.FirstOrDefault(entry => entry.MplsFrameRateCode == frameRateIndex)
        };
    }

    private static void UpdateOptions(
        ObservableCollection<SelectorDisplayOption> target,
        IReadOnlyList<SelectorDisplayOption> entries)
    {
        while (target.Count > entries.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        while (target.Count < entries.Count)
        {
            target.Add(entries[target.Count]);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            target[index].UpdateFrom(entries[index]);
        }
    }

    private SelectorDisplayOption ToClipDisplayOption(ChapterImportEntry entry)
    {
        var display = ChapterImportDisplay.From(entry);
        var remarkText = display.ChapterCount > 0
            ? localizer.Format(display.ChapterCount == 1 ? "Main.ChapterCountSingular" : "Main.ChapterCount", new Dictionary<string, object?> { ["count"] = display.ChapterCount })
            : string.Empty;
        var mainText = entry.DiscTitleNumber is int titleNumber
            ? ChapterImportDisplay.FormatDvdOption(
                localizer.Format("Label.DvdTitle", new Dictionary<string, object?> { ["number"] = titleNumber }),
                entry.ChapterSet,
                entry.ReferencedMediaFiles)
            : display.MainText;
        var displayText = string.IsNullOrWhiteSpace(remarkText) ? mainText : $"{mainText}（{remarkText}）";
        return new SelectorDisplayOption(mainText, remarkText, displayText);
    }
}
