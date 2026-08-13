using System.Text;

namespace ChapterTool.Core.Importing.Cue;

internal static class CueTextDecoder
{
    /// <summary>
    /// Executes the Decode operation.
    /// </summary>
    /// <param name="bytes">The encoded CUE text bytes to decode.</param>
    /// <returns>The operation result.</returns>
    public static string Decode(byte[] bytes) => Decode(bytes, out _);

    /// <summary>
    /// Executes the Decode operation.
    /// </summary>
    /// <param name="bytes">The encoded CUE text bytes to decode.</param>
    /// <param name="usedEncodingFallback">Whether the bytes are not valid UTF-8 and permissive decoding replaced invalid sequences.</param>
    /// <returns>The operation result.</returns>
    public static string Decode(byte[] bytes, out bool usedEncodingFallback)
    {
        usedEncodingFallback = false;
        switch (bytes)
        {
            case [0xFF, 0xFE, ..]:
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            case [0xFE, 0xFF, ..]:
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var offset = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            // Legacy encodings (GBK, Shift-JIS, ANSI) are common for CUE files.
            // Decode permissively so import can continue; the caller reports a warning.
            usedEncodingFallback = true;
            return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }
    }
}
