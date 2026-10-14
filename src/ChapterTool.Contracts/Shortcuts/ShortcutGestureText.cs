using System.Text;

namespace ChapterTool.Contracts.Shortcuts;

public enum ShortcutGestureStatus
{
    /// <summary>Empty text that intentionally clears the shortcut.</summary>
    Cleared,

    /// <summary>Text that parses to a canonical gesture.</summary>
    Valid,

    /// <summary>Text that cannot be tokenized into known modifiers and a known key.</summary>
    Malformed,

    /// <summary>Text whose key requires a modifier before it can be assigned.</summary>
    Reserved
}

/// <summary>
/// Parses, validates, and formats canonical shortcut gesture text.
/// </summary>
/// <remarks>
/// Grammar: zero or more modifier tokens (Ctrl, Alt, Shift, each at most once) followed by one
/// key token, joined by '+'. Canonical modifiers appear in Ctrl, Alt, Shift order. The canonical
/// key token is an uppercase letter, a digit, an F-key (F1-F24), or a named navigation/editing key.
/// </remarks>
public static class ShortcutGestureText
{
    /// <summary>Modifier tokens in canonical order.</summary>
    private static readonly IReadOnlyList<string> Modifiers = ["Ctrl", "Alt", "Meta", "Shift"];

    /// <summary>Named non-function keys that may appear in a gesture.</summary>
    private static readonly IReadOnlySet<string> NamedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "Insert",
        "Delete",
        "Home",
        "End",
        "PageUp",
        "PageDown",
        "Up",
        "Down",
        "Left",
        "Right",
        "Space",
        "Tab",
        "Enter",
        "Escape",
        "Backspace"
    };

    /// <summary>
    /// Keys that require at least one modifier before they can be assigned as a shortcut.
    /// Bare typing keys and editing/navigation keys keep their native meaning without a modifier.
    /// </summary>
    private static readonly IReadOnlySet<string> ModifierRequiredKeys = CreateModifierRequiredKeys();

    /// <summary>Classifies gesture text for draft-row validation and persistence.</summary>
    public static ShortcutGestureStatus Validate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ShortcutGestureStatus.Cleared;
        }

        var tokens = SplitTokens(text);
        if (tokens is null)
        {
            return ShortcutGestureStatus.Malformed;
        }

        var result = TryClassify(tokens, out _, out _);
        return result switch
        {
            ClassifyResult.Ok => ShortcutGestureStatus.Valid,
            ClassifyResult.Reserved => ShortcutGestureStatus.Reserved,
            _ => ShortcutGestureStatus.Malformed
        };
    }

    public static bool IsValid(string? gesture) => Validate(gesture) == ShortcutGestureStatus.Valid;

    /// <summary>Returns the canonical form of gesture text, or null when it cannot be normalized.</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var tokens = SplitTokens(text);
        if (tokens is null || TryClassify(tokens, out var modifiers, out var canonicalKey) != ClassifyResult.Ok)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (index > 0)
            {
                builder.Append('+');
            }

            builder.Append(modifiers[index]);
        }

        if (modifiers.Count > 0)
        {
            builder.Append('+');
        }

        builder.Append(canonicalKey);
        return builder.ToString();
    }

    /// <summary>Returns the canonical key token of a gesture, or null when malformed or empty.</summary>
    public static string? KeyOf(string? gesture)
    {
        var canonical = Normalize(gesture);
        return canonical?[(canonical.LastIndexOf('+') + 1)..];
    }

    /// <summary>Returns true when a gesture carries at least one modifier.</summary>
    public static bool HasModifier(string? gesture)
    {
        var canonical = Normalize(gesture);
        return canonical is not null && canonical.Contains('+');
    }

    /// <summary>Formats a canonical gesture for the platform-neutral shortcut display.</summary>
    public static string FormatForDisplay(string? gesture)
    {
        var canonical = Normalize(gesture);
        if (canonical is null)
        {
            return gesture?.Trim() switch
            {
                "Delete" => "⌦",
                "Insert" => "⎀",
                "Up" => "↑",
                "Down" => "↓",
                "Left" => "←",
                "Right" => "→",
                "PageUp" => "⇞",
                "PageDown" => "⇟",
                _ => gesture ?? string.Empty
            };
        }

        var tokens = canonical.Split('+');
        for (var index = 0; index < tokens.Length - 1; index++)
        {
            tokens[index] = tokens[index] switch
            {
                "Ctrl" => "⌃",
                "Alt" => "⌥",
                "Shift" => "⇧",
                "Meta" => "⌘",
                _ => tokens[index]
            };
        }

        tokens[^1] = tokens[^1] switch
        {
            "Up" => "↑",
            "Down" => "↓",
            "Left" => "←",
            "Right" => "→",
            "PageUp" => "⇞",
            "PageDown" => "⇟",
            "Enter" => "↵",
            "Escape" => "⎋",
            "Backspace" => "⌫",
            "Delete" => "⌦",
            "Insert" => "⎀",
            "Space" => "␠",
            _ => tokens[^1]
        };

        return string.Join('+', tokens);
    }

    private static string[]? SplitTokens(string text)
    {
        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens.Length > Modifiers.Count + 1)
        {
            return null;
        }

        if (tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tokens.Length)
        {
            return null;
        }

        return tokens;
    }

    private static ClassifyResult TryClassify(
        IReadOnlyList<string> tokens,
        out IReadOnlyList<string> modifiers,
        out string canonicalKey)
    {
        modifiers = [];
        canonicalKey = string.Empty;

        var keyToken = tokens[^1];
        var keyPart = CanonicalKey(keyToken);
        if (keyPart is null)
        {
            return ClassifyResult.Malformed;
        }

        var modifierTokens = tokens.SkipLast(1).ToArray();
        var collected = new List<string>(Modifiers.Count);
        foreach (var allowed in Modifiers)
        {
            if (modifierTokens.Contains(allowed, StringComparer.OrdinalIgnoreCase)
                || (allowed == "Meta" && (modifierTokens.Contains("Cmd", StringComparer.OrdinalIgnoreCase)
                    || modifierTokens.Contains("Command", StringComparer.OrdinalIgnoreCase))))
            {
                collected.Add(allowed);
            }
        }

        if (collected.Count != modifierTokens.Length)
        {
            return ClassifyResult.Malformed;
        }

        if (collected.Count == 0 && ModifierRequiredKeys.Contains(keyPart))
        {
            return ClassifyResult.Reserved;
        }

        modifiers = collected;
        canonicalKey = keyPart;
        return ClassifyResult.Ok;
    }

    private static string? CanonicalKey(string token)
    {
        if (string.Equals(token, "Return", StringComparison.OrdinalIgnoreCase))
        {
            return "Enter";
        }

        if (string.Equals(token, "Esc", StringComparison.OrdinalIgnoreCase))
        {
            return "Escape";
        }

        if (string.Equals(token, "Del", StringComparison.OrdinalIgnoreCase))
        {
            return "Delete";
        }

        return token.Length switch
        {
            1 when char.IsAsciiLetter(token[0]) => token.ToUpperInvariant(),
            1 when char.IsAsciiDigit(token[0]) => token,
            > 1 and <= 3 when token[0] is 'F' or 'f' && int.TryParse(token.AsSpan(1), out var function) &&
                              function is >= 1 and <= 24 => $"F{function}",
            _ => NamedKeys.FirstOrDefault(candidate =>
                string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static IReadOnlySet<string> CreateModifierRequiredKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            keys.Add(letter.ToString());
        }

        for (var digit = '0'; digit <= '9'; digit++)
        {
            keys.Add(digit.ToString());
        }

        foreach (var key in NamedKeys)
        {
            if (key is not "PageUp" and not "PageDown")
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private enum ClassifyResult
    {
        Malformed,
        Reserved,
        Ok
    }
}
