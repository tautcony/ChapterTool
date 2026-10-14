using System.Collections.ObjectModel;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.Workflows;

/// <summary>Materializes table rows from the committed workspace content.</summary>
internal sealed class WorkspaceContentRows(
    ChapterWorkspace workspace,
    IChapterTimeFormatter formatter)
{
    public ChapterSet GetCurrentChapterSet() => workspace.CurrentChapterSet ?? EmptyChapterSet();

    public void RefreshRows(
        ObservableCollection<ChapterRowViewModel> rows,
        ChapterSet? display,
        FrameRateOption frameRateOption,
        FramePresentationPolicy policy)
    {
        rows.Clear();
        if ((display ?? workspace.CurrentChapterSet) is not { } current)
        {
            return;
        }

        var document = workspace.ContentSession?.Snapshot.Document;
        var track = GetCurrentTrack(document, workspace.CurrentTrackIndex);
        for (var index = 0; index < current.Chapters.Count; index++)
        {
            rows.Add(CreateRow(current.Chapters[index], track, index, frameRateOption, policy));
        }
    }

    private static EditableChapterTrack? GetCurrentTrack(EditableChapterDocument? document, int trackIndex) =>
        document is not null && trackIndex >= 0 && trackIndex < document.Tracks.Length
            ? document.Tracks[trackIndex]
            : null;

    private ChapterRowViewModel CreateRow(
        Chapter chapter,
        EditableChapterTrack? track,
        int index,
        FrameRateOption frameRateOption,
        FramePresentationPolicy policy)
    {
        var row = new ChapterRowViewModel(chapter, formatter);
        var typedChapter = track is not null && index < track.Chapters.Length ? track.Chapters[index] : null;
        var startTicks = typedChapter?.StartTicks ?? chapter.StartTime.Ticks;
        row.SetFramePresentationInput(new FramePresentationInput(startTicks, frameRateOption.Rate, chapter.FramesInfo), policy);
        if (typedChapter is not null)
        {
            row.TrackId = track!.Id;
            row.ChapterId = typedChapter.Id;
        }

        return row;
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, FrameRateValue.Unknown, TimeSpan.Zero, []);
}
