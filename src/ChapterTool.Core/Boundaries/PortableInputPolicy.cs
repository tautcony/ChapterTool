namespace ChapterTool.Core.Boundaries;

/// <summary>Defines the shared byte budget for portable text and binary hosts.</summary>
public static class PortableInputPolicy
{
    /// <summary>Maximum decoded input size for portable hosts.</summary>
    public const long MaxBytes = 64 * 1024 * 1024;

    /// <summary>Checks whether a byte count fits the portable input budget.</summary>
    public static bool IsWithinLimit(long byteCount) => byteCount is >= 0 and <= MaxBytes;

    /// <summary>
    /// Gets the decoded byte count for Base64 without allocating the decoded buffer.
    /// The method validates the length and padding shape. The caller validates characters
    /// when it performs the actual conversion.
    /// </summary>
    public static bool TryGetBase64DecodedLength(ReadOnlySpan<char> content, out long byteCount)
    {
        var significantLength = 0;
        var padding = 0;
        var seenPadding = false;

        foreach (var character in content)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (character == '=')
            {
                seenPadding = true;
                padding++;
                if (padding > 2)
                {
                    byteCount = 0;
                    return false;
                }
            }
            else
            {
                if (seenPadding || !IsBase64Character(character))
                {
                    byteCount = 0;
                    return false;
                }
            }

            significantLength++;
        }

        if (significantLength == 0)
        {
            byteCount = 0;
            return true;
        }

        if (significantLength % 4 != 0)
        {
            byteCount = 0;
            return false;
        }

        byteCount = ((long)significantLength / 4 * 3) - padding;
        return byteCount >= 0;
    }

    private static bool IsBase64Character(char character) =>
        character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '+'
            or '/';
}
