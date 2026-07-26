using ChapterTool.Core.Boundaries;

namespace ChapterTool.Core.Tests.Boundaries;

public sealed class PortableInputPolicyTests
{
    [Fact]
    public void Accepts_exact_byte_limit_and_rejects_one_byte_over()
    {
        Assert.True(PortableInputPolicy.IsWithinLimit(PortableInputPolicy.MaxBytes));
        Assert.False(PortableInputPolicy.IsWithinLimit(PortableInputPolicy.MaxBytes + 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("TQ==")]
    [InlineData("TWE=")]
    [InlineData("TWFu")]
    public void Reads_base64_decoded_length_without_decoding(string value)
    {
        Assert.True(PortableInputPolicy.TryGetBase64DecodedLength(value, out var length));
        Assert.Equal(Convert.FromBase64String(value).LongLength, length);
    }

    [Fact]
    public void Rejects_invalid_base64_shape()
    {
        Assert.False(PortableInputPolicy.TryGetBase64DecodedLength("TQ=", out _));
        Assert.False(PortableInputPolicy.TryGetBase64DecodedLength("TQ==A", out _));
        Assert.False(PortableInputPolicy.TryGetBase64DecodedLength("T$==", out _));
    }

}
