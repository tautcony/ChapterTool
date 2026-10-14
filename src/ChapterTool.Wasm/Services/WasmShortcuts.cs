using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Wasm.Services;

public static class WasmShortcuts
{
    public static bool IsSupported(string gesture) => gesture.Length == 0
        || (ShortcutGestureText.IsValid(gesture)
            && gesture is not ("Ctrl+W" or "Ctrl+T" or "Ctrl+N" or "Ctrl+Shift+T"
                or "Meta+W" or "Meta+T" or "Meta+N" or "Meta+Shift+T"));

    public static IReadOnlySet<string> InvalidActions(ShortcutSettings settings)
    {
        var invalid = ShortcutConflictValidator.FindConflicts(settings).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, gesture) in settings)
        {
            if (!IsSupported(gesture))
            {
                invalid.Add(id);
            }
        }
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["F9"] = "refresh-rows" };
        foreach (var action in ShortcutCatalog.All)
        {
            var gestures = action.FixedGestures.ToList();
            if (action.IsEditable && settings.TryGetValue(action.Id, out var gesture) && gesture.Length > 0)
            {
                gestures.Add(gesture);
                if (gesture.StartsWith("Ctrl+", StringComparison.Ordinal))
                {
                    gestures.Add("Meta+" + gesture[5..]);
                }
            }
            foreach (var binding in gestures)
            {
                if (claims.TryGetValue(binding, out var other) && other != action.Id)
                {
                    invalid.Add(action.Id);
                    invalid.Add(other);
                }
                else
                {
                    claims[binding] = action.Id;
                }
            }
        }
        return invalid;
    }

    public static ShortcutSettings Normalize(ShortcutSettings? settings)
    {
        var normalized = ShortcutSettings.Normalize(settings);
        return InvalidActions(normalized).Count == 0 ? normalized : ShortcutSettings.Normalize(null);
    }

    public static IReadOnlyDictionary<string, string> Bindings(ShortcutSettings settings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in ShortcutCatalog.All)
        {
            if (action.IsEditable && settings.TryGetValue(action.Id, out var gesture) && gesture.Length > 0)
            {
                result.TryAdd(gesture, action.Id);
                if (gesture.StartsWith("Ctrl+", StringComparison.Ordinal))
                {
                    result.TryAdd("Meta+" + gesture[5..], action.Id);
                }
            }
            foreach (var fixedGesture in action.FixedGestures)
            {
                result[fixedGesture] = action.Id;
            }
        }
        result.TryAdd("F9", "refresh-rows");
        return result;
    }

    public static string Gesture(string key, bool ctrl, bool alt, bool meta, bool shift)
    {
        var normalizedKey = key switch
        {
            "ArrowUp" => "Up",
            "ArrowDown" => "Down",
            "ArrowLeft" => "Left",
            "ArrowRight" => "Right",
            " " => "Space",
            _ => key.Length == 1 ? key.ToUpperInvariant() : key
        };
        return (ctrl ? "Ctrl+" : string.Empty) + (alt ? "Alt+" : string.Empty)
            + (meta ? "Meta+" : string.Empty) + (shift ? "Shift+" : string.Empty) + normalizedKey;
    }
}
