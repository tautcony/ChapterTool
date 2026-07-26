namespace ChapterTool.Core.Importing.Disc;

internal static class MplsStreamReadExtensions
{
    /// <summary>
    /// Executes the ReadByteChecked operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static byte ReadByteChecked(this Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new EndOfStreamException();
        }

        return (byte)value;
    }
}
