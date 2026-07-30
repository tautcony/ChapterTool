namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiProgram(
    uint SPNProgramSequenceStart,
    ushort ProgramMapPID,
    IReadOnlyList<ushort> StreamPIDs,
    IReadOnlyList<ClpiStreamCodingInfo> StreamCodingInfos);

internal sealed record ClpiProgramInfo(
    uint Length,
    IReadOnlyList<ClpiProgram> Programs)
{
    public static ClpiProgramInfo Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 2, ClpiParseLimits.MaximumProgramInfoLength, "program info");
        container.SkipBytes(1);
        var numberOfPrograms = container.ReadByteChecked();
        ClpiParseLimits.ValidateCount(numberOfPrograms, ClpiParseLimits.MaximumPrograms, "program");
        ClpiParseLimits.ValidateCountByBudget(numberOfPrograms, 8, container.Remaining, "program");

        var programs = new List<ClpiProgram>(numberOfPrograms);
        for (var i = 0; i < numberOfPrograms; i++)
        {
            var spnProgramSequenceStart = container.ReadUInt32BigEndian();
            var programMapPID = container.ReadUInt16BigEndian();
            var numberOfStreamsInPS = container.ReadByteChecked();
            ClpiParseLimits.ValidateCount(numberOfStreamsInPS, ClpiParseLimits.MaximumStreamsInPS, "stream in program");
            container.SkipBytes(1);
            ClpiParseLimits.ValidateCountByBudget(numberOfStreamsInPS, 3, container.Remaining, "stream in program");

            var streamPIDs = new List<ushort>(numberOfStreamsInPS);
            var streamCodingInfos = new List<ClpiStreamCodingInfo>(numberOfStreamsInPS);
            for (var j = 0; j < numberOfStreamsInPS; j++)
            {
                streamPIDs.Add(container.ReadUInt16BigEndian());
                streamCodingInfos.Add(ClpiStreamCodingInfo.Read(container));
            }

            programs.Add(new ClpiProgram(spnProgramSequenceStart, programMapPID, streamPIDs, streamCodingInfos));
        }

        container.Complete("program info");
        return new ClpiProgramInfo(length, programs);
    }
}
