namespace ChapterTool.Core.Boundaries;

/// <summary>Defines the shared byte budget for portable text and binary hosts.</summary>
public static class PortableInputPolicy
{
    /// <summary>Maximum decoded input size for portable hosts.</summary>
    public const long MaxBytes = 64 * 1024 * 1024;

    /// <summary>Result of a bounded stream copy.</summary>
    /// <param name="Stream">The copied buffer, or null when the budget was exceeded.</param>
    /// <param name="Exceeded">true when the input is larger than <see cref="MaxBytes"/>.</param>
    public readonly record struct BoundedStreamCopy(MemoryStream? Stream, bool Exceeded)
    {
        /// <summary>A copy result that reports an over-budget input.</summary>
        public static BoundedStreamCopy TooLarge { get; } = new(null, true);
    }

    /// <summary>Checks whether a byte count fits the portable input budget.</summary>
    public static bool IsWithinLimit(long byteCount) => byteCount is >= 0 and <= MaxBytes;

    /// <summary>
    /// Copies a stream into a memory buffer. The copy stops when the portable
    /// byte budget is exceeded.
    /// </summary>
    public static async ValueTask<BoundedStreamCopy> CopyToBoundedMemoryAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.CanSeek)
        {
            try
            {
                var remaining = source.Length - source.Position;
                if (!IsWithinLimit(remaining))
                {
                    return BoundedStreamCopy.TooLarge;
                }
            }
            catch (NotSupportedException)
            {
            }
        }

        var memory = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (!IsWithinLimit(total))
            {
                await memory.DisposeAsync();
                return BoundedStreamCopy.TooLarge;
            }

            memory.Write(buffer, 0, read);
        }

        memory.Position = 0;
        return new BoundedStreamCopy(memory, false);
    }

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
