namespace ChapterTool.Contracts.Shortcuts;

/// <summary>
/// Immutable runtime mapping between canonical gestures and catalog actions, resolved from the
/// current settings overrides plus fixed catalog gestures.
/// </summary>
public sealed class ShortcutMapping
{
    private readonly IReadOnlyDictionary<string, string> gesturesByActionId;
    private readonly IReadOnlyDictionary<string, string> actionIdsByGesture;

    private ShortcutMapping(
        IReadOnlyDictionary<string, string> gesturesByActionId,
        IReadOnlyDictionary<string, string> actionIdsByGesture)
    {
        this.gesturesByActionId = gesturesByActionId;
        this.actionIdsByGesture = actionIdsByGesture;
    }

    /// <summary>Builds the mapping used when no user overrides exist.</summary>
    public static ShortcutMapping Default { get; } = Resolve(new ShortcutSettings());

    /// <summary>
    /// Resolves the active mapping from persisted overrides. Bindings are normalized first, so each
    /// editable action id is present with its canonical row gesture, or empty when cleared.
    /// </summary>
    public static ShortcutMapping Resolve(ShortcutSettings? overrides)
    {
        var normalized = ShortcutSettings.Normalize(overrides);
        var gesturesByActionId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var claimedGestures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in ShortcutCatalog.All)
        {
            if (!action.IsEditable)
            {
                continue;
            }

            var rowGesture = normalized[action.Id];
            if (string.IsNullOrEmpty(rowGesture))
            {
                continue;
            }

            gesturesByActionId[action.Id] = rowGesture;
            claimedGestures.TryAdd(rowGesture, action.Id);
        }

        // Fixed catalog gestures (reload F5 alias, clip digits) always bind to their action.
        foreach (var action in ShortcutCatalog.All)
        {
            foreach (var fixedGesture in action.FixedGestures)
            {
                claimedGestures.TryAdd(fixedGesture, action.Id);
            }
        }

        return new ShortcutMapping(gesturesByActionId, claimedGestures);
    }

    /// <summary>Returns the active editable row gesture for an action id, or null when cleared.</summary>
    public string? RowGesture(string actionId) =>
        gesturesByActionId.GetValueOrDefault(actionId);

    /// <summary>Returns the action id assigned to a canonical gesture, or null when unassigned.</summary>
    public string? ActionForGesture(string gesture)
    {
        var canonical = ShortcutGestureText.Normalize(gesture);
        return canonical is not null && actionIdsByGesture.TryGetValue(canonical, out var actionId)
            ? actionId
            : null;
    }

    /// <summary>Returns whether a canonical gesture is claimed by any action.</summary>
    public bool ContainsGesture(string gesture) => ActionForGesture(gesture) is not null;
}
