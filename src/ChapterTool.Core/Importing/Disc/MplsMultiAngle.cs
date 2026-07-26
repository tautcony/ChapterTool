namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsMultiAngle(
    byte NumberOfAngles,
    byte FlagField,
    IReadOnlyList<MplsClipNameWithRef> Angles)
{
    /// <summary>
    /// Gets the IsDifferentAudios value.
    /// </summary>
    public bool IsDifferentAudios => ((FlagField >> 1) & 1) == 1;

    /// <summary>
    /// Gets the IsSeamlessAngleChange value.
    /// </summary>
    public bool IsSeamlessAngleChange => (FlagField & 1) == 1;

    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsMultiAngle Read(Stream stream)
    {
        var numberOfAngles = stream.ReadByteChecked();
        var flagField = stream.ReadByteChecked();
        MplsParseLimits.ValidateCount(numberOfAngles, MplsParseLimits.MaximumMultiAngleEntries, "multi-angle entry");
        MplsParseLimits.ValidateCountByBudget(numberOfAngles - 1, 10, stream.Length - stream.Position, "multi-angle entry");
        var angles = new List<MplsClipNameWithRef>(Math.Max(0, numberOfAngles - 1));
        for (var i = 0; i < numberOfAngles - 1; i++)
        {
            angles.Add(MplsClipNameWithRef.Read(stream));
        }

        return new MplsMultiAngle(numberOfAngles, flagField, angles);
    }
}
