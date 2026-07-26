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
        byte? videoFormat = null;
        byte? frameRate = null;
        byte? dynamicRangeType = null;
        byte? colorSpace = null;
        bool? crFlag = null;
        bool? hdrPlusFlag = null;
        byte? audioFormat = null;
        byte? sampleRate = null;
        byte? characterCode = null;
        string? languageCode = null;

        switch (streamCodingType)
        {
            case 0x01:
            case 0x02:
            case 0x1B:
            case 0x20:
            case 0xEA:
                ReadVideoInfo(container, out videoFormat, out frameRate);
                break;
            case 0x24:
                ReadVideoInfo(container, out videoFormat, out frameRate);
                var dynamicRangeAndColor = container.ReadByteChecked();
                dynamicRangeType = (byte)(dynamicRangeAndColor >> 4);
                colorSpace = (byte)(dynamicRangeAndColor & 0x0f);
                var hdrFlags = container.ReadByteChecked();
                crFlag = ((hdrFlags >> 7) & 1) == 1;
                hdrPlusFlag = ((hdrFlags >> 6) & 1) == 1;
                break;
            case 0x03:
            case 0x04:
            case 0x80:
            case 0x81:
            case 0x82:
            case 0x83:
            case 0x84:
            case 0x85:
            case 0x86:
                ReadAudioInfo(container, out audioFormat, out sampleRate);
                languageCode = container.ReadAscii(3);
                break;
            case 0x90:
            case 0x91:
                languageCode = container.ReadAscii(3);
                break;
            case 0x92:
                characterCode = container.ReadByteChecked();
                languageCode = container.ReadAscii(3);
                break;
            case 0xA1:
            case 0xA2:
                ReadAudioInfo(container, out audioFormat, out sampleRate);
                languageCode = container.ReadAscii(3);
                break;
        }

        container.Complete("stream attributes");
        return new MplsStreamAttributes(
            length,
            streamCodingType,
            videoFormat,
            frameRate,
            dynamicRangeType,
            colorSpace,
            crFlag,
            hdrPlusFlag,
            audioFormat,
            sampleRate,
            characterCode,
            languageCode);
    }

    private static void ReadVideoInfo(Stream stream, out byte? videoFormat, out byte? frameRate)
    {
        var videoInfo = stream.ReadByteChecked();
        videoFormat = (byte)(videoInfo >> 4);
        frameRate = (byte)(videoInfo & 0x0f);
    }

    private static void ReadAudioInfo(Stream stream, out byte? audioFormat, out byte? sampleRate)
    {
        var audioInfo = stream.ReadByteChecked();
        audioFormat = (byte)(audioInfo >> 4);
        sampleRate = (byte)(audioInfo & 0x0f);
    }
}
