namespace ChapterTool.Contracts.Shortcuts;

/// <summary>
/// Validates shortcut draft rows against the catalog and each other.
/// </summary>
public static class ShortcutConflictValidator
{
    /// <summary>Returns the catalog default row gesture for every editable action.</summary>
    public static IReadOnlyDictionary<string, string> DefaultRowGestures() =>
        ShortcutCatalog.EditableActions.ToDictionary(
            static action => action.Id,
            static action => action.DefaultGesture,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds editable action ids whose active gesture set (row value plus fixed gestures) collides
    /// with another action's active gesture set.
    /// </summary>
    /// <param name="rowGestures">
    /// Current row gestures keyed by editable action id. An empty value clears the row.
    /// </param>
    public static IReadOnlySet<string> FindConflicts(IReadOnlyDictionary<string, string> rowGestures)
    {
        // gesture -> distinct owners. The same action may own both an editable row value and a
        // fixed gesture for the same text (allowed); a shared text across two actions is a conflict.
        var byGesture = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in ShortcutCatalog.All)
        {
            var gestureTexts = new List<string>();
            if (action.IsEditable)
            {
                if (rowGestures.TryGetValue(action.Id, out var row) && !string.IsNullOrEmpty(row))
                {
                    var canonical = ShortcutGestureText.Normalize(row);
                    if (canonical is not null)
                    {
                        gestureTexts.Add(canonical);
                    }
                }
            }

            gestureTexts.AddRange(action.FixedGestures);
            foreach (var gesture in gestureTexts)
            {
                if (!byGesture.TryGetValue(gesture, out var owners))
                {
                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    byGesture[gesture] = owners;
                }

                owners.Add(action.Id);
            }
        }

        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owners in byGesture.Values)
        {
            if (owners.Count > 1)
            {
                foreach (var actionId in owners)
                {
                    conflicts.Add(actionId);
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Returns true when one proposed row gesture for an action would collide with another action's
    /// active gestures, using catalog defaults for every other editable row.
    /// </summary>
    public static bool IsConflicting(string actionId, string? proposedGesture)
    {
        var rows = new Dictionary<string, string>(DefaultRowGestures(), StringComparer.OrdinalIgnoreCase)
            {
                [actionId] = proposedGesture ?? string.Empty
            };
        var conflicts = FindConflicts(rows);
        return conflicts.Contains(actionId);
    }
}
