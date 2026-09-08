namespace ChapterTool.Contracts.Shortcuts;

/// <summary>
/// Persisted shortcut overrides stored as the versioned settings `shortcuts` child content.
/// </summary>
/// <remarks>
/// Each entry maps an editable catalog action id to its canonical gesture text. A present empty
/// string clears that action's row gesture. Normalization backfills every editable action, so the
/// stored mapping always has one entry per configurable action id. Unknown action ids are dropped.
/// Equality is value-based so normalized settings snapshots compare deterministically.
/// </remarks>
public sealed class ShortcutSettings : Dictionary<string, string>, IEquatable<ShortcutSettings>
{
    public ShortcutSettings()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public ShortcutSettings(IEnumerable<KeyValuePair<string, string>> bindings)
        : base(bindings, StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>Gets a shortcut overrides object with no explicit bindings.</summary>
    public static ShortcutSettings Empty { get; } = new();

    /// <summary>Gets the normalized default mapping (every editable action at its default gesture).</summary>
    public static ShortcutSettings Default { get; } = Normalize(null);

    /// <summary>Returns whether the row gesture for an action is explicitly cleared.</summary>
    public bool IsCleared(string actionId) => TryGetValue(actionId, out var gesture) && gesture.Length == 0;

    /// <summary>
    /// Normalizes persisted overrides into a canonical full mapping for the current catalog.
    /// Unknown action ids are dropped, malformed gestures fall back to the catalog default, an
    /// empty value is preserved as a cleared row, and missing editable actions are backfilled.
    /// </summary>
    public static ShortcutSettings Normalize(ShortcutSettings? settings)
    {
        var result = new ShortcutSettings();
        if (settings is not null)
        {
            foreach (var (actionId, rawGesture) in settings)
            {
                if (!ShortcutCatalog.TryGet(actionId, out var action) || !action.IsEditable)
                {
                    continue;
                }

                if (rawGesture.Length == 0)
                {
                    result[action.Id] = string.Empty;
                    continue;
                }

                var canonical = ShortcutGestureText.Normalize(rawGesture);
                if (canonical is not null)
                {
                    result[action.Id] = canonical;
                }
            }
        }

        foreach (var action in ShortcutCatalog.EditableActions)
        {
            result.TryAdd(action.Id, action.DefaultGesture);
        }

        return result;
    }

    public bool Equals(ShortcutSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Count != other.Count)
        {
            return false;
        }

        foreach (var (actionId, gesture) in this)
        {
            if (!other.TryGetValue(actionId, out var otherGesture)
                || !string.Equals(gesture, otherGesture, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is ShortcutSettings other && Equals(other);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var (actionId, gesture) in this.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(actionId);
            hash.Add(gesture);
        }

        return hash.ToHashCode();
    }
}
