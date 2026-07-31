using System.Buffers.Binary;
using System.Text;
using ChapterTool.Core.Importing.Disc.Bdjo;

namespace ChapterTool.Core.Tests.Importing;

public sealed class BdjoParserTests
{
    [Fact]
    public void ParserReadsExplicitAutostartPlaylistTable()
    {
        using var stream = new MemoryStream(BuildBdjo("00017", "01001", accessToAll: false, autostart: true));

        var file = BdjoFile.Read(stream);

        Assert.Equal("0240", file.VersionNumber);
        Assert.Equal((ushort)2, file.AccessiblePlaylists.Count);
        Assert.True(file.AccessiblePlaylists.AutostartFirstPlaylist);
        Assert.False(file.AccessiblePlaylists.AccessToAll);
        Assert.Equal(new[] { "00017", "01001" }, file.AccessiblePlaylists.Names);
    }

    [Fact]
    public void ParserReadsAccessToAllFlag()
    {
        using var stream = new MemoryStream(BuildBdjo("00001", accessToAll: true, autostart: false));

        var file = BdjoFile.Read(stream);

        Assert.True(file.AccessiblePlaylists.AccessToAll);
        Assert.Single(file.AccessiblePlaylists.Names);
    }

    [Fact]
    public void ParserRejectsTruncatedAccessibleSection()
    {
        using var stream = new MemoryStream(BuildBdjo("00001", accessToAll: false, autostart: false)[..^2]);

        Assert.ThrowsAny<Exception>(() => BdjoFile.Read(stream));
    }

    [Fact]
    public void ParserRejectsNonNumericPlaylistName()
    {
        var bytes = BuildBdjo("0000X", accessToAll: false, autostart: false);
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => BdjoFile.Read(stream));
    }

    private static byte[] BuildBdjo(string first, string? second = null, bool accessToAll = false, bool autostart = false)
    {
        var names = second == null ? new[] { first } : new[] { first, second };
        var bytes = new byte[8 + 4 + 4 + 4 + 4 + names.Length * 6];
        Encoding.ASCII.GetBytes("BDJO0240").CopyTo(bytes, 0);
        var flags = (uint)names.Length << 21;
        if (accessToAll)
        {
            flags |= 0x00100000;
        }

        if (autostart)
        {
            flags |= 0x00080000;
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)(4 + names.Length * 6));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), flags);
        var offset = 24;
        foreach (var name in names)
        {
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, offset);
            offset += 6;
        }

        return bytes;
    }
}
