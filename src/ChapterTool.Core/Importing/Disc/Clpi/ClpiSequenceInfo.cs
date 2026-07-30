namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiATCSequence(
    uint SPNATCStart,
    byte NumberOfSTCSequences,
    byte OffsetSTCID,
    IReadOnlyList<ClpiSTCSequence> STCSequences);

internal sealed record ClpiSTCSequence(
    ushort PCRPID,
    uint SPNSTCStart,
    uint PresentationStartTime,
    uint PresentationEndTime);

internal sealed record ClpiSequenceInfo(
    uint Length,
    IReadOnlyList<ClpiATCSequence> ATCSequences)
{
    public static ClpiSequenceInfo Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 2, ClpiParseLimits.MaximumSequenceInfoLength, "sequence info");
        container.SkipBytes(1);
        var numberOfATCSequences = container.ReadByteChecked();
        ClpiParseLimits.ValidateCount(numberOfATCSequences, ClpiParseLimits.MaximumATCSequences, "ATC sequence");
        ClpiParseLimits.ValidateCountByBudget(numberOfATCSequences, 12, container.Remaining, "ATC sequence");

        var atcSequences = new List<ClpiATCSequence>(numberOfATCSequences);
        for (var i = 0; i < numberOfATCSequences; i++)
        {
            var spnATCStart = container.ReadUInt32BigEndian();
            var numberOfSTCSequences = container.ReadByteChecked();
            ClpiParseLimits.ValidateCount(numberOfSTCSequences, ClpiParseLimits.MaximumSTCSequences, "STC sequence");
            var offsetSTCID = container.ReadByteChecked();
            ClpiParseLimits.ValidateCountByBudget(numberOfSTCSequences, 12, container.Remaining, "STC sequence");

            var stcSequences = new List<ClpiSTCSequence>(numberOfSTCSequences);
            for (var j = 0; j < numberOfSTCSequences; j++)
            {
                var pcrPID = container.ReadUInt16BigEndian();
                var spnSTCStart = container.ReadUInt32BigEndian();
                var presentationStartTime = container.ReadUInt32BigEndian();
                var presentationEndTime = container.ReadUInt32BigEndian();
                stcSequences.Add(new ClpiSTCSequence(pcrPID, spnSTCStart, presentationStartTime, presentationEndTime));
            }

            atcSequences.Add(new ClpiATCSequence(spnATCStart, numberOfSTCSequences, offsetSTCID, stcSequences));
        }

        container.Complete("sequence info");
        return new ClpiSequenceInfo(length, atcSequences);
    }

    public ClpiSTCSequence? FindSTCSequence(byte stcId)
    {
        var cursor = 0;
        foreach (var atc in ATCSequences)
        {
            var localId = stcId - atc.OffsetSTCID;
            if (localId >= 0 && localId < atc.STCSequences.Count)
            {
                return atc.STCSequences[localId];
            }

            cursor += atc.NumberOfSTCSequences;
        }

        return null;
    }
}
