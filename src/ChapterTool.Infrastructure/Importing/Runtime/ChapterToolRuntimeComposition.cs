using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Infrastructure.Importing.Runtime;

public static class ChapterToolRuntimeComposition
{
    public static string ResolveSettingsDirectory(string? settingsDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            return settingsDirectory;
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.CurrentDirectory, "settings")
            : Path.Combine(root, "ChapterTool");
    }

    public static ChapterToolSettingsStore CreateSettingsStore(string? settingsDirectory = null) =>
        new(ResolveSettingsDirectory(settingsDirectory));

    public static RuntimeChapterImporterRegistry CreateImporterRegistry(
        ISettingsStore<ChapterToolSettings> settingsStore,
        IChapterTimeFormatter? formatter = null,
        IExternalToolLocator? toolLocator = null,
        IProcessRunner? processRunner = null,
        IMediaChapterReader? mediaChapterReader = null,
        IMediaChapterReader? mp4FallbackChapterReader = null)
    {
        formatter ??= new ChapterTimeFormatter();
        toolLocator ??= new ExternalToolLocator(settingsStore, PathSearchDirectories().ToList());
        processRunner ??= new ProcessRunner();
        return new RuntimeChapterImporterRegistry(
            formatter,
            toolLocator,
            processRunner,
            mediaChapterReader ?? new FfprobeMediaChapterReader(toolLocator, processRunner),
            mp4FallbackChapterReader ?? new AtlMp4ChapterReader());
    }

    public static ChapterExportService CreateExportService(IChapterExpressionEngine? expressionEngine = null) =>
        new(new ChapterTimeFormatter(), expressionEngine ?? new LuaExpressionScriptService());

    public static IMediaChapterReader CreateMp4ChapterReader() => new AtlMp4ChapterReader();

    public static IEnumerable<string> PathSearchDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var part in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}
