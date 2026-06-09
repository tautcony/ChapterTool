using System.Buffers.Binary;
using System.Text;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Cue;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class CueImporterTests
{
    [Fact]
    public async Task CueImporterReadsExistingSample()
    {
        var importer = new CueChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Cue", "ARCHIVES 2.cue")),
            CancellationToken.None);

        Assert.True(result.Success);
        var info = result.Groups.Single().Options.Single().ChapterInfo;
        Assert.Equal("CUE", info.SourceType);
        Assert.Equal("ARCHIVES 2.flac", info.SourceName);
        Assert.Equal("とある科学の超電磁砲 ARCHIVES 2", info.Title);
        Assert.Equal(4, info.Chapters.Count);
        Assert.Equal("初色bloomy [初春飾利(豊崎愛生)]", info.Chapters[1].Name);
        Assert.Equal(new TimeSpan(0, 0, 15, 19, 280), info.Chapters[1].Time);
    }

    [Fact]
    public async Task CueImporterReadsNonAsciiFixtureName()
    {
        var importer = new CueChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Cue", "のんのんびより りぴーと オリジナルサウンドトラック.cue")),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("のんのんバイオリン", result.Groups.Single().Options.Single().ChapterInfo.Chapters[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CueImporterReadsCopiedExampleFixture()
    {
        var importer = new CueChapterImporter();
        var result = await importer.ImportAsync(
            new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Cue", "example-cue-sheet-1.cue")),
            CancellationToken.None);

        Assert.True(result.Success);
        var info = result.Groups.Single().Options.Single().ChapterInfo;
        Assert.Equal("Back To Mine", info.Title);
        Assert.Equal("Orbital - Back To Mine.mp3", info.SourceName);
        Assert.Equal(19, info.Chapters.Count);
        Assert.Equal("John Barry & His Orchestra - The Knack [Orbital]", info.Chapters[0].Name);
        Assert.Equal("Robert Mellin Orchestra - The Adventures Of Robinson Crusoe [Orbital]", info.Chapters[^1].Name);
        Assert.Equal(new TimeSpan(0, 1, 11, 17, 707), info.Chapters[^1].Time);
    }

    [Theory]
    [MemberData(nameof(EncodedCueSheets))]
    public async Task CueImporterSupportsExpectedEncodings(byte[] bytes)
    {
        var importer = new CueChapterImporter();
        using var stream = new MemoryStream(bytes);

        var result = await importer.ImportAsync(new ChapterImportRequest("encoded.cue", stream), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public void CueParserSkipsBlankLinesBetweenTracksAsIntentionalExpansion()
    {
        var result = new CueSheetParser().Parse(
            """
            FILE "a.wav" WAVE
              TRACK 01 AUDIO
                TITLE "Track 1"
                INDEX 01 00:00:00

              TRACK 02 AUDIO
                TITLE "Track 2"
                INDEX 01 00:10:00
            """);

        Assert.True(result.Success);
        Assert.Equal(["Track 1", "Track 2"], result.Groups.Single().Options.Single().ChapterInfo.Chapters.Select(chapter => chapter.Name));
    }

    [Theory]
    [InlineData("", "EmptyCueFile")]
    [InlineData("TITLE \"x\"", "EmptyCueFile")]
    [InlineData("FILE \"a.wav\" WAVE\n  TRACK 01 AUDIO\n    TITLE \"x\"\n    INDEX 02 00:00:00", "MalformedCueSyntax")]
    [InlineData("FILE \"a.wav\" WAVE\n  TRACK 01 AUDIO\n    TITLE \"x\"\n    INDEX 01 bad", "MalformedCueSyntax")]
    public void CueParserFailsEmptyOrMalformedText(string text, string code)
    {
        var result = new CueSheetParser().Parse(text);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public async Task FlacImporterFailsInvalidHeader()
    {
        var result = await new FlacCueImporter().ImportAsync(
            new ChapterImportRequest("bad.flac", new MemoryStream("bad!"u8.ToArray())),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidContainerHeader");
    }

    [Fact]
    public async Task FlacImporterReadsVorbisCuesheetAndSkipsNativeCuesheetBlock()
    {
        var cue = MinimalCue();
        using var stream = new MemoryStream(CreateFlac(cue, includeNativeCueSheetBlock: true));

        var result = await new FlacCueImporter().ImportAsync(new ChapterImportRequest("music.flac", stream), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task FlacImporterAcceptsUppercaseVorbisCuesheetKeyAsIntentionalExpansion()
    {
        var cue = MinimalCue();
        using var stream = new MemoryStream(CreateFlac(cue, includeNativeCueSheetBlock: false, cueKey: "CUESHEET"));

        var result = await new FlacCueImporter().ImportAsync(new ChapterImportRequest("music.flac", stream), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task FlacImporterFailsMissingOrMalformedVorbisComment()
    {
        using var stream = new MemoryStream(CreateFlac(cue: null, includeNativeCueSheetBlock: false));

        var result = await new FlacCueImporter().ImportAsync(new ChapterImportRequest("music.flac", stream), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "EmbeddedCueNotFound");
    }

    [Fact]
    public async Task TakImporterFailsInvalidHeader()
    {
        var result = await new TakCueImporter().ImportAsync(
            new ChapterImportRequest("bad.tak", new MemoryStream("bad!"u8.ToArray())),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidContainerHeader");
    }

    [Fact]
    public async Task TakImporterExtractsMarkerUntilTerminator()
    {
        var cue = MinimalCue();
        var bytes = Encoding.UTF8.GetBytes("tBaKpaddingCUESHEET=" + cue + "\0\0\0\0\0\0trailer");
        using var stream = new MemoryStream(bytes);

        var result = await new TakCueImporter().ImportAsync(new ChapterImportRequest("music.tak", stream), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task TakImporterFindsUppercaseMarkerAfterNonAsciiPadding()
    {
        var cue = MinimalCue();
        var bytes = Encoding.UTF8.GetBytes("tBaK填充CUESHEET=" + cue + "\0\0\0\0\0\0trailer");
        using var stream = new MemoryStream(bytes);

        var result = await new TakCueImporter().ImportAsync(new ChapterImportRequest("music.tak", stream), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Track 1", result.Groups.Single().Options.Single().ChapterInfo.Chapters.Single().Name);
    }

    [Fact]
    public async Task TakImporterFailsMissingMarkerForSmallFile()
    {
        var bytes = Encoding.UTF8.GetBytes("tBaKsmall");
        using var stream = new MemoryStream(bytes);

        var result = await new TakCueImporter().ImportAsync(new ChapterImportRequest("music.tak", stream), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "EmbeddedCueNotFound");
    }

    [Fact]
    public void CueExporterWritesHeaderSkipsSeparatorsAndUsesOutputOrder()
    {
        var info = new ChapterInfo(
            "Album",
            "fallback.flac",
            0,
            "CUE",
            0,
            TimeSpan.FromSeconds(90),
            [
                new Chapter(7, TimeSpan.Zero, "A"),
                new Chapter(-1, Chapter.SeparatorTime, ""),
                new Chapter(3, TimeSpan.FromSeconds(65.5), "B")
            ]);
        var exporter = new ChapterExportService(new ChapterTimeFormatter(), new ExpressionService());

        var result = exporter.Export(info, new ChapterExportOptions(ChapterExportFormat.Cue, SourceFileName: "source.wav"));

        Assert.True(result.Success);
        Assert.StartsWith("REM Generate By ChapterTool", result.Content, StringComparison.Ordinal);
        Assert.Contains("FILE \"source.wav\" WAVE", result.Content, StringComparison.Ordinal);
        Assert.Contains("  TRACK 01 AUDIO", result.Content, StringComparison.Ordinal);
        Assert.Contains("  TRACK 02 AUDIO", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("TRACK 03", result.Content, StringComparison.Ordinal);
        Assert.Contains("    INDEX 01 01:05:38", result.Content, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> EncodedCueSheets()
    {
        var text = MinimalCue();
        yield return [Encoding.UTF8.GetBytes(text)];
        yield return [new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray()];
        yield return [new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(text)).ToArray()];
        yield return [new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray()];
    }

    private static string MinimalCue() =>
        """
        TITLE "Album"
        FILE "audio.flac" WAVE
          TRACK 01 AUDIO
            TITLE "Track 1"
            INDEX 01 00:00:00
        """;

    private static byte[] CreateFlac(string? cue, bool includeNativeCueSheetBlock, string cueKey = "cuesheet")
    {
        using var stream = new MemoryStream();
        stream.Write("fLaC"u8);
        if (includeNativeCueSheetBlock)
        {
            WriteBlock(stream, type: 5, isLast: false, [1, 2, 3, 4]);
        }

        WriteBlock(stream, type: 4, isLast: true, CreateVorbisBlock(cue, cueKey));
        return stream.ToArray();
    }

    private static byte[] CreateVorbisBlock(string? cue, string cueKey)
    {
        using var stream = new MemoryStream();
        WriteLittleEndianInt32(stream, 6);
        stream.Write("vendor"u8);
        WriteLittleEndianInt32(stream, cue is null ? 1 : 2);
        WriteVorbisComment(stream, "ARTIST=Someone");
        if (cue is not null)
        {
            WriteVorbisComment(stream, cueKey + "=" + cue);
        }

        return stream.ToArray();
    }

    private static void WriteVorbisComment(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        WriteLittleEndianInt32(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteLittleEndianInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteBlock(Stream stream, int type, bool isLast, byte[] data)
    {
        stream.WriteByte((byte)(type | (isLast ? 0x80 : 0)));
        stream.WriteByte((byte)((data.Length >> 16) & 0xff));
        stream.WriteByte((byte)((data.Length >> 8) & 0xff));
        stream.WriteByte((byte)(data.Length & 0xff));
        stream.Write(data);
    }
}
