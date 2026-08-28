using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Core.Tests.Importing;

public sealed class MplsStreamAttributesTests
{
    [Fact]
    public void ReadHdrVideoCodingType24ParsesDynamicRangeColorAndHdrFlags()
    {
        using var stream = new MemoryStream([4, 0x24, 0x23, 0xAB, 0xC0]);

        var attributes = MplsStreamAttributes.Read(stream);

        Assert.Equal((byte)4, attributes.Length);
        Assert.Equal((byte)0x24, attributes.StreamCodingType);
        Assert.Equal((byte)2, attributes.VideoFormat);
        Assert.Equal((byte)3, attributes.FrameRate);
        Assert.Equal((byte)0xA, attributes.DynamicRangeType);
        Assert.Equal((byte)0xB, attributes.ColorSpace);
        Assert.True(attributes.CRFlag);
        Assert.True(attributes.HDRPlusFlag);
    }
}
