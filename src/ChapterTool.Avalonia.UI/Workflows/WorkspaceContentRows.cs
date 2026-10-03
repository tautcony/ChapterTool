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

    public void RefreshRows(ObservableCollection<ChapterRowViewModel> rows)
    {
        rows.Clear();
        if (workspace.CurrentChapterSet is not { } current)
        {
            return;
        }

        foreach (var chapter in current.Chapters)
        {
            rows.Add(new ChapterRowViewModel(chapter, formatter));
        }
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}
