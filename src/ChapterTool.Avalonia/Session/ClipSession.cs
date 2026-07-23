using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Session;

/// <summary>
/// Typed multi-clip session: either split (selectable entries) or combined.
/// Replaces multi-flag state (splitClipGroup / combinedClipOption / belongs-to-selected).
/// </summary>
public abstract record ClipSession
{
    /// <summary>
    /// Stable identity for anti-stale append checks. Changes on load/combine/restore/append
    /// structural transitions; preserved across select and write-back.
    /// </summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>Original multi-entry group retained across combine/restore.</summary>
    public abstract ChapterImportSource OriginalGroup { get; }

    /// <summary>Entries shown in the clip selector.</summary>
    public abstract IReadOnlyList<ChapterImportEntry> ClipOptions { get; }

    /// <summary>Selected index within <see cref="ClipOptions"/>.</summary>
    public abstract int SelectedIndex { get; }

    /// <summary>Whether the session is in combined mode.</summary>
    public abstract bool IsCombined { get; }

    /// <summary>
    /// Whether combine (or restore-from-combine) is available.
    /// Derived from mode and entry formats — not a sticky boolean.
    /// </summary>
    public bool CanCombine =>
        IsCombined
        || (OriginalGroup.Entries.Count > 1
            && OriginalGroup.Entries[0].ChapterSet.ImportFormat is ChapterImportFormat.Mpls or ChapterImportFormat.DvdIfo
            && OriginalGroup.Entries.All(entry =>
                entry.ChapterSet.ImportFormat == OriginalGroup.Entries[0].ChapterSet.ImportFormat));

    /// <summary>Whether append-MPLS is available for the original group.</summary>
    public bool CanAppendMpls =>
        OriginalGroup.Entries.Any(static entry => entry.ChapterSet.ImportFormat == ChapterImportFormat.Mpls);

    /// <summary>Chapter set of the currently selected clip option, if any.</summary>
    public ChapterSet? CurrentChapterSet =>
        SelectedIndex >= 0 && SelectedIndex < ClipOptions.Count
            ? ClipOptions[SelectedIndex].ChapterSet
            : null;

    /// <summary>Media references for the selected clip option.</summary>
    public IReadOnlyList<ReferencedMediaFile> RelatedMedia =>
        SelectedIndex >= 0 && SelectedIndex < ClipOptions.Count
            ? ClipOptions[SelectedIndex].ReferencedMediaFiles ?? []
            : [];
}

/// <summary>Split / multi-clip mode with a selectable entry index.</summary>
public sealed record SplitClipSession(ChapterImportSource Group, int SelectedClipIndex) : ClipSession
{
    public override ChapterImportSource OriginalGroup => Group;

    public override IReadOnlyList<ChapterImportEntry> ClipOptions => Group.Entries;

    public override int SelectedIndex => SelectedClipIndex;

    public override bool IsCombined => false;
}

/// <summary>Combined mode retaining the original multi-entry group for restore.</summary>
public sealed record CombinedClipSession(
    ChapterImportSource OriginalMultiEntryGroup,
    ChapterImportEntry CombinedEntry) : ClipSession
{
    public override ChapterImportSource OriginalGroup => OriginalMultiEntryGroup;

    public override IReadOnlyList<ChapterImportEntry> ClipOptions => [CombinedEntry];

    public override int SelectedIndex => 0;

    public override bool IsCombined => true;
}

/// <summary>Result of a combine-or-restore transition.</summary>
public sealed record ClipCombineTransitionResult(
    ClipSession? Session,
    ChapterEditResult EditResult,
    bool Restored,
    bool Succeeded);

/// <summary>Result of an append transition.</summary>
public sealed record ClipAppendTransitionResult(
    CombinedClipSession? Session,
    ChapterEditResult EditResult,
    bool Succeeded);

/// <summary>Pure clip-session transitions (load, select, combine, restore, append, write-back).</summary>
public static class ClipSessionTransitions
{
    /// <summary>Creates a split session from a successful load group.</summary>
    public static SplitClipSession FromLoad(ChapterImportSource group)
    {
        if (group.Entries.Count == 0)
        {
            return new SplitClipSession(group, -1) { SessionId = Guid.NewGuid() };
        }

        var index = Math.Clamp(group.DefaultEntryIndex, 0, group.Entries.Count - 1);
        return new SplitClipSession(group, index) { SessionId = Guid.NewGuid() };
    }

    /// <summary>Selects a clip index within a split session (no-op if out of range or already selected).</summary>
    public static SplitClipSession Select(SplitClipSession session, int index)
    {
        if (index < 0 || index >= session.Group.Entries.Count || index == session.SelectedClipIndex)
        {
            return session;
        }

        // Preserve SessionId — selection is not a structural session replacement.
        return session with { SelectedClipIndex = index };
    }

    /// <summary>Selects a clip index; only split sessions change selection.</summary>
    public static ClipSession Select(ClipSession session, int index) =>
        session switch
        {
            SplitClipSession split => Select(split, index),
            _ => session
        };

    /// <summary>
    /// Toggles combine: split multi-clip → combined, or combined → restored split.
    /// On combine failure, returns diagnostics without a new session.
    /// </summary>
    public static ClipCombineTransitionResult ToggleCombine(ClipSession session)
    {
        if (session is CombinedClipSession combined)
        {
            return new ClipCombineTransitionResult(
                Restore(combined),
                new ChapterEditResult(combined.CombinedEntry.ChapterSet, []),
                Restored: true,
                Succeeded: true);
        }

        if (session is not SplitClipSession split)
        {
            return new ClipCombineTransitionResult(
                null,
                new ChapterEditResult(
                    EmptyChapterSet(),
                    [new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.NoSegments, "No chapter segments are available.")]),
                Restored: false,
                Succeeded: false);
        }

        var result = ChapterSegmentService.Combine(split.Group);
        if (result.Diagnostics.Count > 0)
        {
            return new ClipCombineTransitionResult(null, result, Restored: false, Succeeded: false);
        }

        var combinedEntry = CreateCombinedClipOption(split.Group, result.ChapterSet);
        return new ClipCombineTransitionResult(
            new CombinedClipSession(split.Group, combinedEntry) { SessionId = Guid.NewGuid() },
            result,
            Restored: false,
            Succeeded: true);
    }

    /// <summary>Restores a combined session to its original split group.</summary>
    public static SplitClipSession Restore(CombinedClipSession session)
    {
        if (session.OriginalGroup.Entries.Count == 0)
        {
            return new SplitClipSession(session.OriginalGroup, -1) { SessionId = Guid.NewGuid() };
        }

        var index = Math.Clamp(session.OriginalGroup.DefaultEntryIndex, 0, session.OriginalGroup.Entries.Count - 1);
        return new SplitClipSession(session.OriginalGroup, index) { SessionId = Guid.NewGuid() };
    }

    /// <summary>
    /// Appends an MPLS group into the original multi-entry group and transitions to combined mode.
    /// </summary>
    public static ClipAppendTransitionResult Append(ClipSession session, ChapterImportSource appended)
    {
        var baseGroup = session.OriginalGroup;
        var edit = ChapterSegmentService.Append(baseGroup, appended);
        if (edit.Diagnostics.Count > 0)
        {
            return new ClipAppendTransitionResult(null, edit, Succeeded: false);
        }

        var entries = baseGroup.Entries.ToList();
        entries.AddRange(appended.Entries);
        var appendedGroup = baseGroup with { Entries = entries };
        var combinedOption = CreateCombinedClipOption(appendedGroup, edit.ChapterSet);
        return new ClipAppendTransitionResult(
            new CombinedClipSession(appendedGroup, combinedOption) { SessionId = Guid.NewGuid() },
            edit,
            Succeeded: true);
    }

    /// <summary>
    /// Writes an edited chapter set back into the active clip ownership
    /// (selected split entry vs combined entry). Preserves <see cref="ClipSession.SessionId"/>.
    /// </summary>
    public static ClipSession WriteBack(ClipSession session, ChapterSet info) =>
        session switch
        {
            SplitClipSession split => WriteBackSplit(split, info),
            CombinedClipSession combined => combined with
            {
                CombinedEntry = combined.CombinedEntry with { ChapterSet = info }

                // SessionId preserved via record `with`
            },
            _ => session
        };

    private static SplitClipSession WriteBackSplit(SplitClipSession split, ChapterSet info)
    {
        var index = split.SelectedClipIndex;
        if (index < 0 || index >= split.Group.Entries.Count)
        {
            return split;
        }

        var entries = split.Group.Entries.ToList();
        entries[index] = entries[index] with { ChapterSet = info };

        // SessionId preserved via record `with`
        return split with { Group = split.Group with { Entries = entries } };
    }

    /// <summary>Creates the synthetic combined clip entry used by the selector.</summary>
    public static ChapterImportEntry CreateCombinedClipOption(ChapterImportSource sourceGroup, ChapterSet combinedInfo)
    {
        var mediaReferences = sourceGroup.Entries
            .SelectMany(static entry => entry.ReferencedMediaFiles ?? [])
            .Distinct()
            .ToArray();
        return new ChapterImportEntry(
            "combined",
            $"{combinedInfo.Title}__{combinedInfo.Chapters.Count}",
            combinedInfo,
            CanCombine: true,
            ReferencedMediaFiles: mediaReferences);
    }

    private static ChapterSet EmptyChapterSet() =>
        new(string.Empty, null, ChapterImportFormat.Unknown, 0, TimeSpan.Zero, []);
}
