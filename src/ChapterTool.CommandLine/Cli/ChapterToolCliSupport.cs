using ChapterTool.Core.Exporting;
using DotMake.CommandLine;

namespace ChapterTool.CommandLine.Cli;

internal static class ChapterToolCliSupport
{
    private static readonly CliSettings ParseSettings = new()
    {
        EnableDefaultExceptionHandler = false
    };

    internal static CliLaunchPlan AnalyzeLaunch(IReadOnlyList<string> args)
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
            && parsed.Bind<ChapterToolRootCliCommand>() is { Input.Length: > 0 } inputCommand
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

    internal static CliLaunchPlan AnalyzeDesktopLaunch(IReadOnlyList<string> args) => AnalyzeLaunch(args);

    internal static int Run(IReadOnlyList<string> args)
    {
        var parsed = DotMake.CommandLine.Cli.Parse<ChapterToolRootCliCommand>([.. args], ParseSettings);
        if (parsed.IsCalled<LoadCliCommand>())
        {
            Console.Error.WriteLine("The `load` command is available only in the Avalonia host. Use an explicit CLI command such as `inspect` or `convert`.");
            return 1;
        }

        return parsed.Run();
    }

    private static bool IsExistingPath(string value) => File.Exists(value) || Directory.Exists(value);

    public static IReadOnlyList<CliOutputFormatDefinition> OutputFormats { get; } =
        ChapterExportFormats.All
            .Select(static format => new CliOutputFormatDefinition(
                ChapterExportFormats.Code(format),
                format,
                ChapterExportFormats.Extension(format),
                ChapterExportFormats.Description(format)))
            .ToArray();

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
