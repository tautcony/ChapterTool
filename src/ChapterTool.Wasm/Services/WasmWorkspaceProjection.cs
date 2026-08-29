using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Wasm.Services;

internal static class WasmWorkspaceProjection
{
    internal static IReadOnlyList<ClipOption> BuildClipOptions(
        ChapterImportResult result,
        WasmLocalizer localizer)
    {
        var options = new List<ClipOption>();
        for (var groupIndex = 0; groupIndex < result.Groups.Count; groupIndex++)
        {
            var group = result.Groups[groupIndex];
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                var id = $"{groupIndex}:{entryIndex}:{entry.Id}";
                var display = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? $"Entry {entryIndex + 1}"
                    : entry.DisplayName;
                if (result.Groups.Count > 1)
                {
                    display = $"{Path.GetFileName(group.SourcePath)} · {display}";
                }

                options.Add(ToClipOption(entry, id, groupIndex, entryIndex, localizer, display));
            }
        }

        return options;
    }

    internal static IReadOnlyList<ClipOption> BuildClipOptionsFromSession(
        ClipSession session,
        int groupIndex,
        WasmLocalizer localizer)
    {
        if (session.IsCombined)
        {
            var combined = session.ClipOptions[0];
            return [ToClipOption(combined, $"combined:{groupIndex}", groupIndex, -1, localizer)];
        }

        return
        [
            .. session.ClipOptions
                .Select((entry, index) => ToClipOption(entry, $"{groupIndex}:{entry.Id}", groupIndex, index, localizer))
        ];
    }

    internal static ChapterRowModel ToRow(Chapter chapter, IChapterTimeFormatter formatter) =>
        new()
        {
            Number = chapter.DisplayNumber,
            TimeText = chapter.IsSeparator ? string.Empty : formatter.Format(chapter.StartTime),
            Name = chapter.Name,
            FramesInfo = chapter.FramesInfo,
            IsSeparator = chapter.IsSeparator,
            IsFrameAccurate = chapter.FrameAccuracy == FrameAccuracy.Accurate,
            IsFrameInexact = chapter.FrameAccuracy == FrameAccuracy.Inexact
        };

    internal static IReadOnlyList<DiagnosticView> ToDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
    [
        .. diagnostics.Select(static diagnostic => new DiagnosticView(
            diagnostic.Severity.ToString(),
            diagnostic.DisplayCode,
            diagnostic.Message,
            diagnostic.Details))
    ];

    internal static string? FirstError(IEnumerable<ChapterDiagnostic> diagnostics)
    {
        var chapterDiagnostics = diagnostics.ToList();
        return chapterDiagnostics.FirstOrDefault(static d => d.Severity == DiagnosticSeverity.Error)?.Message
               ?? chapterDiagnostics.FirstOrDefault()?.Message;
    }

    private static ClipOption ToClipOption(
        ChapterImportEntry entry,
        string id,
        int groupIndex,
        int entryIndex,
        WasmLocalizer localizer,
        string? prefix = null)
    {
        var display = ChapterImportDisplay.From(entry);
        var mainText = prefix ?? display.MainText;
        var displayText = display.ChapterCount > 0
            ? localizer.Format("Label.ClipOption", mainText, display.ChapterCount)
            : mainText;
        return new ClipOption(id, displayText, groupIndex, entryIndex);
    }
}
