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
        var trackIndex = workspace.CurrentTrackIndex;
        var track = document is not null && trackIndex >= 0 && trackIndex < document.Tracks.Length
            ? document.Tracks[trackIndex]
            : null;
        for (var index = 0; index < current.Chapters.Count; index++)
        {
            var chapter = current.Chapters[index];
            var row = new ChapterRowViewModel(chapter, formatter);
            var typedChapter = track is not null && index < track.Chapters.Length
                ? track.Chapters[index]
                : null;
            var startTicks = typedChapter?.StartTicks ?? chapter.StartTime.Ticks;
            var input = frameRateOption.ExactRate is { } exactRate
                ? FramePresentationInput.FromExactRate(startTicks, exactRate, chapter.FramesInfo)
                : FramePresentationInput.FromApproximateRate(startTicks, frameRateOption.Value, chapter.FramesInfo);
            row.SetFramePresentationInput(input, policy);
            if (typedChapter is not null)
            {
                row.TrackId = track!.Id;
                row.ChapterId = typedChapter.Id;
            }

            rows.Add(row);
        }
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}
