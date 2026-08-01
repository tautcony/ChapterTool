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
        return bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3),
            [0xFF, 0xFE, ..] => Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2),
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2),
            _ => new UTF8Encoding(false, true).GetString(bytes)
        };
    }
}
