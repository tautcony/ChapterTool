using System.Globalization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Command-line inspect workflow for listing import groups and diagnostics.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
    public async Task<int> InspectAsync(CliInspectRequest request, CancellationToken cancellationToken)
    {
        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderFailure(localizer.GetString("Cli.Error.ImportFailed"), import.Result.Diagnostics);
            return 1;
        }

        RenderInspect(import, request.InputPath);
        return 0;
    }

    private void RenderInspect(CliImportExecution import, string inputPath)
    {
        console.WriteLine($"{localizer.GetString("Cli.Header.Source")}: {Path.GetFullPath(inputPath)}");
        console.WriteLine($"{localizer.GetString("Cli.Header.Importer")}: {import.Importer.Id}");
        console.WriteLine($"{localizer.GetString("Cli.Header.Groups")}: {import.Result.Groups.Count}");
        RenderGroups(import.Result.Groups);
        RenderDiagnostics(import.Result.Diagnostics);
    }

    private void RenderGroups(IReadOnlyList<ChapterImportSource> groups)
    {
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            console.WriteLine();
            console.WriteLine($"[{groupIndex}] {Path.GetFileName(group.SourcePath)}");
            foreach (var optionLine in DescribeGroup(group))
            {
                console.WriteLine(optionLine);
            }
        }
    }

    private void RenderDiagnostics(IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        console.WriteLine();
        console.WriteLine(localizer.GetString("Cli.Header.Diagnostics"));
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteLine($"  {line}");
        }
    }

    private static IEnumerable<string> DescribeGroup(ChapterImportSource group)
    {
        for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
        {
            var entry = group.Entries[entryIndex];
            var defaultMarker = entryIndex == group.DefaultEntryIndex ? " default" : string.Empty;
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"  ({entryIndex}) id={entry.Id} name=\"{entry.DisplayName}\" chapters={entry.ChapterSet.Chapters.Count(static chapter => !chapter.IsSeparator)} fps={entry.ChapterSet.FramesPerSecond:0.###}{defaultMarker}");
        }
    }
}
