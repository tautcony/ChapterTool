namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsBasicStreamEntry(MplsStreamEntry StreamEntry, MplsStreamAttributes StreamAttributes)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsBasicStreamEntry Read(Stream stream) =>
        new(MplsStreamEntry.Read(stream), MplsStreamAttributes.Read(stream));
}
