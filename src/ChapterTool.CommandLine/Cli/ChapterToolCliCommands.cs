using DotMake.CommandLine;

namespace ChapterTool.CommandLine;

[CliCommand(
    Description = "ChapterTool command-line workflows",
    Children = [typeof(LoadCliCommand), typeof(ConvertCliCommand), typeof(InspectCliCommand), typeof(FormatsCliCommand)])]
public sealed class ChapterToolRootCliCommand
{
    [CliArgument(Description = "Input file or supported source path for GUI startup.", Required = false)]
    public string Input { get; set; } = string.Empty;

    public int Run(CliContext context)
    {
        context.ShowHelp();
        return context.Result.HasTokens ? 1 : 0;
    }
}

[CliCommand(Parent = typeof(ChapterToolRootCliCommand), Description = "Launch the GUI and load a source path")]
public sealed class LoadCliCommand
{
    [CliArgument(Description = "Input file or supported source path", Required = false)]
    public string Input { get; set; } = string.Empty;

    [CliOption(Alias = "-i", Description = "Input file or supported source path.", Required = false)]
    public string? Source { get; set; }

    public int Run()
    {
        return 0;
    }
}

[CliCommand(Parent = typeof(ChapterToolRootCliCommand), Description = "Convert a chapter source into another format")]
public sealed class ConvertCliCommand
{
    [CliArgument(Description = "Input file or supported source path", Required = false)]
    public string Input { get; set; } = string.Empty;

    [CliOption(Alias = "-i", Description = "Input file or supported source path.", Required = false)]
    public string? Source { get; set; }

    [CliOption(
        Description = "Output format. Run `formats` to see the supported values.",
        Required = false,
        AllowedValues = ["txt", "xml", "qpf", "timecodes", "tsmuxer", "cue", "json", "vtt", "celltimes"])]
    public string Format { get; set; } = "txt";

    [CliOption(Description = "Output file path. If omitted, ChapterTool writes next to the input file.", Required = false)]
    public string? Output { get; set; }

    [CliOption(Alias = "-s", Description = "Write converted content to stdout instead of a file.", Required = false)]
    public bool Stdout { get; set; }

    [CliOption(Description = "Imported group index to use when the source exposes multiple groups.", Required = false)]
    public int? GroupIndex { get; set; }

    [CliOption(Description = "Imported entry index to use inside the selected group.", Required = false)]
    public int? EntryIndex { get; set; }

    [CliOption(Description = "Imported entry id to use inside the selected group.", Required = false)]
    public string? EntryId { get; set; }

    [CliOption(Description = "Chapter language code for XML export.", Required = false)]
    public string? XmlLanguage { get; set; }

    [CliOption(Description = "Source file name to embed in CUE export.", Required = false)]
    public string? SourceFileName { get; set; }

    [CliOption(Description = "Override frame rate for frame-based exports.", Required = false)]
    public double? FrameRate { get; set; }

    [CliOption(Description = "Lua expression used to transform chapter times before export.", Required = false)]
    public string? Expression { get; set; }

    [CliOption(Description = "Built-in expression preset id used to transform chapter times before export.", Required = false)]
    public string? ExpressionPreset { get; set; }

    public async Task<int> RunAsync()
    {
        var app = new ChapterToolCliApplication();
        return await app.ConvertAsync(
            new CliConvertRequest(
                CliInputResolver.Resolve(Input, Source) ?? string.Empty,
                Format,
                Output,
                Stdout,
                GroupIndex,
                EntryIndex,
                EntryId,
                XmlLanguage,
                SourceFileName,
                FrameRate,
                Expression,
                ExpressionPreset),
            CancellationToken.None);
    }
}

[CliCommand(Parent = typeof(ChapterToolRootCliCommand), Description = "Inspect available chapter groups, entries, and diagnostics")]
public sealed class InspectCliCommand
{
    [CliArgument(Description = "Input file or supported source path", Required = false)]
    public string Input { get; set; } = string.Empty;

    [CliOption(Alias = "-i", Description = "Input file or supported source path.", Required = false)]
    public string? Source { get; set; }

    public async Task<int> RunAsync()
    {
        var app = new ChapterToolCliApplication();
        return await app.InspectAsync(
            new CliInspectRequest(CliInputResolver.Resolve(Input, Source) ?? string.Empty),
            CancellationToken.None);
    }
}

[CliCommand(Parent = typeof(ChapterToolRootCliCommand), Description = "List CLI-supported input and output formats")]
public sealed class FormatsCliCommand
{
    public int Run()
    {
        var app = new ChapterToolCliApplication();
        return app.ShowFormats();
    }
}
