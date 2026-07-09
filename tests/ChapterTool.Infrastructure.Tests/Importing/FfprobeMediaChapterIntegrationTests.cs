using ChapterTool.Core.Models;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Infrastructure.Tests.Importing;

public sealed class FfprobeMediaChapterIntegrationTests
{
    [Theory]
    [MemberData(nameof(ChapteredContainerFixtures))]
    public async Task FfprobeReaderAndMediaImporterReadRealChapteredContainer(
        string fileName,
        TimeSpan expectedDuration,
        string[] expectedNames,
        TimeSpan[] expectedStarts,
        TimeSpan?[] expectedEnds)
    {
        var fixturePath = FixtureResolver.Fixture("Importing", "Media", fileName);
        var ffprobe = await LocateFfprobeAsync();
        var importer = new MediaChapterImporter(new FfprobeMediaChapterReader(
            new SingleToolLocator(ffprobe),
            new ProcessRunner()));

        var result = await importer.ImportAsync(new ChapterImportRequest(fixturePath), TestContext.Current.CancellationToken);

        Assert.True(result.Success, Diagnostics(result));
        var entry = result.Groups.Single().Entries.Single();
        var info = entry.ChapterSet;
        Assert.Equal(ChapterImportFormat.Media, info.ImportFormat);
        Assert.Equal("FFprobe Chapters", entry.DisplayName);
        Assert.Equal(expectedDuration, info.Duration);
        Assert.Equal(expectedNames, info.Chapters.Select(static chapter => chapter.Name));
        Assert.Equal(expectedStarts, info.Chapters.Select(static chapter => chapter.StartTime));
        Assert.Equal(expectedEnds, info.Chapters.Select(static chapter => chapter.EndTime));
    }

    public static TheoryData<string, TimeSpan, string[], TimeSpan[], TimeSpan?[]> ChapteredContainerFixtures() => new()
    {
        {
            "Chapter.mp4",
            TimeSpan.FromSeconds(29.15),
            ["Chapter 01", "Chapter 02", "Chapter 03", "Chapter 04"],
            [TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)],
            [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(29.15), null]
        },
        {
            "Chapter.mkv",
            TimeSpan.FromSeconds(775),
            ["Intro", "Act 1", "Act 2", "Credits"],
            [TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(330), TimeSpan.FromSeconds(740)],
            [TimeSpan.FromSeconds(29.15), null, null, TimeSpan.FromSeconds(775)]
        },
        {
            "Chapter.flac",
            TimeSpan.FromMilliseconds(17947),
            ["Intro", "Sweep", "Tone"],
            [TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12)],
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), TimeSpan.FromMilliseconds(17947)]
        }
    };

    private static async ValueTask<ExternalToolLocation> LocateFfprobeAsync()
    {
        var searchDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var locator = new ExternalToolLocator(new EmptySettingsStore(), searchDirectories, new EmptyMkvToolNixInstallProbe());
        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);
        Assert.True(location.Found, location.Message ?? "External tool 'ffprobe' was not found.");
        return location;
    }

    private static string Diagnostics(ChapterImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} {diagnostic.Details}"));

    private sealed class SingleToolLocator(ExternalToolLocation location) : IExternalToolLocator
    {
        public ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
        {
            Assert.Equal("ffprobe", toolId);
            return ValueTask.FromResult(location);
        }
    }

    private sealed class EmptySettingsStore : ISettingsStore<Configuration.AppSettings>
    {
        public ValueTask<Configuration.AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new Configuration.AppSettings());

        public ValueTask SaveAsync(Configuration.AppSettings settings, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class EmptyMkvToolNixInstallProbe : IMkvToolNixInstallProbe
    {
        public IEnumerable<string> FindMkvExtractCandidates(string executableName) => [];
    }
}
