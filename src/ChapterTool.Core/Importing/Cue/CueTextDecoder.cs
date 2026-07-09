using System.Text;

namespace ChapterTool.Core.Importing.Cue;

internal static class CueTextDecoder
{
    /// <summary>
    /// Executes the Decode operation.
    /// </summary>
    /// <param name="bytes">The encoded CUE text bytes to decode.</param>
    /// <returns>The operation result.</returns>
    public static string Decode(byte[] bytes)
    {
        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes is [0xFF, 0xFE, ..])
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes is [0xFE, 0xFF, ..])
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return new UTF8Encoding(false, true).GetString(bytes);
    }
}
