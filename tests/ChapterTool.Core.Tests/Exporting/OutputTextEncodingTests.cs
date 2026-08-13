using ChapterTool.Core.Exporting;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class OutputTextEncodingTests
{
    [Theory]
    [InlineData("utf16le", OutputTextEncoding.Utf16LittleEndian)]
    [InlineData("UTF16BE", OutputTextEncoding.Utf16BigEndian)]
    [InlineData("Utf16LittleEndian", OutputTextEncoding.Utf16LittleEndian)]
    [InlineData("utf32le", OutputTextEncoding.Utf32LittleEndian)]
    [InlineData("Utf8", OutputTextEncoding.Utf8)]
    public void TryParseAcceptsSettingsIdsAndEnumNames(string value, OutputTextEncoding expected)
    {
        Assert.True(OutputTextEncodings.TryParse(value, out var encoding));
        Assert.Equal(expected, encoding);
    }

    [Fact]
    public void TryParseRejectsUnknownValues()
    {
        Assert.False(OutputTextEncodings.TryParse("latin1", out _));
        Assert.False(OutputTextEncodings.TryParse(string.Empty, out _));
    }
}
