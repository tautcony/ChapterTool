namespace ChapterTool.Wasm.Services;

/// <summary>A virtualized browser history row projected from Core history metadata.</summary>
public sealed record WasmHistoryEntry(Guid Id, Guid? ParentId, string Description, int Depth, bool IsCurrent,
    bool HasChildren, bool HasCurrentDescendant)
{
    public string DisplayDescription => $"{new string(' ', Depth * 2)}{(IsCurrent ? "● " : "  ")}{Description}";
}
