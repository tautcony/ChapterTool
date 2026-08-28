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
        var fields = ReadCodingFields(reader, streamCodingType);

        byte[]? isrc = null;
        if (reader.RemainingBytes >= 12)
        {
            isrc = reader.ReadBytes(12);
        }
        return new ClpiStreamCodingInfo(
            length,
            streamCodingType,
            fields.VideoFormat,
            fields.FrameRate,
            fields.VideoAspect,
            fields.OcFlag,
            fields.DynamicRangeType,
            fields.ColorSpace,
            fields.CrFlag,
            fields.HdrPlusFlag,
            fields.AudioFormat,
            fields.SampleRate,
            fields.CharacterCode,
            fields.LanguageCode,
            isrc);
    }

    private static CodingFields ReadCodingFields(ClpiBitReader reader, byte streamCodingType)
    {
        var fields = new CodingFields();
        switch (streamCodingType)
        {
            case 0x01 or 0x02 or 0x1B or 0x20 or 0xEA:
                ReadVideoFields(reader, fields);
                break;
            case 0x24:
                ReadHdrVideoFields(reader, fields);
                break;
            case 0x03 or 0x04 or 0x80 or 0x81 or 0x82 or 0x83 or 0x84 or 0x85 or 0x86 or 0xA1 or 0xA2:
                ReadAudioFields(reader, fields);
                break;
            case 0x90 or 0x91 or 0xA0:
                fields.LanguageCode = reader.ReadAscii(3);
                reader.SkipBits(8);
                break;
            case 0x92:
                fields.CharacterCode = (byte)reader.ReadBits(8);
                fields.LanguageCode = reader.ReadAscii(3);
                break;
        }

        return fields;
    }

    private static void ReadVideoFields(ClpiBitReader reader, CodingFields fields)
    {
        var (videoFormat, frameRate, videoAspect) = ReadVideoInfo(reader);
        fields.VideoFormat = videoFormat;
        fields.FrameRate = frameRate;
        fields.VideoAspect = videoAspect;
        reader.SkipBits(2);
        fields.OcFlag = reader.ReadBits(1) != 0;
        reader.SkipBits(17);
    }

    private static void ReadHdrVideoFields(ClpiBitReader reader, CodingFields fields)
    {
        var (videoFormat, frameRate, videoAspect) = ReadVideoInfo(reader);
        fields.VideoFormat = videoFormat;
        fields.FrameRate = frameRate;
        fields.VideoAspect = videoAspect;
        reader.SkipBits(2);
        fields.OcFlag = reader.ReadBits(1) != 0;
        fields.CrFlag = reader.ReadBits(1) != 0;
        fields.DynamicRangeType = (byte)reader.ReadBits(4);
        fields.ColorSpace = (byte)reader.ReadBits(4);
        fields.HdrPlusFlag = reader.ReadBits(1) != 0;
        reader.SkipBits(7);
    }

    private static void ReadAudioFields(ClpiBitReader reader, CodingFields fields)
    {
        var (audioFormat, sampleRate) = ReadAudioInfo(reader);
        fields.AudioFormat = audioFormat;
        fields.SampleRate = sampleRate;
        fields.LanguageCode = reader.ReadAscii(3);
    }

    private sealed class CodingFields
    {
        internal byte? VideoFormat { get; set; }

        internal byte? FrameRate { get; set; }

        internal byte? VideoAspect { get; set; }

        internal bool? OcFlag { get; set; }

        internal byte? DynamicRangeType { get; set; }

        internal byte? ColorSpace { get; set; }

        internal bool? CrFlag { get; set; }

        internal bool? HdrPlusFlag { get; set; }

        internal byte? AudioFormat { get; set; }

        internal byte? SampleRate { get; set; }

        internal byte? CharacterCode { get; set; }

        internal string? LanguageCode { get; set; }
    }

    private static (byte VideoFormat, byte FrameRate, byte VideoAspect) ReadVideoInfo(ClpiBitReader reader)
    {
        return ((byte)reader.ReadBits(4), (byte)reader.ReadBits(4), (byte)reader.ReadBits(4));
    }

    private static (byte AudioFormat, byte SampleRate) ReadAudioInfo(ClpiBitReader reader)
    {
        return ((byte)reader.ReadBits(4), (byte)reader.ReadBits(4));
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
