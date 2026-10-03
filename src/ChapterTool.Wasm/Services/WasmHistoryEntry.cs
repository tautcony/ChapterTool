namespace ChapterTool.Wasm.Services;

/// <summary>A virtualized browser history row projected from Core history metadata.</summary>
public sealed record WasmHistoryEntry(Guid Id, string Description, int Depth, bool IsCurrent)
{
    public string DisplayDescription => $"{new string(' ', Depth * 2)}{(IsCurrent ? "• " : string.Empty)}{Description}";
}
