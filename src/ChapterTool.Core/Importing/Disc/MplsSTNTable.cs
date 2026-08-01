namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsSTNTable(
    ushort Length,
    byte NumberOfPrimaryVideoStreamEntries,
    byte NumberOfPrimaryAudioStreamEntries,
    byte NumberOfPrimaryPGStreamEntries,
    byte NumberOfPrimaryIGStreamEntries,
    byte NumberOfSecondaryAudioStreamEntries,
    byte NumberOfSecondaryVideoStreamEntries,
    byte NumberOfPIPPGStreamEntries,
    byte NumberOfDVStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryVideoStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryAudioStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryPGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PrimaryIGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> SecondaryAudioStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> SecondaryVideoStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> PIPPGStreamEntries,
    IReadOnlyList<MplsBasicStreamEntry> DVStreamEntries)
{
    /// <summary>
    /// Gets the SubPathStreamEntries value.
    /// </summary>
    public IReadOnlyList<MplsBasicStreamEntry> SubPathStreamEntries => [.. PIPPGStreamEntries, .. DVStreamEntries];

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsSTNTable Read(Stream stream)
    {
        var length = stream.ReadUInt16BigEndian();
        using var container = stream.CreateMplsContainer(length, 14, MplsParseLimits.MaximumStnTableLength, "stream table");
        container.SkipBytes(2);
        var primaryVideo = container.ReadByteChecked();
        var primaryAudio = container.ReadByteChecked();
        var primaryPg = container.ReadByteChecked();
        var primaryIg = container.ReadByteChecked();
        var secondaryAudio = container.ReadByteChecked();
        var secondaryVideo = container.ReadByteChecked();
        var pipPg = container.ReadByteChecked();
        var dv = container.ReadByteChecked();
        container.SkipBytes(4);

        var primaryVideoEntries = ReadEntries(container, primaryVideo, "primary video stream");
        var primaryAudioEntries = ReadEntries(container, primaryAudio, "primary audio stream");
        var primaryPgEntries = ReadEntries(container, primaryPg, "primary presentation graphics stream");
        var pipPgEntries = ReadEntries(container, pipPg, "picture-in-picture graphics stream");
        var primaryIgEntries = ReadEntries(container, primaryIg, "primary interactive graphics stream");
        var secondaryAudioEntries = ReadEntries(container, secondaryAudio, "secondary audio stream");
        var secondaryVideoEntries = ReadEntries(container, secondaryVideo, "secondary video stream");
        var dvEntries = ReadEntries(container, dv, "Dolby Vision stream");

        container.Complete("stream table");
        return new MplsSTNTable(
            length,
            primaryVideo,
            primaryAudio,
            primaryPg,
            primaryIg,
            secondaryAudio,
            secondaryVideo,
            pipPg,
            dv,
            primaryVideoEntries,
            primaryAudioEntries,
            primaryPgEntries,
            primaryIgEntries,
            secondaryAudioEntries,
            secondaryVideoEntries,
            pipPgEntries,
            dvEntries);
    }

    private static List<MplsBasicStreamEntry> ReadEntries(Stream stream, int count, string entryName)
    {
        MplsParseLimits.ValidateCount(count, MplsParseLimits.MaximumStreamEntriesPerCategory, entryName);
        MplsParseLimits.ValidateCountByBudget(count, 3, stream.Length - stream.Position, entryName);
        var entries = new List<MplsBasicStreamEntry>(count);
        for (var i = 0; i < count; i++)
        {
            entries.Add(MplsBasicStreamEntry.Read(stream));
        }

        return entries;
    }
}
