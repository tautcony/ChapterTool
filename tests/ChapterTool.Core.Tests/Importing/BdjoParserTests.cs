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
        Assert.Equal("*****", file.TerminalInfo.DefaultFont);
        Assert.Empty(file.ApplicationCacheInfo.Items);
        Assert.Empty(file.ApplicationManagementTable.Applications);
        Assert.Equal(string.Empty, file.FileAccessInfo.Path);
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

    [Fact]
    public void ParserReadsCompleteBdjoMetadata()
    {
        using var stream = new MemoryStream(BuildMetadataBdjo());

        var file = BdjoFile.Read(stream);

        Assert.Equal("F0001", file.TerminalInfo.DefaultFont);
        Assert.Equal((byte)7, file.TerminalInfo.InitialHaviConfigId);
        Assert.True(file.TerminalInfo.MenuCallMask);
        var cache = Assert.Single(file.ApplicationCacheInfo.Items);
        Assert.Equal((byte)1, cache.Type);
        Assert.Equal("00001", cache.ReferenceName);
        var app = Assert.Single(file.ApplicationManagementTable.Applications);
        Assert.Equal(0x01020304U, app.OrganizationId);
        Assert.Equal((ushort)7, app.ApplicationId);
        Assert.Equal("Demo", Assert.Single(app.Names).Name);
        Assert.Equal("00000", app.BaseDirectory);
        Assert.Equal("Main", app.InitialClass);
        Assert.Equal("arg", Assert.Single(app.Parameters));
        Assert.True(file.KeyInterestTable.Play);
        Assert.True(file.KeyInterestTable.Pause);
        Assert.Equal("BDMV;JAR", file.FileAccessInfo.Path);
    }

    private static byte[] BuildBdjo(string first, string? second = null, bool accessToAll = false, bool autostart = false)
    {
        var names = second == null ? new[] { first } : new[] { first, second };
        var bytes = new byte[48 + names.Length * 6];
        Encoding.ASCII.GetBytes("BDJO0240").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 10);
        Encoding.ASCII.GetBytes("*****").CopyTo(bytes, 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(22), 2);
        var flags = (uint)names.Length << 21;
        if (accessToAll)
        {
            flags |= 0x00100000;
        }

        if (autostart)
        {
            flags |= 0x00080000;
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(28), (uint)(4 + names.Length * 6));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(32), flags);
        var offset = 36;
        foreach (var name in names)
        {
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, offset);
            offset += 6;
        }

        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), 2);

        return bytes;
    }

    private static byte[] BuildMetadataBdjo()
    {
        var bytes = new List<byte>(160);
        bytes.AddRange("BDJO0240"u8.ToArray());
        AddUInt32(bytes, 10);
        bytes.AddRange("F0001"u8.ToArray());
        bytes.Add(0x7C);
        bytes.AddRange(new byte[4]);
        AddUInt32(bytes, 14);
        bytes.Add(1);
        bytes.Add(0);
        bytes.Add(1);
        bytes.AddRange("00001eng"u8.ToArray());
        bytes.AddRange(new byte[3]);
        AddUInt32(bytes, 10);
        AddUInt32(bytes, 1U << 21);
        bytes.AddRange("00001"u8.ToArray());
        bytes.Add(0);

        var applications = new List<byte>();
        applications.Add(1);
        applications.Add(0);
        applications.Add(1);
        applications.Add(0x10);
        AddUInt32(applications, 0x01020304);
        AddUInt16(applications, 7);
        applications.AddRange(new byte[10]);
        AddUInt16(applications, 0x1000);
        AddUInt16(applications, 1);
        applications.AddRange(new byte[] { 2, 1, 0, 0 });
        applications.Add(10);
        applications.Add(0x50);
        AddUInt16(applications, 8);
        applications.AddRange("eng"u8.ToArray());
        applications.Add(4);
        applications.AddRange("Demo"u8.ToArray());
        applications.AddRange(new byte[2]);
        AddUInt16(applications, 0x1234);
        applications.Add(5);
        applications.AddRange("00000"u8.ToArray());
        applications.AddRange(new byte[2]);
        applications.Add(4);
        applications.AddRange("Main"u8.ToArray());
        applications.Add(0);
        applications.Add(4);
        applications.Add(3);
        applications.AddRange("arg"u8.ToArray());
        applications.Add(0);
        AddUInt32(bytes, (uint)applications.Count);
        bytes.AddRange(applications);
        AddUInt32(bytes, 0x82000000);
        AddUInt16(bytes, 8);
        bytes.AddRange("BDMV;JAR"u8.ToArray());
        return bytes.ToArray();
    }

    private static void AddUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    private static void AddUInt32(List<byte> bytes, uint value)
    {
        bytes.Add((byte)(value >> 24));
        bytes.Add((byte)(value >> 16));
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }
}
