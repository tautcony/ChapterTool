using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Models;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Command-line group and entry selection helpers.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
    private CliSelectionResult? SelectOption(IReadOnlyList<ChapterImportSource> groups, CliConvertRequest request)
    {
        if (groups.Count == 0)
        {
            return CliSelectionResult.Failure("No chapter groups were imported.", []);
        }

        if (!TryResolveGroupIndex(groups, request, out var group, out var failure))
        {
            return failure;
        }

        if (group is null || group.Entries.Count == 0)
        {
            return CliSelectionResult.Failure($"Group {request.GroupIndex ?? 0} contains no selectable chapter entries.", []);
        }

        return ResolveEntryFromGroup(group, request);
    }

    private static bool TryResolveGroupIndex(
        IReadOnlyList<ChapterImportSource> groups,
        CliConvertRequest request,
        out ChapterImportSource? group,
        out CliSelectionResult? failure)
    {
        var groupIndex = request.GroupIndex ?? (groups.Count == 1 ? 0 : null);
        if (groupIndex is null || groupIndex < 0 || groupIndex >= groups.Count)
        {
            group = null;
            failure = CliSelectionResult.Failure(
                "Multiple groups are available. Specify --group-index to select one.",
                AmbiguousSelectionDiagnostics(groups));
            return false;
        }

        group = groups[groupIndex.Value];
        failure = null;
        return true;
    }

    private static CliSelectionResult ResolveEntryFromGroup(ChapterImportSource group, CliConvertRequest request)
    {
        var groupIndex = request.GroupIndex ?? 0;

        if (!string.IsNullOrWhiteSpace(request.EntryId))
        {
            return ResolveEntryById(group, request.EntryId, groupIndex);
        }

        if (request.EntryIndex is not null)
        {
            return ResolveEntryByIndex(group, request.EntryIndex.Value, groupIndex);
        }

        if (group.Entries.Count == 1)
        {
            return CliSelectionResult.Success(group.Entries[0]);
        }

        return CliSelectionResult.Failure(
            $"Group {groupIndex} has multiple entries. Specify --entry-id or --entry-index.",
            AmbiguousSelectionDiagnostics([group], groupIndex));
    }

    private static CliSelectionResult ResolveEntryById(ChapterImportSource group, string entryId, int groupIndex)
    {
        var entry = group.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entryId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return CliSelectionResult.Failure(
                $"Entry id '{entryId}' was not found in group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(entry);
    }

    private static CliSelectionResult ResolveEntryByIndex(ChapterImportSource group, int entryIndex, int groupIndex)
    {
        if (entryIndex < 0 || entryIndex >= group.Entries.Count)
        {
            return CliSelectionResult.Failure(
                $"Entry index {entryIndex} is out of range for group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(group.Entries[entryIndex]);
    }

    private static IReadOnlyList<ChapterDiagnostic> AmbiguousSelectionDiagnostics(IReadOnlyList<ChapterImportSource> groups, int groupOffset = 0)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        for (var localGroupIndex = 0; localGroupIndex < groups.Count; localGroupIndex++)
        {
            var group = groups[localGroupIndex];
            var groupIndex = localGroupIndex + groupOffset;
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.SelectionGroupAvailable,
                $"group={groupIndex} default-entry-index={group.DefaultEntryIndex} source={group.SourcePath}"));
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.SelectionOptionAvailable,
                    $"group={groupIndex} entry-index={entryIndex} entry-id={entry.Id} name={entry.DisplayName}"));
            }
        }

        return diagnostics;
    }
}
