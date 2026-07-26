namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsClipName(string ClipInformationFileName, string ClipCodecIdentifier)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsClipName Read(Stream stream) =>
        new(stream.ReadAscii(5), stream.ReadAscii(4));

    /// <inheritdoc />
    public override string ToString() => $"{ClipInformationFileName}.{ClipCodecIdentifier}";
}
