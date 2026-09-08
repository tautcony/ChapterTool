namespace ChapterTool.Contracts.Shortcuts;

/// <summary>Provides the deterministic shortcut action catalog shared by routing, menus, and settings.</summary>
public static class ShortcutCatalog
{
    public const string SaveId = "save";
    public const string ReloadId = "reload";
    public const string LogId = "log";
    public const string PreviewId = "preview";
    public const string InsertId = "insert";
    public const string DeleteId = "delete";
    public const string LoadId = "load";
    public const string PreviousClipId = "previous-clip";
    public const string NextClipId = "next-clip";

    /// <summary>Gets all catalog actions in deterministic display and routing order.</summary>
    public static IReadOnlyList<ShortcutAction> All { get; } =
    [
        new(LoadId, "Shortcuts.Action.Load", IsEditable: true, DefaultGesture: "Ctrl+O", FixedGestures: []),
        new(SaveId, "Shortcuts.Action.Save", IsEditable: true, DefaultGesture: "Ctrl+S", FixedGestures: []),
        new(ReloadId, "Shortcuts.Action.Refresh", IsEditable: true, DefaultGesture: "Ctrl+R", FixedGestures: ["F5"]),
        new(PreviousClipId, "Shortcuts.Action.PreviousClip", IsEditable: true, DefaultGesture: "PageUp", FixedGestures: []),
        new(NextClipId, "Shortcuts.Action.NextClip", IsEditable: true, DefaultGesture: "PageDown", FixedGestures: []),
        new(PreviewId, "Shortcuts.Action.Preview", IsEditable: true, DefaultGesture: "F11", FixedGestures: []),
        new(LogId, "Shortcuts.Action.Log", IsEditable: true, DefaultGesture: "Ctrl+L", FixedGestures: []),
        new(InsertId, "Shortcuts.Action.Insert", IsEditable: false, DefaultGesture: "Insert", FixedGestures: ["Insert"]),
        new(DeleteId, "Shortcuts.Action.Delete", IsEditable: false, DefaultGesture: "Delete", FixedGestures: ["Delete"])
    ];

    public static IReadOnlyList<ShortcutAction> EditableActions { get; } =
        [.. All.Where(static action => action.IsEditable)];

    public static ShortcutAction Save => All.Single(static action => action.Id == SaveId);

    public static ShortcutAction Reload => All.Single(static action => action.Id == ReloadId);

    public static ShortcutAction Log => All.Single(static action => action.Id == LogId);

    public static ShortcutAction Preview => All.Single(static action => action.Id == PreviewId);

    public static bool TryGet(string id, out ShortcutAction action)
    {
        var match = All.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        action = match!;
        return match is not null;
    }

}
