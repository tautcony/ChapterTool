using System.Buffers.Binary;
using System.Text;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Infrastructure.Importing.Bdmv;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class BdmvBdjoNavigationTests
{
    [Fact]
    public async Task ImportLoadsBdjoFromPrimaryDirectory()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "primary");

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        var loaded = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.NavigationSource
            && diagnostic.Message.Contains("Loaded primary BDJO 00000.", StringComparison.Ordinal));
        Assert.EndsWith(Path.Combine("BDJO", "00000.bdjo"), loaded.Location ?? string.Empty);
    }

    [Fact]
    public async Task ImportLoadsBdjoFromBackupDirectory()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "backup");

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.NavigationSource
            && diagnostic.Message.Contains("Loaded backup BDJO 00000.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportReportsUnparseableBdjo()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "missing");

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation
            && diagnostic.Message.Contains("BD-J object 00000 could not be parsed.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportReportsDynamicSelectionWarningForAccessToAllBdjo()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "primary", accessToAll: true);

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == ChapterDiagnosticCode.UnsupportedDynamicBdJNavigation
            && diagnostic.Message.Contains("may select playlists dynamically", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAppliesAutostartBdjoEvidenceToPlaylist()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "primary", autostart: true);

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Groups.SelectMany(static group => group.Entries), entry => entry.Id == "00001.mpls");
    }

    [Fact]
    public async Task ImportSkipsProhibitedAndHiddenTitlesAndLogsMovieObjectUnavailability()
    {
        using var temp = new TempDirectory();
        BuildDisc(temp.Path, bdjoLocation: "primary", withProhibitedAndHiddenTitles: true);

        var result = await new BdmvImporter().ImportAsync(new ChapterImportRequest(temp.Path), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("Skipped prohibited INDEX title", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("INDEX title 2 is hidden.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("MovieObject navigation was unavailable", StringComparison.Ordinal));
    }

    private static void BuildDisc(
        string root,
        string bdjoLocation,
        bool accessToAll = false,
        bool autostart = false,
        bool withProhibitedAndHiddenTitles = false)
    {
        var bdmv = Path.Combine(root, "BDMV");
        var playlistDir = Path.Combine(bdmv, "PLAYLIST");
        Directory.CreateDirectory(playlistDir);

        var fixtureDir = Path.Combine(
            FixtureResolver.RepositoryRoot,
            "tests",
            "ChapterTool.Core.Tests",
            "Fixtures",
            "Importing",
            "Disc",
            "Bdmv",
            "Detective Conan Zero the Enforcer",
            "BDMV",
            "PLAYLIST");
        File.Copy(Path.Combine(fixtureDir, "00000.mpls"), Path.Combine(playlistDir, "00001.mpls"));

        File.WriteAllBytes(Path.Combine(bdmv, "index.bdmv"), BuildIndex(withProhibitedAndHiddenTitles));

        if (bdjoLocation != "missing")
        {
            var bdjoDirectory = Path.Combine(bdmv, bdjoLocation == "backup" ? "BACKUP" : string.Empty, "BDJO");
            Directory.CreateDirectory(bdjoDirectory);
            File.WriteAllBytes(Path.Combine(bdjoDirectory, "00000.bdjo"), BuildBdjo(accessToAll, autostart));
        }
    }

    private static byte[] BuildBdjo(bool accessToAll, bool autostart)
    {
        const string name = "00001";
        var bytes = new byte[48 + 6];
        "BDJO0240"u8.ToArray().CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 10);
        "*****"u8.ToArray().CopyTo(bytes, 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(22), 2);
        var flags = 1u << 21;
        if (accessToAll)
        {
            flags |= 0x00100000;
        }

        if (autostart)
        {
            flags |= 0x00080000;
        }

        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(28), 4 + 6);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(32), flags);
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 36);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(42), 2);
        return bytes;
    }

    private static byte[] BuildIndex(bool withProhibitedAndHiddenTitles)
    {
        const int headerSize = 40;
        const int appInfoSize = 40;
        const int indexesAddress = headerSize + appInfoSize;
        const int indexesContentSize = 12 + 12 + 2 + 12 * 3;
        const int paddedContentSize = 64;
        const int extAddress = indexesAddress + 4 + paddedContentSize;

        using var builder = new IndexBinaryBuilder();
        builder.Ascii("INDX");
        builder.Ascii("0100");
        builder.UInt32BE(indexesAddress);
        builder.UInt32BE(extAddress);
        builder.Reserved(24);

        builder.UInt32BE(36);
        builder.Byte(0);
        builder.Byte(0);
        builder.Byte(0x10);
        builder.Byte(0x01);
        builder.Ascii("TEST DISC");
        builder.Reserved(23);

        builder.SeekTo(indexesAddress);
        builder.UInt32BE(paddedContentSize);
        builder.Reserved(12);
        builder.Reserved(12);
        builder.UInt16BE(3);
        if (withProhibitedAndHiddenTitles)
        {
            WriteTitleEntry(builder, objectType: 1, accessType: 0x01, playbackType: 0, "00001");
            WriteTitleEntry(builder, objectType: 1, accessType: 0x02, playbackType: 0, "00002");
        }
        else
        {
            WriteTitleEntry(builder, objectType: 1, accessType: 0x00, playbackType: 0, "00001");
            WriteTitleEntry(builder, objectType: 1, accessType: 0x00, playbackType: 0, "00002");
        }

        WriteTitleEntry(builder, objectType: 2, accessType: 0x00, playbackType: 0, "00000");
        builder.Reserved(paddedContentSize - indexesContentSize);
        return builder.ToArray();
    }

    private static void WriteTitleEntry(IndexBinaryBuilder builder, byte objectType, byte accessType, byte playbackType, string data)
    {
        var firstByte = (byte)((objectType << 6) | ((accessType & 0x03) << 4));
        builder.Byte(firstByte);
        builder.Reserved(3);
        builder.Byte((byte)((playbackType << 6) & 0xC0));
        builder.Byte(0);
        if (objectType == 1)
        {
            builder.UInt16BE(ushort.Parse(data));
            builder.Reserved(4);
            return;
        }

        var padded = data.PadRight(6, '\0');
        builder.Ascii(padded);
    }

    private sealed class IndexBinaryBuilder : IDisposable
    {
        private readonly MemoryStream stream = new();

        public IndexBinaryBuilder UInt32BE(uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            return this;
        }

        public IndexBinaryBuilder UInt16BE(ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            return this;
        }

        public IndexBinaryBuilder Byte(byte value)
        {
            stream.WriteByte(value);
            return this;
        }

        public IndexBinaryBuilder Reserved(int count)
        {
            stream.Write(new byte[count]);
            return this;
        }

        public IndexBinaryBuilder Ascii(string value)
        {
            stream.Write(Encoding.ASCII.GetBytes(value));
            return this;
        }

        public IndexBinaryBuilder SeekTo(int position)
        {
            stream.Position = position;
            return this;
        }

        public byte[] ToArray() => stream.ToArray();

        public void Dispose() => stream.Dispose();
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ChapterTool_Bdjo_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
