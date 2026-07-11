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
    public void CreateExportOptions_UsesWorkspaceSourceNameAndPreferenceInputs()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        Assert.True(workspace.TryCommitLoad(revision, "movie.mpls", ClipSessionTransitions.FromLoad(MultiMplsGroup())));

        var options = workspace.CreateExportOptions(new ExportPreferenceInputs(
            Format: ChapterExportFormat.Xml,
            XmlLanguage: "eng",
            AutoGenerateNames: true,
            UseTemplateNames: false,
            ChapterNameTemplateText: string.Empty,
            OrderShift: 2,
            ApplyExpression: true,
            Expression: "t+1",
            ExpressionPresetId: "preset",
            ExpressionSourceName: "script.lua",
            TextEncoding: OutputTextEncoding.Utf8,
            EmitBom: false));

        Assert.Equal(ChapterExportFormat.Xml, options.Format);
        Assert.Equal("eng", options.XmlLanguage);
        Assert.Equal("00001", options.SourceFileName);
        Assert.True(options.ApplyExpression);
        Assert.Equal("t+1", options.Expression);
        Assert.Equal(2, options.OrderShift);
        Assert.False(options.EmitBom);

        var projected = workspace.CreateExportOptionsForProjectedInfo(new ExportPreferenceInputs(
            Format: ChapterExportFormat.Xml,
            XmlLanguage: "eng",
            AutoGenerateNames: true,
            UseTemplateNames: true,
            ChapterNameTemplateText: "Ch%02d",
            OrderShift: 2,
            ApplyExpression: true,
            Expression: "t+1",
            ExpressionPresetId: "preset",
            ExpressionSourceName: "script.lua",
            TextEncoding: OutputTextEncoding.Utf8,
            EmitBom: false));

        Assert.False(projected.ApplyExpression);
        Assert.False(projected.AutoGenerateNames);
        Assert.False(projected.UseTemplateNames);
        Assert.Equal(0, projected.OrderShift);
        Assert.False(projected.ProjectOutput);
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

        workspace.ClearProjectionCache();
        Assert.Null(workspace.LastSuccessfulExpressionProjection);
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
