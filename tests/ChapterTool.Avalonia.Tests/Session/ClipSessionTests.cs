using ChapterTool.Avalonia.Session;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Tests.Session;

public sealed class ClipSessionTests
{
    [Fact]
    public void FromLoad_CreatesSplitSessionWithDefaultIndex()
    {
        var group = MultiMplsGroup();
        var session = ClipSessionTransitions.FromLoad(group);

        Assert.IsType<SplitClipSession>(session);
        Assert.False(session.IsCombined);
        Assert.Equal(0, session.SelectedIndex);
        Assert.Equal(2, session.ClipOptions.Count);
        Assert.True(session.CanCombine);
        Assert.True(session.CanAppendMpls);
        Assert.Equal("A", session.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void Select_UpdatesSelectedEntryWithoutChangingSessionId()
    {
        var session = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var sessionId = session.SessionId;

        var selected = ClipSessionTransitions.Select(session, 1);

        Assert.Equal(1, selected.SelectedIndex);
        Assert.Equal("C", selected.CurrentChapterSet?.Chapters[0].Name);
        Assert.Equal(sessionId, selected.SessionId);
        Assert.False(selected.IsCombined);
    }

    [Fact]
    public void ToggleCombine_EntersCombinedModeRetainingOriginalGroup()
    {
        var group = MultiMplsGroup();
        var split = ClipSessionTransitions.FromLoad(group);

        var result = ClipSessionTransitions.ToggleCombine(split);

        Assert.True(result.Succeeded);
        Assert.False(result.Restored);
        var combined = Assert.IsType<CombinedClipSession>(result.Session);
        Assert.True(combined.IsCombined);
        Assert.Single(combined.ClipOptions);
        Assert.Equal(4, combined.CurrentChapterSet?.Chapters.Count);
        Assert.Same(group, combined.OriginalGroup);
        Assert.NotEqual(split.SessionId, combined.SessionId);
        Assert.True(combined.CanCombine);
    }

    [Fact]
    public void ToggleCombine_OnCombined_RestoresSplit()
    {
        var split = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(split).Session);

        var result = ClipSessionTransitions.ToggleCombine(combined);

        Assert.True(result.Succeeded);
        Assert.True(result.Restored);
        var restored = Assert.IsType<SplitClipSession>(result.Session);
        Assert.False(restored.IsCombined);
        Assert.Equal(2, restored.ClipOptions.Count);
        Assert.Equal(0, restored.SelectedIndex);
        Assert.Equal("A", restored.CurrentChapterSet?.Chapters[0].Name);
        Assert.NotEqual(combined.SessionId, restored.SessionId);
    }

    [Fact]
    public void Append_ExpandsOriginalGroupAndEntersCombinedMode()
    {
        var baseSession = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var appended = SingleMplsGroup("append.mpls", "Append");

        var result = ClipSessionTransitions.Append(baseSession, appended);

        Assert.True(result.Succeeded);
        var combined = Assert.IsType<CombinedClipSession>(result.Session);
        Assert.True(combined.IsCombined);
        Assert.Equal(3, combined.OriginalGroup.Entries.Count);
        Assert.Equal(5, combined.CurrentChapterSet?.Chapters.Count);
        Assert.NotEqual(baseSession.SessionId, combined.SessionId);
    }

    [Fact]
    public void Append_FromCombined_UsesOriginalGroupNotDisplayEntry()
    {
        var split = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(split).Session);
        var appended = SingleMplsGroup("append.mpls", "Append");

        var result = ClipSessionTransitions.Append(combined, appended);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Session!.OriginalGroup.Entries.Count);
    }

    [Fact]
    public void WriteBack_OnSplit_UpdatesSelectedEntryOwnership()
    {
        var session = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        session = ClipSessionTransitions.Select(session, 1);
        var sessionId = session.SessionId;
        var updated = session.CurrentChapterSet! with
        {
            Chapters = [new Chapter(1, TimeSpan.Zero, "Edited")]
        };

        var written = Assert.IsType<SplitClipSession>(ClipSessionTransitions.WriteBack(session, updated));

        Assert.Equal(sessionId, written.SessionId);
        Assert.Equal(1, written.SelectedIndex);
        Assert.Equal("Edited", written.Group.Entries[1].ChapterSet.Chapters[0].Name);
        Assert.Equal("A", written.Group.Entries[0].ChapterSet.Chapters[0].Name);
    }

    [Fact]
    public void WriteBack_OnCombined_UpdatesCombinedEntry()
    {
        var split = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(split).Session);
        var sessionId = combined.SessionId;
        var updated = combined.CurrentChapterSet! with
        {
            Chapters = [new Chapter(1, TimeSpan.Zero, "CombinedEdit")]
        };

        var written = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.WriteBack(combined, updated));

        Assert.Equal(sessionId, written.SessionId);
        Assert.Equal("CombinedEdit", written.CombinedEntry.ChapterSet.Chapters[0].Name);
        Assert.Equal(2, written.OriginalGroup.Entries.Count);
    }

    [Fact]
    public void FromLoad_ClearsPreviousCombinedIdentity()
    {
        var first = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var combined = Assert.IsType<CombinedClipSession>(ClipSessionTransitions.ToggleCombine(first).Session);
        Assert.True(combined.IsCombined);

        var second = ClipSessionTransitions.FromLoad(SingleMplsGroup("other.mpls", "Other"));

        Assert.IsType<SplitClipSession>(second);
        Assert.False(second.IsCombined);
        Assert.NotEqual(combined.SessionId, second.SessionId);
        Assert.Single(second.ClipOptions);
        Assert.Equal("Other", second.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void SingleEntryLoad_CannotCombine()
    {
        var session = ClipSessionTransitions.FromLoad(SingleMplsGroup("solo.mpls", "Solo"));

        Assert.False(session.CanCombine);
        Assert.True(session.CanAppendMpls);
        Assert.False(session.IsCombined);
    }

    private static ChapterImportSource MultiMplsGroup() =>
        new(
            "movie.mpls",
            [
                new ChapterImportEntry(
                    "clip-0",
                    "00001",
                    Info(ChapterImportFormat.Mpls, "00001",
                        new Chapter(1, TimeSpan.Zero, "A"),
                        new Chapter(2, TimeSpan.FromSeconds(10), "B"))),
                new ChapterImportEntry(
                    "clip-1",
                    "00002",
                    Info(ChapterImportFormat.Mpls, "00002",
                        new Chapter(1, TimeSpan.Zero, "C"),
                        new Chapter(2, TimeSpan.FromSeconds(5), "D")))
            ]);

    private static ChapterImportSource SingleMplsGroup(string path, string name) =>
        new(
            path,
            [
                new ChapterImportEntry(
                    "clip-0",
                    name,
                    Info(ChapterImportFormat.Mpls, name, new Chapter(1, TimeSpan.Zero, name)))
            ]);

    private static ChapterSet Info(ChapterImportFormat format, string sourceName, params Chapter[] chapters) =>
        new(
            sourceName,
            sourceName,
            format,
            24000d / 1001d,
            chapters.Length == 0 ? TimeSpan.Zero : chapters[^1].StartTime + TimeSpan.FromSeconds(1),
            chapters);
}
