using ChapterTool.Core.Session;

namespace ChapterTool.Wasm.Services;

/// <summary>Provides retained history inspection and navigation for the browser workspace.</summary>
public sealed partial class WasmWorkspace
{
    private Guid? selectedHistoryRedoBranchId;
    private Guid? selectedHistoryRedoRootId;
    private Guid? selectedHistoryRedoCursorId;

    public SessionHistorySnapshot? HistorySnapshot => session.ContentSession?.GetHistorySnapshot();

    public Guid? HistoryRootId => HistorySnapshot?.RootId;

    public Guid? CurrentHistoryId => HistorySnapshot?.CursorId;

    public IReadOnlyList<WasmHistoryRedoChoice> HistoryRedoChoices
    {
        get
        {
            var snapshot = HistorySnapshot;
            if (selectedHistoryRedoBranchId is not null
                && (snapshot?.RootId != selectedHistoryRedoRootId || snapshot?.CursorId != selectedHistoryRedoCursorId))
            {
                selectedHistoryRedoBranchId = null;
                selectedHistoryRedoRootId = null;
                selectedHistoryRedoCursorId = null;
            }
            var nodes = snapshot?.Nodes.ToDictionary(static node => node.Id);
            if (snapshot is null || nodes is null || !nodes.TryGetValue(snapshot.CursorId, out var cursor))
            {
                return [];
            }

            var preferredId = cursor.PreferredChildId ?? cursor.ChildIds.FirstOrDefault();
            return cursor.ChildIds
                .Where(nodes.ContainsKey)
                .Select(id => nodes[id])
                .Select(node => new WasmHistoryRedoChoice(node.Id,
                    WasmHistoryProjection.OperationTitle(localizer, node.Operation, node.Description), node.Description,
                    selectedHistoryRedoBranchId == node.Id || (selectedHistoryRedoBranchId is null && preferredId == node.Id)))
                .ToArray();
        }
    }

    public Guid? SelectedHistoryRedoBranchId
    {
        get
        {
            var choices = HistoryRedoChoices;
            return choices.FirstOrDefault(choice => choice.Id == selectedHistoryRedoBranchId)?.Id
                ?? choices.FirstOrDefault(static choice => choice.IsPreferred)?.Id;
        }
    }

    public bool CanRedoHistoryBranch => HistoryRedoChoices.Count > 0 && !IsBusy && !IsHistoryEnded;

    public bool SelectHistoryRedoBranch(Guid? nodeId)
    {
        if (nodeId is null || !HistoryRedoChoices.Any(choice => choice.Id == nodeId) || IsBusy || IsHistoryEnded)
        {
            return false;
        }

        selectedHistoryRedoBranchId = nodeId;
        selectedHistoryRedoRootId = HistoryRootId;
        selectedHistoryRedoCursorId = CurrentHistoryId;
        Notify();
        return true;
    }

    public async ValueTask RedoSelectedHistoryBranchAsync(CancellationToken cancellationToken = default)
    {
        var choices = HistoryRedoChoices;
        var target = choices.FirstOrDefault(choice => choice.Id == selectedHistoryRedoBranchId)?.Id
            ?? choices.FirstOrDefault(static choice => choice.IsPreferred)?.Id
            ?? choices.FirstOrDefault()?.Id;
        if (target is { } nodeId)
        {
            await NavigateHistoryAsync("navigate", nodeId, cancellationToken);
            selectedHistoryRedoBranchId = null;
            selectedHistoryRedoRootId = null;
            selectedHistoryRedoCursorId = null;
        }
    }

    public string LocalizeHistoryEntry(WasmHistoryEntry entry)
    {
        var node = HistorySnapshot?.Nodes.FirstOrDefault(candidate => candidate.Id == entry.Id);
        return node is null ? entry.Description : WasmHistoryProjection.OperationTitle(localizer, node.Operation, node.Description);
    }
}

public sealed record WasmHistoryRedoChoice(Guid Id, string Title, string Description, bool IsPreferred);
