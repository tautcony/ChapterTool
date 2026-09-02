namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiClipInfo(
    uint Length,
    byte ClipStreamType,
    byte ApplicationType,
    uint TSRecordingRate,
    uint NumberOfSourcePackets,
    bool IsAtcDelta,
    ClpiTsTypeInfo? TsTypeInfo,
    IReadOnlyList<ClpiAtcDelta> AtcDeltas,
    IReadOnlyList<ClpiFontInfo> Fonts)
{
    public static ClpiClipInfo Read(Stream stream)
    {
        var length = stream.ReadUInt32BigEndian();
        using var container = stream.CreateMplsContainer(length, 18, ClpiParseLimits.MaximumClipInfoLength, "clip info");
        container.SkipBytes(2);
        var clipStreamType = container.ReadByteChecked();
        var applicationType = container.ReadByteChecked();
        container.SkipBytes(3);
        var flagsAndCC5 = container.ReadByteChecked();
        var isCC5 = (flagsAndCC5 & 1) == 1;
        var tsRecordingRate = container.ReadUInt32BigEndian();
        var numberOfSourcePackets = container.ReadUInt32BigEndian();
        container.SkipBytes(128);

        var tsTypeInfo = ReadTsTypeInfo(container);
        var atcDeltas = ReadAtcDeltas(container, isCC5);
        var fonts = ReadFonts(container, applicationType);
        container.Complete("clip info");
        return new ClpiClipInfo(
            length,
            clipStreamType,
            applicationType,
            tsRecordingRate,
            numberOfSourcePackets,
            isCC5,
            tsTypeInfo,
            atcDeltas,
            fonts);
    }

    private static ClpiTsTypeInfo? ReadTsTypeInfo(MplsBoundedStream container)
    {
        if (container.Remaining >= 2)
        {
            var tsTypeInfoLength = container.ReadUInt16BigEndian();
            if (tsTypeInfoLength > container.Remaining)
            {
                throw new InvalidDataException("CLPI TS type information exceeds the clip info section.");
            }

            if (tsTypeInfoLength > 0)
            {
                if (tsTypeInfoLength < 5)
                {
                    throw new InvalidDataException("CLPI TS type information is too short.");
                }

                var validity = container.ReadByteChecked();
                var formatIdentifier = container.ReadAscii(4);
                container.SkipBytes(tsTypeInfoLength - 5);
                return new ClpiTsTypeInfo(validity, formatIdentifier);
            }
        }

        return null;
    }

    private static IReadOnlyList<ClpiAtcDelta> ReadAtcDeltas(MplsBoundedStream container, bool isCC5)
    {
        var atcDeltas = new List<ClpiAtcDelta>();
        if (!isCC5)
        {
            return atcDeltas;
        }

        if (container.Remaining < 2)
        {
            throw new InvalidDataException("CLPI ATC delta information is truncated.");
        }

        container.SkipBytes(1);
        var atcDeltaCount = container.ReadByteChecked();
        ClpiParseLimits.ValidateCountByBudget(atcDeltaCount, 14, container.Remaining, "ATC delta");
        for (var i = 0; i < atcDeltaCount; i++)
        {
            var delta = container.ReadUInt32BigEndian();
            var fileId = container.ReadAscii(5);
            var fileCode = container.ReadAscii(4);
            container.SkipBytes(1);
            atcDeltas.Add(new ClpiAtcDelta(delta, fileId, fileCode));
        }

        return atcDeltas;
    }

    private static IReadOnlyList<ClpiFontInfo> ReadFonts(MplsBoundedStream container, byte applicationType)
    {
        var fonts = new List<ClpiFontInfo>();
        if (applicationType != 6)
        {
            return fonts;
        }

        if (container.Remaining < 2)
        {
            throw new InvalidDataException("CLPI subtitle font information is truncated.");
        }

        container.SkipBytes(1);
        var fontCount = container.ReadByteChecked();
        ClpiParseLimits.ValidateCountByBudget(fontCount, 6, container.Remaining, "subtitle font");
        for (var i = 0; i < fontCount; i++)
        {
            var fileId = container.ReadAscii(5);
            container.SkipBytes(1);
            fonts.Add(new ClpiFontInfo(fileId));
        }

        return fonts;
    }

    public bool IsCC5 => IsAtcDelta;

    public TimeSpan DurationFromPackets =>
        TSRecordingRate > 0
            ? TimeSpan.FromSeconds((double)NumberOfSourcePackets * 192 / (TSRecordingRate * 8))
            : TimeSpan.Zero;
}

internal sealed record ClpiTsTypeInfo(byte Validity, string FormatIdentifier);

internal sealed record ClpiAtcDelta(uint Delta, string FileId, string FileCode);

internal sealed record ClpiFontInfo(string FileId);
