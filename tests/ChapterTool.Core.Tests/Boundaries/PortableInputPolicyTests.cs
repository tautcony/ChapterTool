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

    [Fact]
    public async Task CopyToBoundedMemoryRejectsSeekableStreamOverLimit()
    {
        await using var source = new OversizedSeekableStream();
        var copy = await PortableInputPolicy.CopyToBoundedMemoryAsync(source, TestContext.Current.CancellationToken);

        Assert.True(copy.Exceeded);
        Assert.Null(copy.Stream);
        Assert.Equal(0, source.ReadCalls);
    }

    [Fact]
    public async Task CopyToBoundedMemoryAcceptsSmallStream()
    {
        using var source = new MemoryStream([.. "ok"u8]);
        var copy = await PortableInputPolicy.CopyToBoundedMemoryAsync(source, TestContext.Current.CancellationToken);

        Assert.False(copy.Exceeded);
        Assert.NotNull(copy.Stream);
        Assert.Equal("ok"u8.ToArray(), copy.Stream.ToArray());
        await copy.Stream.DisposeAsync();
    }

    private sealed class OversizedSeekableStream : Stream
    {
        public int ReadCalls { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => PortableInputPolicy.MaxBytes + 1;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => Position
        };

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

}
