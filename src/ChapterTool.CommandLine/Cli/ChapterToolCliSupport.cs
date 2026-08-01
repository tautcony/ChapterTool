using ChapterTool.Core.Exporting;
using DotMake.CommandLine;

namespace ChapterTool.CommandLine.Cli;

internal static class ChapterToolCliSupport
{
    private static readonly CliSettings ParseSettings = new()
    {
        EnableDefaultExceptionHandler = false
    };

    internal static int Run(IReadOnlyList<string> args)
    {
        var parsed = DotMake.CommandLine.Cli.Parse<ChapterToolRootCliCommand>([.. args], ParseSettings);
        return parsed.Run();
    }

    public static IReadOnlyList<CliOutputFormatDefinition> OutputFormats { get; } =
    [
        .. ChapterExportFormats.All
            .Select(static format => new CliOutputFormatDefinition(
                ChapterExportFormats.Code(format),
                format,
                ChapterExportFormats.Extension(format),
                ChapterExportFormats.Description(format)))
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

public sealed record CliOutputFormatDefinition(
    string Name,
    ChapterExportFormat Format,
    string FileExtension,
    string Description);
