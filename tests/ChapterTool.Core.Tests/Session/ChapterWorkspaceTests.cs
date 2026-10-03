using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Session;

public sealed class ChapterWorkspaceTests
{
    [Fact]
    public void Ended_session_history_owner_is_collectible()
    {
        var weak = CreateEndedSessionReference();
        for (var attempt = 0; attempt < 3 && weak.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(weak.IsAlive);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateEndedSessionReference()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = SingleGroup("old.txt", "Old");
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("old.txt"), group, ClipSessionTransitions.FromLoad(group)));
        var session = workspace.ContentSession!;
        _ = session.GetHistorySnapshot();
        var weak = new WeakReference(session);
        workspace.ClearSession();
        return weak;
    }

    [Fact]
    public void TryCommitLoad_ReplacesPathAndSessionAtomically()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var session = ClipSessionTransitions.FromLoad(MultiMplsGroup());

        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("/media/movie.mpls"), MultiMplsGroup(), session));
        Assert.Equal("/media/movie.mpls", workspace.CurrentPath);
        Assert.Equal("movie.mpls", workspace.DisplayPath);
        Assert.Same(session, workspace.ClipSession);
        Assert.Equal("A", workspace.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void TryCommitLoad_RetainsBufferedSourceWithoutLocalPath()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var source = new BufferedChapterSource("chapters.txt", [.. "data"u8]);

        var group = SingleGroup("chapters.txt", "Loaded");
        Assert.True(CommitLoad(workspace, revision, source, group, ClipSessionTransitions.FromLoad(group)));
        Assert.Same(source, workspace.CurrentSource);
        Assert.Equal("chapters.txt", workspace.DisplayPath);
        Assert.Empty(workspace.CurrentPath);
        Assert.Equal("buffer:chapters.txt", source.Identity);
    }

    [Fact]
    public void TryCommitLoad_IgnoresStaleBufferedSource()
    {
        var workspace = new ChapterWorkspace();
        var oldRevision = workspace.BeginLoadOperation();
        var currentRevision = workspace.BeginLoadOperation();
        var currentSource = new BufferedChapterSource("current.txt", [2]);
        var currentGroup = SingleGroup("current.txt", "Current");
        Assert.True(CommitLoad(workspace, currentRevision, currentSource, currentGroup, ClipSessionTransitions.FromLoad(currentGroup)));

        var oldGroup = SingleGroup("old.txt", "Old");
        Assert.False(CommitLoad(workspace, oldRevision,
            new BufferedChapterSource("old.txt", [1]), oldGroup, ClipSessionTransitions.FromLoad(oldGroup)));
        Assert.Same(currentSource, workspace.CurrentSource);
        Assert.Equal("Current", workspace.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void TryCommitLoad_IgnoresStaleRevision()
    {
        var workspace = new ChapterWorkspace();
        var oldRevision = workspace.BeginLoadOperation();
        var newerRevision = workspace.BeginLoadOperation();
        var newerSession = ClipSessionTransitions.FromLoad(SingleGroup("fast.txt", "Fast"));
        var newerGroup = SingleGroup("fast.txt", "Fast");
        Assert.True(CommitLoad(workspace, newerRevision, new LocalPathChapterSource("fast.txt"), newerGroup, newerSession));

        var staleSession = ClipSessionTransitions.FromLoad(SingleGroup("slow.txt", "Slow"));
        var slowGroup = SingleGroup("slow.txt", "Slow");
        Assert.False(CommitLoad(workspace, oldRevision, new LocalPathChapterSource("slow.txt"), slowGroup, staleSession));

        Assert.Equal("fast.txt", workspace.CurrentPath);
        Assert.Equal("Fast", workspace.CurrentChapterSet?.Chapters[0].Name);
    }

    [Fact]
    public void Replacing_session_ends_old_history_owner_and_cancels_its_token()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var originalGroup = SingleGroup("old.txt", "Old");
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("old.txt"), originalGroup, ClipSessionTransitions.FromLoad(originalGroup)));
        var oldSession = workspace.ContentSession!;
        var oldToken = oldSession.LifetimeToken;

        var replacementRevision = workspace.BeginLoadOperation();
        var replacementGroup = SingleGroup("new.txt", "New");
        Assert.True(CommitLoad(workspace, replacementRevision, new LocalPathChapterSource("new.txt"), replacementGroup, ClipSessionTransitions.FromLoad(replacementGroup)));

        Assert.True(oldSession.IsEnded);
        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(workspace.ContentSession!.IsEnded);
        Assert.NotSame(oldSession, workspace.ContentSession);
    }

    [Fact]
    public void Successful_export_baseline_is_track_scoped_and_tracks_later_edits()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));
        var options = workspace.CreateExportOptionsForProjectedInfo();
        var captured = workspace.CaptureExportSnapshot(workspace.CurrentChapterSet!, options, trackIndex: 0)!;

        Assert.True(workspace.RecordSuccessfulExport(captured));
        Assert.NotNull(workspace.GetExportBaseline(captured.TrackId));
        Assert.True(workspace.HasUnexportedChanges); // The second track has no baseline.

        workspace.SelectClip(0);
        workspace.WriteBackCurrentChapterSet(workspace.CurrentChapterSet! with
        {
            Chapters = [workspace.CurrentChapterSet!.Chapters[0] with { Name = "Edited" }, .. workspace.CurrentChapterSet.Chapters.Skip(1)]
        });
        Assert.True(workspace.HasUnexportedChanges);

        var editedCapture = workspace.CaptureExportSnapshot(workspace.CurrentChapterSet!, options, trackIndex: 0)!;
        Assert.True(workspace.RecordSuccessfulExport(editedCapture));
        workspace.SelectClip(1);
        var otherTrackOptions = workspace.CreateExportOptionsForProjectedInfo();
        var otherTrackCapture = workspace.CaptureExportSnapshot(workspace.CurrentChapterSet!, otherTrackOptions, trackIndex: 1)!;
        Assert.True(workspace.RecordSuccessfulExport(otherTrackCapture));
        Assert.Equal(workspace.GetExportBaseline(editedCapture.TrackId)!.Digest,
            workspace.CaptureExportSnapshot(EditableChapterDocumentAdapter.ToChapterSet(workspace.ContentSession!.Snapshot.Document, 0),
                workspace.CreateExportOptionsForProjectedInfo() with { SourceFileName = "00001" }, 0)!.Digest);
        Assert.Equal(workspace.GetExportBaseline(otherTrackCapture.TrackId)!.Digest,
            workspace.CaptureExportSnapshot(workspace.CurrentChapterSet!, workspace.CreateExportOptionsForProjectedInfo(), 1)!.Digest);
        Assert.False(workspace.HasUnexportedChanges);
        Assert.Equal(editedCapture.Content.StateIdentity, workspace.GetExportBaseline(editedCapture.TrackId)!.StateIdentity);
        Assert.Equal(2, workspace.ContentSession!.GetHistorySnapshot().Nodes.Length);
    }

    [Fact]
    public void Stale_export_completion_cannot_update_replacement_baseline()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = SingleGroup("old.txt", "Old");
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("old.txt"), group, ClipSessionTransitions.FromLoad(group)));
        var captured = workspace.CaptureExportSnapshot(workspace.CurrentChapterSet!, workspace.CreateExportOptionsForProjectedInfo(), 0)!;

        var replacementRevision = workspace.BeginLoadOperation();
        var replacement = SingleGroup("new.txt", "New");
        Assert.True(CommitLoad(workspace, replacementRevision, new LocalPathChapterSource("new.txt"), replacement, ClipSessionTransitions.FromLoad(replacement)));

        Assert.False(workspace.RecordSuccessfulExport(captured));
        Assert.Null(workspace.GetExportBaseline(workspace.ContentSession!.Snapshot.Document.Tracks[0].Id));
    }

    [Fact]
    public void Clearing_session_invalidates_pending_load_revision_and_releases_owner()
    {
        var workspace = new ChapterWorkspace();
        var oldRevision = workspace.BeginLoadOperation();
        var group = SingleGroup("old.txt", "Old");
        Assert.True(CommitLoad(workspace, oldRevision, new LocalPathChapterSource("old.txt"), group, ClipSessionTransitions.FromLoad(group)));
        var ending = workspace.ContentSession!;
        var pendingRevision = workspace.BeginLoadOperation();

        workspace.ClearSession();

        Assert.True(ending.IsEnded);
        Assert.Null(workspace.ContentSession);
        Assert.False(workspace.IsCurrentRevision(pendingRevision));
    }

    [Fact]
    public void TryCommitAppend_RequiresMatchingSessionIdAndRevision()
    {
        var workspace = new ChapterWorkspace();
        var loadRevision = workspace.BeginLoadOperation();
        var baseSession = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var baseGroup = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, loadRevision, new LocalPathChapterSource("base.mpls"), baseGroup, baseSession));

        var appendRevision = workspace.CaptureRevision();
        var expectedId = workspace.ClipSession!.SessionId;
        var snapshot = workspace.ContentSession!.Snapshot;
        var append = workspace.AppendClipSource(appendRevision, expectedId, snapshot, SingleGroup("append.mpls", "Append"));
        Assert.True(append.Succeeded);
        Assert.True(workspace.ClipSession.IsCombined);
        Assert.Equal(3, workspace.ClipSession.OriginalGroup.Entries.Count);
    }

    [Fact]
    public void TryCommitAppend_RejectsAfterNewerLoad()
    {
        var workspace = new ChapterWorkspace();
        var loadRevision = workspace.BeginLoadOperation();
        var baseSession = ClipSessionTransitions.FromLoad(MultiMplsGroup());
        var baseGroup = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, loadRevision, new LocalPathChapterSource("base.mpls"), baseGroup, baseSession));

        var appendRevision = workspace.CaptureRevision();
        var expectedId = workspace.ClipSession!.SessionId;
        var snapshot = workspace.ContentSession!.Snapshot;
        var appendSession = ClipSessionTransitions.FromDocument(workspace.ClipSession!, snapshot.Document, combined: true);

        var newerRevision = workspace.BeginLoadOperation();
        var newGroup = SingleGroup("new.txt", "New");
        Assert.True(CommitLoad(workspace, newerRevision, new LocalPathChapterSource("new.txt"), newGroup, ClipSessionTransitions.FromLoad(newGroup)));

        Assert.False(workspace.TryCommitAppend(appendRevision, expectedId, appendSession));
        Assert.Equal("new.txt", workspace.CurrentPath);
        Assert.False(workspace.ClipSession!.IsCombined);
    }

    [Fact]
    public void WriteBack_UpdatesSelectedSplitEntry()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));
        workspace.SelectClip(1);

        var updated = workspace.CurrentChapterSet! with
        {
            Chapters = [new Chapter(1, TimeSpan.Zero, "Edited")]
        };
        workspace.WriteBackCurrentChapterSet(updated);

        var split = Assert.IsType<SplitClipSession>(workspace.ClipSession);
        Assert.Empty(split.Group.Entries[0].ChapterSet.Chapters);
        Assert.Empty(split.Group.Entries[1].ChapterSet.Chapters);
        Assert.Equal("Edited", workspace.ContentSession!.Snapshot.Document.Tracks[1].Chapters[0].Name);
        Assert.Equal("A", workspace.ContentSession.Snapshot.Document.Tracks[0].Chapters[0].Name);
    }

    [Fact]
    public async Task Publishing_content_candidate_keeps_history_attached_to_selected_clip()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));
        workspace.SelectClip(1);
        var rootId = workspace.ContentSession!.GetHistorySnapshot().RootId;
        var edited = workspace.CurrentChapterSet! with
        {
            Chapters = [workspace.CurrentChapterSet.Chapters[0] with { Name = "Edited" }, .. workspace.CurrentChapterSet.Chapters.Skip(1)]
        };
        workspace.WriteBackCurrentChapterSet(edited);

        Assert.Equal("Edited", workspace.CurrentChapterSet?.Chapters[0].Name);
        Assert.Equal("Edited", workspace.ContentSession!.Snapshot.Document.Tracks[1].Chapters[0].Name);
        Assert.Equal(rootId, workspace.ContentSession.GetHistorySnapshot().RootId);
        Assert.Equal(1, workspace.ContentSession.Snapshot.MutationRevision);

        var undone = await workspace.ContentSession.UndoAsync();
        workspace.PublishContentDocument(undone.Snapshot.Document);

        Assert.Equal("C", workspace.CurrentChapterSet?.Chapters[0].Name);
        Assert.Equal("C", workspace.ContentSession.Snapshot.Document.Tracks[1].Chapters[0].Name);
        Assert.Equal(rootId, workspace.ContentSession.GetHistorySnapshot().CursorId);
    }

    [Fact]
    public async Task Merge_then_edit_then_split_uses_current_content_and_keeps_merge_undo_separate()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));
        var contentSession = workspace.ContentSession!;
        var originalCId = contentSession.Snapshot.Document.Tracks[1].Chapters[0].Id;

        var merge = workspace.ToggleClipStructure();
        Assert.True(merge.Succeeded, string.Join("; ", merge.EditResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        Assert.Same(contentSession, workspace.ContentSession);
        Assert.True(workspace.ClipSession!.IsCombined);
        Assert.Single(contentSession.Snapshot.Document.Tracks);
        Assert.All(workspace.ClipSession.OriginalGroup.Entries, static entry => Assert.Empty(entry.ChapterSet.Chapters));

        var edited = workspace.CurrentChapterSet! with
        {
            Chapters = [.. workspace.CurrentChapterSet.Chapters.Select(chapter => chapter.Name == "C" ? chapter with { Name = "Edited C" } : chapter)]
        };
        workspace.WriteBackCurrentChapterSet(edited);
        var editedId = contentSession.Snapshot.Document.Tracks[0].Chapters.Single(static chapter => chapter.Name == "Edited C").Id;
        Assert.Equal(originalCId, editedId);

        var split = workspace.ToggleClipStructure();
        Assert.True(split.Succeeded, string.Join("; ", split.EditResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        Assert.False(workspace.ClipSession!.IsCombined);
        Assert.Equal("Edited C", contentSession.Snapshot.Document.Tracks[1].Chapters[0].Name);
        Assert.Equal(originalCId, contentSession.Snapshot.Document.Tracks[1].Chapters[0].Id);

        var undoSplit = await contentSession.UndoAsync();
        workspace.PublishContentDocument(undoSplit.Snapshot.Document);
        Assert.True(workspace.ClipSession!.IsCombined);
        Assert.Equal("Edited C", contentSession.Snapshot.Document.Tracks[0].Chapters.Single(static chapter => chapter.Name == "Edited C").Name);

        var undoEdit = await contentSession.UndoAsync();
        workspace.PublishContentDocument(undoEdit.Snapshot.Document);
        Assert.Equal("C", contentSession.Snapshot.Document.Tracks[0].Chapters.Single(chapter => chapter.Id == originalCId).Name);

        var undoMerge = await contentSession.UndoAsync();
        workspace.PublishContentDocument(undoMerge.Snapshot.Document);
        Assert.False(workspace.ClipSession!.IsCombined);
        Assert.Equal("C", contentSession.Snapshot.Document.Tracks[1].Chapters[0].Name);
    }

    [Fact]
    public void Append_rejects_captured_document_after_selected_track_edit()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));
        var captured = workspace.ContentSession!.Snapshot;
        var expectedSessionId = workspace.ClipSession!.SessionId;
        workspace.WriteBackCurrentChapterSet(workspace.CurrentChapterSet! with
        {
            Chapters = [workspace.CurrentChapterSet.Chapters[0] with { Name = "Edited" }, .. workspace.CurrentChapterSet.Chapters.Skip(1)]
        });

        var result = workspace.AppendClipSource(revision, expectedSessionId, captured, SingleGroup("append.mpls", "Append"));

        Assert.True(result.IsStale);
        Assert.Equal(2, workspace.ContentSession.Snapshot.Document.Tracks.Length);
        Assert.Equal("Edited", workspace.ContentSession.Snapshot.Document.Tracks[0].Chapters[0].Name);
    }

    [Fact]
    public void CreateExportOptions_ReadsOwnedProjectionAndExportPreferences()
    {
        var workspace = new ChapterWorkspace();
        var revision = workspace.BeginLoadOperation();
        var group = MultiMplsGroup();
        Assert.True(CommitLoad(workspace, revision, new LocalPathChapterSource("movie.mpls"), group, ClipSessionTransitions.FromLoad(group)));

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
        Assert.False(options.AutoGenerateNames);
        Assert.False(options.ApplyExpression);
        Assert.Equal("t+1", options.Expression);
        Assert.Equal("preset", options.ExpressionPresetId);
        Assert.Equal("script.lua", options.ExpressionSourceName);
        Assert.Equal(0, options.OrderShift);
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
        Assert.False(options.ApplyExpression);
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

    private static bool CommitLoad(
        ChapterWorkspace workspace,
        int revision,
        ChapterSourceDocument source,
        ChapterImportSource group,
        ClipSession session) =>
        workspace.TryCommitLoad(revision, source, session, EditableChapterDocumentAdapter.FromChapterImportSource(group));
}
