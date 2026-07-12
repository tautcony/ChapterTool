namespace ChapterTool.Core.Importing.Disc;

/// <summary>
/// Seekable view over a declared MPLS container. The underlying stream remains
/// owned by the playlist reader; this view only advances within its budget.
/// </summary>
internal sealed class MplsBoundedStream : Stream
{
    private readonly Stream inner;
    private readonly long start;
    private long position;

    private MplsBoundedStream(Stream inner, long length, string containerName)
    {
        if (!inner.CanSeek || inner.Position < 0 || length < 0 || length > inner.Length - inner.Position)
        {
            throw new InvalidDataException($"MPLS {containerName} container exceeds the parent stream boundary.");
        }

        this.inner = inner;
        start = inner.Position;
        this.Length = length;
    }

    public static MplsBoundedStream Create(Stream inner, uint length, int minimumLength, int maximumLength, string containerName)
    {
        MplsParseLimits.ValidateContainerLength(length, minimumLength, maximumLength, containerName);
        return new MplsBoundedStream(inner, length, containerName);
    }

    public static MplsBoundedStream CreateToAddress(Stream inner, long endAddress, string sectionName)
    {
        if (!inner.CanSeek || endAddress < inner.Position || endAddress > inner.Length)
        {
            throw new InvalidDataException($"MPLS {sectionName} boundary {endAddress} is outside the parent stream.");
        }

        return new MplsBoundedStream(inner, endAddress - inner.Position, sectionName);
    }

    public long Remaining => Length - position;

    public void Complete(string containerName)
    {
        if (position > Length)
        {
            throw new InvalidDataException($"MPLS {containerName} consumed content beyond its declared length.");
        }

        if (position < Length)
        {
            Seek(Length - position, SeekOrigin.Current);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            throw new ArgumentOutOfRangeException();
        }

        var boundedCount = (int)Math.Min(count, Remaining);
        if (boundedCount == 0)
        {
            return 0;
        }

        var read = inner.Read(buffer, offset, boundedCount);
        position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (target < 0 || target > Length)
        {
            throw new InvalidDataException("MPLS seek crossed a container boundary.");
        }

        inner.Seek(start + target, SeekOrigin.Begin);
        position = target;
        return position;
    }

    public override void Flush() => inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position
    {
        get => position;
        set => Seek(value, SeekOrigin.Begin);
    }
}

internal static class MplsBoundedStreamExtensions
{
    internal static MplsBoundedStream CreateMplsContainer(this Stream stream, uint length, int minimumLength, int maximumLength, string containerName) =>
        MplsBoundedStream.Create(stream, length, minimumLength, maximumLength, containerName);
}
