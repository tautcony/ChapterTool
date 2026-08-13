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
            return CliSelectionResult.Failure(localizer.GetString("Cli.Error.NoGroups"), []);
        }

        if (!TryResolveGroupIndex(groups, request, out var group, out var failure))
        {
            return failure;
        }

        if (group is null || group.Entries.Count == 0)
        {
            return CliSelectionResult.Failure(localizer.Format("Cli.Error.EmptyGroup", new Dictionary<string, object?> { ["group"] = request.GroupIndex ?? 0 }), []);
        }

        return ResolveEntryFromGroup(group, request);
    }

    private bool TryResolveGroupIndex(
        IReadOnlyList<ChapterImportSource> groups,
        CliConvertRequest request,
        out ChapterImportSource? group,
        out CliSelectionResult? failure)
    {
        if (request.GroupIndex is null)
        {
            if (groups.Count == 1)
            {
                group = groups[0];
                failure = null;
                return true;
            }

            group = null;
            failure = CliSelectionResult.Failure(
                localizer.GetString("Cli.Error.MultipleGroups"),
                AmbiguousSelectionDiagnostics(groups));
            return false;
        }

        if (request.GroupIndex < 0 || request.GroupIndex >= groups.Count)
        {
            group = null;
            failure = CliSelectionResult.Failure(
                localizer.Format("Cli.Error.GroupIndex", new Dictionary<string, object?> { ["group"] = request.GroupIndex }),
                AmbiguousSelectionDiagnostics(groups));
            return false;
        }

        group = groups[request.GroupIndex.Value];
        failure = null;
        return true;
    }

    private CliSelectionResult ResolveEntryFromGroup(ChapterImportSource group, CliConvertRequest request)
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
            localizer.Format("Cli.Error.MultipleEntries", new Dictionary<string, object?> { ["group"] = groupIndex }),
            AmbiguousSelectionDiagnostics([group], groupIndex));
    }

    private CliSelectionResult ResolveEntryById(ChapterImportSource group, string entryId, int groupIndex)
    {
        var entry = group.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entryId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return CliSelectionResult.Failure(
                localizer.Format("Cli.Error.EntryNotFound", new Dictionary<string, object?> { ["entry"] = entryId, ["group"] = groupIndex }),
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(entry);
    }

    private CliSelectionResult ResolveEntryByIndex(ChapterImportSource group, int entryIndex, int groupIndex)
    {
        if (entryIndex < 0 || entryIndex >= group.Entries.Count)
        {
            return CliSelectionResult.Failure(
                localizer.Format("Cli.Error.EntryIndex", new Dictionary<string, object?> { ["entry"] = entryIndex, ["group"] = groupIndex }),
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
