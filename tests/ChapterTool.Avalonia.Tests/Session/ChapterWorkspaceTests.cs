using ChapterTool.Avalonia.Session;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Tests.Session;

public sealed class ChapterWorkspaceTests
{
    [Fact]
    public void TryCommitLoad_ReplacesPathAndSessionAtomically()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var session = ClipSessionTransitions.FromLoad(MultiMplsGroup());

        Assert.True(workspace.TryCommitLoad(revision, "/media/movie.mpls", session));
        Assert.Equal("/media/movie.mpls", workspace.CurrentPath);
        Assert.Equal("movie.mpls", workspace.DisplayPath);
        Assert.Same(session, workspace.ClipSession);
        Assert.Equal("A", workspace.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void TryCommitLoad_IgnoresStaleRevision()
    {
        var workspace = new ChapterWorkspace();
        var oldRevision = workspace.BeginLoadOperation();
        var newerRevision = workspace.BeginLoadOperation();
        var newerSession = ClipSessionTransitions.FromLoad(SingleGroup("fast.txt", "Fast"));
        Assert.True(workspace.TryCommitLoad(newerRevision, "fast.txt", newerSession));

        var staleSession = ClipSessionTransitions.FromLoad(SingleGroup("slow.txt", "Slow"));
        Assert.False(workspace.TryCommitLoad(oldRevision, "slow.txt", staleSession));

        Assert.Equal("fast.txt", workspace.CurrentPath);
        Assert.Equal("Fast", workspace.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void TryCommitAppend_RequiresMatchingSessionIdAndRevision()
    {
        var workspace = new ChapterWorkspace();
        var loadRevision = workspace.BeginLoadOperation();
        var baseSession = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        Assert.True(workspace.TryCommitLoad(loadRevision, "base.mpls", baseSession));

        var appendRevision = workspace.CaptureRevision();
        var expectedId = workspace.ClipSession!.SessionId;
        var appended = ClipSessionTransitions.Append(workspace.ClipSession, SingleGroup("append.mpls", "Append")).Session!;
        Assert.True(workspace.TryCommitAppend(appendRevision, expectedId, appended));
        Assert.True(workspace.ClipSession.IsCombined);
        Assert.Equal(3, workspace.ClipSession.OriginalGroup.Entries.Count);
    }

    [Fact]
    public void TryCommitAppend_RejectsAfterNewerLoad()
    {
        var workspace = new ChapterWorkspace();
        var loadRevision = workspace.BeginLoadOperation();
        var baseSession = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        Assert.True(workspace.TryCommitLoad(loadRevision, "base.mpls", baseSession));

        var appendRevision = workspace.CaptureRevision();
        var expectedId = workspace.ClipSession!.SessionId;
        var appendSession = ClipSessionTransitions.Append(workspace.ClipSession, SingleGroup("append.mpls", "Append")).Session!;

        var newerRevision = workspace.BeginLoadOperation();
        Assert.True(workspace.TryCommitLoad(newerRevision, "new.txt", ClipSessionTransitions.FromLoad(SingleGroup("new.txt", "New"))));

        Assert.False(workspace.TryCommitAppend(appendRevision, expectedId, appendSession));
        Assert.Equal("new.txt", workspace.CurrentPath);
        Assert.False(workspace.ClipSession!.IsCombined);
    }

    [Fact]
    public void WriteBack_UpdatesSelectedSplitEntry()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        Assert.True(workspace.TryCommitLoad(revision, "movie.mpls", ClipSessionTransitions.FromLoad(MultiMplsGroup())));
        workspace.SelectClip(1);

        var updated = workspace.CurrentChapterSet! with
        {
            Chapters = [new Chapter(1, TimeSpan.Zero, "Edited")]
        };
        workspace.WriteBackCurrentChapterSet(updated);

        var split = Assert.IsType<SplitClipSession>(workspace.ClipSession);
        Assert.Equal("Edited", split.Group.Entries[1].ChapterSet.Chapters[0].Name);
        Assert.Equal("A", split.Group.Entries[0].ChapterSet.Chapters[0].Name);
    }

    [Fact]
    public void CreateExportOptions_ReadsOwnedProjectionAndExportPreferences()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        Assert.True(workspace.TryCommitLoad(revision, "movie.mpls", ClipSessionTransitions.FromLoad(MultiMplsGroup())));

        workspace.ExportPreferences.SetFormat(ChapterExportFormat.Xml);
        workspace.ExportPreferences.SetXmlLanguage("eng");
        workspace.ExportPreferences.SetTextEncoding(OutputTextEncoding.Utf8);
        workspace.ExportPreferences.SetEmitBom(false);
        workspace.Projection.SetAutoGenerateNames(true);
        workspace.Projection.SetOrderShift(2);
        workspace.ApplyExpressionFields("t+1", applyExpression: true, "preset", "script.lua");

        var options = workspace.CreateExportOptions();

        Assert.Equal(ChapterExportFormat.Xml, options.Format);
        Assert.Equal("eng", options.XmlLanguage);
        Assert.Equal("00001", options.SourceFileName);
        Assert.True(options.AutoGenerateNames);
        Assert.True(options.ApplyExpression);
        Assert.Equal("t+1", options.Expression);
        Assert.Equal("preset", options.ExpressionPresetId);
        Assert.Equal("script.lua", options.ExpressionSourceName);
        Assert.Equal(2, options.OrderShift);
        Assert.False(options.EmitBom);

        var projected = workspace.CreateExportOptionsForProjectedInfo();

        Assert.False(projected.ApplyExpression);
        Assert.False(projected.AutoGenerateNames);
        Assert.False(projected.UseTemplateNames);
        Assert.Equal(0, projected.OrderShift);
        Assert.False(projected.ProjectOutput);
        Assert.Equal(ChapterExportFormat.Xml, projected.Format);
        Assert.Equal("eng", projected.XmlLanguage);
    }

    [Fact]
    public void ApplyExpressionFields_UpdatesProjectionAtomically()
    {
        var workspace = new ChapterWorkspace();
        workspace.ApplyExpressionFields("t*2", applyExpression: true, "id-1", "batch.lua");

        Assert.Equal("t*2", workspace.Projection.Expression);
        Assert.True(workspace.Projection.ApplyExpression);
        Assert.Equal("id-1", workspace.Projection.ExpressionPresetId);
        Assert.Equal("batch.lua", workspace.Projection.ExpressionSourceName);

        var options = workspace.CreateExportOptions();
        Assert.Equal("t*2", options.Expression);
        Assert.True(options.ApplyExpression);
    }

    [Fact]
    public void NamingModes_AreMutuallyExclusiveOnProjectionState()
    {
        var workspace = new ChapterWorkspace();
        Assert.True(workspace.Projection.SetAutoGenerateNames(true));
        Assert.True(workspace.Projection.AutoGenerateNames);
        Assert.False(workspace.Projection.UseTemplateNames);

        Assert.True(workspace.Projection.SetUseTemplateNames(true));
        Assert.True(workspace.Projection.UseTemplateNames);
        Assert.False(workspace.Projection.AutoGenerateNames);
    }

    [Fact]
    public void LastSuccessfulExpressionProjection_RetainedUntilCleared()
    {
        var workspace = new ChapterWorkspace();
        var projection = new ChapterOutputProjectionResult(
            new ChapterSet("t", "s", ChapterImportFormat.Ogm, 25, TimeSpan.FromSeconds(1), [new Chapter(1, TimeSpan.Zero, "X")]),
            []);
        workspace.LastSuccessfulExpressionProjection = projection;
        Assert.Same(projection, workspace.LastSuccessfulExpressionProjection);
        Assert.Same(projection, workspace.Projection.LastSuccessfulExpressionProjection);

        workspace.ClearProjectionCache();
        Assert.Null(workspace.LastSuccessfulExpressionProjection);
        Assert.Null(workspace.Projection.LastSuccessfulExpressionProjection);
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

    private static ChapterImportSource SingleGroup(string path, string name) =>
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
