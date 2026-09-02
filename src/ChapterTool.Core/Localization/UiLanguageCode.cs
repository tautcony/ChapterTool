namespace ChapterTool.Core.Localization;

/// <summary>Normalizes user-interface language codes for every host.</summary>
public static class UiLanguageCode
{
    /// <summary>English culture name.</summary>
    public const string English = "en-US";

    /// <summary>Simplified Chinese culture name.</summary>
    public const string Chinese = "zh-CN";

    /// <summary>Japanese culture name.</summary>
    public const string Japanese = "ja-JP";

    /// <summary>Gets supported culture names.</summary>
    public static IReadOnlyList<string> Supported { get; } = [English, Chinese, Japanese];

    /// <summary>
    /// Maps a user language value to a supported culture name.
    /// Short forms such as <c>zh</c> and <c>ja</c> are accepted.
    /// </summary>
    public static string Normalize(string? culture)
    {
        TryNormalize(culture, out var normalized);
        return normalized;
    }

    /// <summary>
    /// Maps a user language value to a supported culture name.
    /// Returns <see langword="false"/> when the value is empty or unrecognized.
    /// </summary>
    public static bool TryNormalize(string? culture, out string normalized)
    {
        var trimmed = culture?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            normalized = English;
            return false;
        }

        if (string.Equals(trimmed, Chinese, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "zh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Chinese;
            return true;
        }

        if (string.Equals(trimmed, Japanese, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "ja", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Japanese;
            return true;
        }

        if (string.Equals(trimmed, English, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "en", StringComparison.OrdinalIgnoreCase))
        {
            normalized = English;
            return true;
        }

        normalized = English;
        return false;
    }
}
