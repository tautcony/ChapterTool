using System.Collections.ObjectModel;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>A stable selectable node in the native hierarchical session history tree.</summary>
public sealed class HistoryEntryViewModel : ObservableViewModel
{
    private string description;
    private string title;
    private bool isCurrent;
    private bool isCurrentPath;
    private bool isExpanded;
    private bool isPreferredRedo;

    internal HistoryEntryViewModel(SessionHistoryNodeSnapshot node, string title)
    {
        Id = node.Id;
        ParentId = node.ParentId;
        PublishedAt = node.PublishedAt;
        Operation = node.Operation;
        description = node.Description;
        this.title = title;
    }

    public Guid Id { get; }

    public Guid? ParentId { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public HistoryOperationDescriptor? Operation { get; private set; }

    public ObservableCollection<HistoryEntryViewModel> Children { get; } = [];

    public string Description
    {
        get => description;
        private set => SetProperty(ref description, value);
    }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public bool HasChildren => Children.Count > 0;

    public bool IsCurrent
    {
        get => isCurrent;
        private set => SetProperty(ref isCurrent, value);
    }

    public bool IsCurrentPath
    {
        get => isCurrentPath;
        private set => SetProperty(ref isCurrentPath, value);
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool IsPreferredRedo
    {
        get => isPreferredRedo;
        private set => SetProperty(ref isPreferredRedo, value);
    }

    internal void Update(SessionHistoryNodeSnapshot node, string localizedTitle, bool current, bool currentPath, bool expanded, bool preferred)
    {
        Description = node.Description;
        Title = localizedTitle;
        ParentId = node.ParentId;
        PublishedAt = node.PublishedAt;
        Operation = node.Operation;
        IsCurrent = current;
        IsCurrentPath = currentPath;
        IsExpanded = expanded;
        IsPreferredRedo = preferred;
    }

    internal static (IReadOnlyList<HistoryEntryViewModel> Roots, IReadOnlyList<HistoryEntryViewModel> All) CreateTree(
        SessionHistorySnapshot? history,
        IReadOnlyDictionary<Guid, HistoryEntryViewModel> previous,
        ISet<Guid> expandedNodes,
        Func<SessionHistoryNodeSnapshot, string> titleProvider)
    {
        if (history is null)
        {
            return ([], []);
        }

        var snapshots = history.Nodes.ToDictionary(static node => node.Id);
        var currentPath = new HashSet<Guid>();
        var pathId = history.CursorId;
        while (snapshots.TryGetValue(pathId, out var pathNode))
        {
            currentPath.Add(pathId);
            if (pathNode.ParentId is not Guid parentId)
            {
                break;
            }
            pathId = parentId;
        }

        var projected = new Dictionary<Guid, HistoryEntryViewModel>(history.Nodes.Length);
        foreach (var node in history.Nodes.Reverse())
        {
            var hasPrevious = previous.TryGetValue(node.Id, out var existing);
            var entry = hasPrevious ? existing! : new HistoryEntryViewModel(node, titleProvider(node));
            var preferredChild = node.ParentId is Guid parent && snapshots.TryGetValue(parent, out var parentNode)
                ? parentNode.PreferredChildId
                : null;
            var isExpanded = hasPrevious
                ? existing!.IsExpanded
                : expandedNodes.Contains(node.Id) || currentPath.Contains(node.Id);
            entry.Update(node, titleProvider(node), node.Id == history.CursorId, currentPath.Contains(node.Id), isExpanded,
                preferredChild == node.Id);
            entry.Children.Clear();
            projected[node.Id] = entry;
            foreach (var childId in node.ChildIds)
            {
                if (projected.TryGetValue(childId, out var child))
                {
                    entry.Children.Add(child);
                }
            }
        }

        foreach (var entry in projected.Values)
        {
            entry.OnPropertyChanged(nameof(HasChildren));
        }

        var roots = projected.TryGetValue(history.RootId, out var root) ? new[] { root } : [];
        var all = new List<HistoryEntryViewModel>(projected.Count);
        var pending = new Stack<HistoryEntryViewModel>();
        for (var index = roots.Length - 1; index >= 0; index--)
        {
            pending.Push(roots[index]);
        }
        while (pending.TryPop(out var entry))
        {
            all.Add(entry);
            for (var index = entry.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(entry.Children[index]);
            }
        }
        return (roots, all);
    }
}
