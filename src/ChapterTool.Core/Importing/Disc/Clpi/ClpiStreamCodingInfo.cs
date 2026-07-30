namespace ChapterTool.Core.Importing.Disc.Clpi;

internal sealed record ClpiStreamCodingInfo(
    byte Length,
    byte StreamCodingType,
    byte? VideoFormat,
    byte? FrameRate,
    byte? VideoAspect,
    bool? OCFlag,
    byte? DynamicRangeType,
    byte? ColorSpace,
    bool? CRFlag,
    bool? HDRPlusFlag,
    byte? AudioFormat,
    byte? SampleRate,
    byte? CharacterCode,
    string? LanguageCode,
    byte[]? Isrc)
{
    public static ClpiStreamCodingInfo Read(Stream stream)
    {
        var length = stream.ReadByteChecked();
        using var container = stream.CreateMplsContainer(length, 1, byte.MaxValue, "stream coding info");
        var payload = container.ReadExactBytes(length);
        if (length == 0)
        {
            return new ClpiStreamCodingInfo(length, 0, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        var reader = new ClpiBitReader(payload);
        var streamCodingType = (byte)reader.ReadBits(8);
        byte? videoFormat = null;
        byte? frameRate = null;
        byte? videoAspect = null;
        bool? ocFlag = null;
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
                ReadVideoInfo(reader, out videoFormat, out frameRate, out videoAspect);
                reader.SkipBits(2);
                ocFlag = reader.ReadBits(1) != 0;
                reader.SkipBits(17);
                break;
            case 0x24:
                ReadVideoInfo(reader, out videoFormat, out frameRate, out videoAspect);
                reader.SkipBits(2);
                ocFlag = reader.ReadBits(1) != 0;
                crFlag = reader.ReadBits(1) != 0;
                dynamicRangeType = (byte)reader.ReadBits(4);
                colorSpace = (byte)reader.ReadBits(4);
                hdrPlusFlag = reader.ReadBits(1) != 0;
                reader.SkipBits(7);
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
            case 0xA1:
            case 0xA2:
                ReadAudioInfo(reader, out audioFormat, out sampleRate);
                languageCode = reader.ReadAscii(3);
                break;
            case 0x90:
            case 0x91:
            case 0xA0:
                languageCode = reader.ReadAscii(3);
                reader.SkipBits(8);
                break;
            case 0x92:
                characterCode = (byte)reader.ReadBits(8);
                languageCode = reader.ReadAscii(3);
                break;
        }

        byte[]? isrc = null;
        if (reader.RemainingBytes >= 12)
        {
            isrc = reader.ReadBytes(12);
        }
        return new ClpiStreamCodingInfo(
            length,
            streamCodingType,
            videoFormat,
            frameRate,
            videoAspect,
            ocFlag,
            dynamicRangeType,
            colorSpace,
            crFlag,
            hdrPlusFlag,
            audioFormat,
            sampleRate,
            characterCode,
            languageCode,
            isrc);
    }

    private static void ReadVideoInfo(ClpiBitReader reader, out byte? videoFormat, out byte? frameRate, out byte? videoAspect)
    {
        videoFormat = (byte)reader.ReadBits(4);
        frameRate = (byte)reader.ReadBits(4);
        videoAspect = (byte)reader.ReadBits(4);
    }

    private static void ReadAudioInfo(ClpiBitReader reader, out byte? audioFormat, out byte? sampleRate)
    {
        audioFormat = (byte)reader.ReadBits(4);
        sampleRate = (byte)reader.ReadBits(4);
    }

    private sealed class ClpiBitReader(byte[] bytes)
    {
        private int bitPosition;

        public int RemainingBytes => (bytes.Length * 8 - bitPosition) / 8;

        public uint ReadBits(int count)
        {
            if (count is < 1 or > 32 || count > bytes.Length * 8 - bitPosition)
            {
                throw new InvalidDataException("CLPI stream coding information is truncated.");
            }

            uint value = 0;
            for (var i = 0; i < count; i++)
            {
                var position = bitPosition++;
                value = (value << 1) | (uint)((bytes[position / 8] >> (7 - position % 8)) & 1);
            }

            return value;
        }

        public void SkipBits(int count) => _ = ReadBits(count);

        public string ReadAscii(int count) =>
            System.Text.Encoding.ASCII.GetString(ReadBytes(count));

        public byte[] ReadBytes(int count)
        {
            if (count < 0 || bitPosition % 8 != 0 || count > RemainingBytes)
            {
                throw new InvalidDataException("CLPI stream coding information is not byte aligned.");
            }

            var result = new byte[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = (byte)ReadBits(8);
            }

            return result;
        }
    }
}
