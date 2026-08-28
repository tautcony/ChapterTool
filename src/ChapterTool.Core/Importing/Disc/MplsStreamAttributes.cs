namespace ChapterTool.Core.Importing.Disc;

internal sealed record MplsStreamAttributes(
    byte Length,
    byte StreamCodingType,
    byte? VideoFormat,
    byte? FrameRate,
    byte? DynamicRangeType,
    byte? ColorSpace,
    bool? CRFlag,
    bool? HDRPlusFlag,
    byte? AudioFormat,
    byte? SampleRate,
    byte? CharacterCode,
    string? LanguageCode)
{
    /// <summary>
    /// Executes the Read operation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The operation result.</returns>
    public static MplsStreamAttributes Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        using var container = stream.CreateMplsContainer(length, 0, byte.MaxValue, "stream attributes");
        if (length == 0)
        {
            container.Complete("stream attributes");
            return new MplsStreamAttributes(length, 0, null, null, null, null, null, null, null, null, null, null);
        }

        var streamCodingType = container.ReadByteChecked();
        var fields = ReadCodingFields(container, streamCodingType);

        container.Complete("stream attributes");
        return new MplsStreamAttributes(
            length,
            streamCodingType,
            fields.VideoFormat,
            fields.FrameRate,
            fields.DynamicRangeType,
            fields.ColorSpace,
            fields.CrFlag,
            fields.HdrPlusFlag,
            fields.AudioFormat,
            fields.SampleRate,
            fields.CharacterCode,
            fields.LanguageCode);
    }

    private static CodingFields ReadCodingFields(Stream stream, byte streamCodingType)
    {
        var fields = new CodingFields();
        switch (streamCodingType)
        {
            case 0x01 or 0x02 or 0x1B or 0x20 or 0xEA:
                ReadVideoFields(stream, fields);
                break;
            case 0x24:
                ReadHdrVideoFields(stream, fields);
                break;
            case 0x03 or 0x04 or 0x80 or 0x81 or 0x82 or 0x83 or 0x84 or 0x85 or 0x86 or 0xA1 or 0xA2:
                ReadAudioFields(stream, fields);
                break;
            case 0x90 or 0x91:
                fields.LanguageCode = stream.ReadAscii(3);
                break;
            case 0x92:
                fields.CharacterCode = stream.ReadByteChecked();
                fields.LanguageCode = stream.ReadAscii(3);
                break;
        }

        return fields;
    }

    private static void ReadVideoFields(Stream stream, CodingFields fields)
    {
        (fields.VideoFormat, fields.FrameRate) = ReadVideoInfo(stream);
    }

    private static void ReadHdrVideoFields(Stream stream, CodingFields fields)
    {
        (fields.VideoFormat, fields.FrameRate) = ReadVideoInfo(stream);
        var dynamicRangeAndColor = stream.ReadByteChecked();
        fields.DynamicRangeType = (byte)(dynamicRangeAndColor >> 4);
        fields.ColorSpace = (byte)(dynamicRangeAndColor & 0x0f);
        var hdrFlags = stream.ReadByteChecked();
        fields.CrFlag = ((hdrFlags >> 7) & 1) == 1;
        fields.HdrPlusFlag = ((hdrFlags >> 6) & 1) == 1;
    }

    private static void ReadAudioFields(Stream stream, CodingFields fields)
    {
        (fields.AudioFormat, fields.SampleRate) = ReadAudioInfo(stream);
        fields.LanguageCode = stream.ReadAscii(3);
    }

    private sealed class CodingFields
    {
        internal byte? VideoFormat { get; set; }

        internal byte? FrameRate { get; set; }

        internal byte? DynamicRangeType { get; set; }

        internal byte? ColorSpace { get; set; }

        internal bool? CrFlag { get; set; }

        internal bool? HdrPlusFlag { get; set; }

        internal byte? AudioFormat { get; set; }

        internal byte? SampleRate { get; set; }

        internal byte? CharacterCode { get; set; }

        internal string? LanguageCode { get; set; }
    }

    private static (byte VideoFormat, byte FrameRate) ReadVideoInfo(Stream stream)
    {
        var videoInfo = stream.ReadByteChecked();
        return ((byte)(videoInfo >> 4), (byte)(videoInfo & 0x0f));
    }

    private static (byte AudioFormat, byte SampleRate) ReadAudioInfo(Stream stream)
    {
        var audioInfo = stream.ReadByteChecked();
        return ((byte)(audioInfo >> 4), (byte)(audioInfo & 0x0f));
    }
}
