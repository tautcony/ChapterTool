using ChapterTool.Core.Exporting;
using DotMake.CommandLine;

namespace ChapterTool.Avalonia.Cli;

internal static class ChapterToolCliSupport
{
    private static readonly CliSettings ParseSettings = new()
    {
        EnableDefaultExceptionHandler = false
    };

    public static CliLaunchPlan AnalyzeLaunch(IReadOnlyList<string> args)
    {
        var parsed = DotMake.CommandLine.Cli.Parse<ChapterToolRootCliCommand>([.. args], ParseSettings);
        if (parsed.IsCalled<LoadCliCommand>())
        {
            var startupPath = parsed.BindCalled() is LoadCliCommand command
                ? CliInputResolver.Resolve(command.Input, command.Source)
                : null;
            return CliLaunchPlan.Gui(startupPath);
        }

        if (!parsed.IsCalled<ConvertCliCommand>()
            && !parsed.IsCalled<InspectCliCommand>()
            && !parsed.IsCalled<FormatsCliCommand>()
            && parsed.Bind<ChapterToolRootCliCommand>() is ChapterToolRootCliCommand inputCommand
            && inputCommand.Input.Length > 0
            && IsExistingPath(inputCommand.Input))
        {
            return CliLaunchPlan.Gui(inputCommand.Input);
        }

        var shouldRunCli = parsed.IsCalled<ConvertCliCommand>()
            || parsed.IsCalled<InspectCliCommand>()
            || parsed.IsCalled<FormatsCliCommand>()
            || parsed.HasTokens;
        return shouldRunCli ? CliLaunchPlan.Cli(parsed) : CliLaunchPlan.None;
    }

    private static bool IsExistingPath(string value) => File.Exists(value) || Directory.Exists(value);

    public static IReadOnlyList<CliOutputFormatDefinition> OutputFormats { get; } =
    [
        new("txt", ChapterExportFormat.Txt, ".txt", "OGM chapter pairs"),
        new("xml", ChapterExportFormat.Xml, ".xml", "Matroska chapter XML"),
        new("qpf", ChapterExportFormat.Qpfile, ".qpf", "QPFile keyframe list"),
        new("timecodes", ChapterExportFormat.TimeCodes, ".TimeCodes.txt", "Chapter start times only"),
        new("tsmuxer", ChapterExportFormat.TsMuxerMeta, ".TsMuxeR_Meta.txt", "tsMuxeR meta chapter list"),
        new("cue", ChapterExportFormat.Cue, ".cue", "CUE sheet"),
        new("json", ChapterExportFormat.Json, ".json", "Structured JSON chapter payload"),
        new("vtt", ChapterExportFormat.WebVtt, ".vtt", "WebVTT cue list"),
        new("celltimes", ChapterExportFormat.Celltimes, ".txt", "Celltimes frame list"),
        new("chapter2qpf", ChapterExportFormat.Chapter2Qpfile, ".qpf", "OGM-to-QPFile conversion")
    ];

    public static bool TryParseFormat(string value, out CliOutputFormatDefinition definition)
    {
        var match = OutputFormats.FirstOrDefault(format =>
            string.Equals(format.Name, value, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            definition = OutputFormats[0];
            return false;
        }

        definition = match;
        return true;
    }
}

internal sealed record CliLaunchPlan(bool LaunchGui, string? GuiStartupPath, CliRunnableResult? CliResult)
{
    public static CliLaunchPlan None { get; } = new(false, null, null);

    public static CliLaunchPlan Gui(string? startupPath) => new(true, startupPath, null);

    public static CliLaunchPlan Cli(CliRunnableResult result) => new(false, null, result);
}

public sealed record CliOutputFormatDefinition(
    string Name,
    ChapterExportFormat Format,
    string FileExtension,
    string Description);
