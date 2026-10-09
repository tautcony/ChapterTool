using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Provides a lightweight row for the virtualized session history list.</summary>
public sealed record HistoryEntryViewModel(Guid Id, string Description, int Depth, bool IsCurrent)
{
    public string BranchPrefix => Depth > 0 ? "↳" : string.Empty;

    public string DisplayDescription => $"{new string(' ', Depth * 2)}{(IsCurrent ? "• " : string.Empty)}{Description}";

    internal static IReadOnlyList<HistoryEntryViewModel> Create(SessionHistorySnapshot? history)
    {
        if (history is null)
        {
            return [];
        }

        var nodes = history.Nodes.ToDictionary(static node => node.Id);
        var rows = new List<HistoryEntryViewModel>(nodes.Count);
        var pending = new Stack<(Guid Id, int Depth)>();
        pending.Push((history.RootId, 0));
        while (pending.TryPop(out var entry))
        {
            if (!nodes.TryGetValue(entry.Id, out var node))
            {
                continue;
            }

            rows.Add(new HistoryEntryViewModel(node.Id, node.Description, entry.Depth, node.Id == history.CursorId));
            for (var index = node.ChildIds.Length - 1; index >= 0; index--)
            {
                pending.Push((node.ChildIds[index], entry.Depth + 1));
            }
        }

        return rows;
    }
}
