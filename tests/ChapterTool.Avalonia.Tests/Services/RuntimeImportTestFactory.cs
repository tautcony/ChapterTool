using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Avalonia.Tests.Services;

internal static class RuntimeImportTestFactory
{
    private static readonly string SettingsDirectory = Path.Combine(
        Path.GetTempPath(),
        "ChapterTool.Tests",
        "RuntimeImportTestFactory");

    private static readonly ChapterTimeFormatter Formatter = new();
    private static readonly IExternalToolLocator ToolLocator = new ExternalToolLocator(
        new ChapterToolSettingsStore(SettingsDirectory),
        PathSearchDirectories().ToList());
    private static readonly ProcessRunner ProcessRunner = new();
    private static readonly FfprobeMediaChapterReader MediaChapterReader = new(ToolLocator, ProcessRunner);
    private static readonly AtlMp4ChapterReader Mp4ChapterReader = new();
    private static readonly RuntimeChapterImporterRegistry Registry = new(
        Formatter,
        ToolLocator,
        ProcessRunner,
        MediaChapterReader,
        Mp4ChapterReader);
    private static readonly RuntimeChapterLoadService LoadService = new(Registry);

    public static RuntimeChapterImporterRegistry CreateRegistry() => Registry;

    public static IChapterLoadService CreateLoadService() => LoadService;

    private static IEnumerable<string> PathSearchDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var part in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}
