namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsClipNameWithRef(MplsClipName ClipName, byte RefToSTCID)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsClipNameWithRef Read(Stream stream) =>
        new(MplsClipName.Read(stream), stream.ReadByteChecked());
}
