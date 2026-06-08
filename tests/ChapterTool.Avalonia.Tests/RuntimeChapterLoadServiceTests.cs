using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Composition;

namespace ChapterTool.Avalonia.Tests;

public sealed class RuntimeChapterLoadServiceTests
{
    [Theory]
    [InlineData(".txt", "CHAPTER01=00:00:00.000\r\nCHAPTER01NAME=Intro\r\n")]
    [InlineData(".vtt", "WEBVTT\r\n\r\n00:00:00.000 --> 00:00:01.000\r\nIntro\r\n")]
    [InlineData(".cue", "FILE \"audio.flac\" WAVE\r\n  TRACK 01 AUDIO\r\n    TITLE \"Intro\"\r\n    INDEX 01 00:00:00\r\n")]
    public async Task RuntimeRoutesTextCueAndWebVttSources(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        try
        {
            var result = await CreateService().LoadAsync(path, CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Single(result.Groups.Single().Options.Single().ChapterInfo.Chapters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeRoutesXmlSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + ".xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,
            """
            <?xml version="1.0"?>
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Intro</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """);
        try
        {
            var result = await CreateService().LoadAsync(path, CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Single(result.Groups.Single().Options.Single().ChapterInfo.Chapters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".mp4", "NativeLibraryMissing")]
    [InlineData(".m4a", "NativeLibraryMissing")]
    [InlineData(".m4v", "NativeLibraryMissing")]
    public async Task RuntimeRoutesMp4FamilyToNativeReaderDiagnostics(string extension, string expectedCode)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        await File.WriteAllBytesAsync(path, [0]);
        try
        {
            var result = await CreateService().LoadAsync(path, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedSource");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(".mkv", "MatroskaMissingDependency")]
    [InlineData(".mka", "MatroskaMissingDependency")]
    public async Task RuntimeRoutesMatroskaFamilyToMkvextractDiagnostics(string extension, string expectedCode)
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0]);
        try
        {
            var result = await CreateService().LoadAsync(path, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedSource");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RuntimeRoutesBdmvDirectoryToBdmvImporter()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "BDMV", "PLAYLIST"));
        try
        {
            var result = await CreateService().LoadAsync(root, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MissingDependency");
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedSource");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeLoadsExistingIfoFixture()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "Time_Shift_Test", "[ifo_Sample]", "VTS_05_0.IFO");

        var result = await CreateService().LoadAsync(path, CancellationToken.None);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var info = result.Groups.Single().Options.Single().ChapterInfo;
        Assert.Equal("DVD", info.SourceType);
        Assert.Equal(7, info.Chapters.Count);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Time_Shift.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static IChapterLoadService CreateService() =>
        new AppCompositionRoot(settingsDirectory: Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N")))
            .CreateChapterLoadService();
}
