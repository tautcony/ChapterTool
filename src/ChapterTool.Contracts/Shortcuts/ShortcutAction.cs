namespace ChapterTool.Contracts.Shortcuts;

/// <summary>Metadata for one shortcut action in the catalog.</summary>
public sealed record ShortcutAction(
    string Id,
    string NameKey,
    bool IsEditable,
    string DefaultGesture,
    IReadOnlyList<string> FixedGestures);
