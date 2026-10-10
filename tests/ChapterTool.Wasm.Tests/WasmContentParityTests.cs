using System.Runtime.Versioning;
using ChapterTool.Core.Editing;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmContentParityTests
{
    [Fact]
    public async Task NamingDraftAndCancelDoNotChangeExportOrHistory()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        var before = workspace.Preview().Content;
        workspace.ChapterNameModeIndex = 1;
        workspace.OrderShift = 2;
        var preview = workspace.PrepareNamingPreview();
        Assert.True(preview!.IsValid);
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Single(workspace.HistoryEntries);
        workspace.CancelContentPreview();
        Assert.Equal(before, workspace.Preview().Content);
        Assert.False(await workspace.ApplyContentPreviewAsync());
    }

    [Fact]
    public async Task PreferenceRefreshNeverReappliesNames()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.ChapterNameModeIndex = 1;
        workspace.PrepareNamingPreview();
        Assert.True(await workspace.ApplyContentPreviewAsync());
        workspace.UpdateRow(1, null, "Manual name");
        workspace.ChapterNameModeIndex = 1;
        var before = workspace.Preview().Content;
        var history = workspace.HistoryEntries.Count;
        workspace.RoundFrames = false;
        workspace.ApplyOptionsAndRefresh();
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Equal(history, workspace.HistoryEntries.Count);
        Assert.Equal("Manual name", workspace.Rows[1].Name);
    }

    [Fact]
    public async Task CandidateIsAtomicStaleAndCannotBeSubmittedTwice()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        workspace.UpdateRow(0, "00:00:01.000", null);
        var before = workspace.Preview().Content;
        var initialCount = workspace.HistoryEntries.Count;
        workspace.PrepareFrameShiftPreview(1);
        Assert.Equal(before, workspace.Preview().Content);
        Assert.True(await workspace.ApplyContentPreviewAsync());
        Assert.False(await workspace.ApplyContentPreviewAsync());
        Assert.Equal(initialCount + 1, workspace.HistoryEntries.Count);
        await workspace.UndoAsync();
        Assert.Equal(before, workspace.Preview().Content);
        workspace.PrepareFrameShiftPreview(1);
        workspace.UpdateRow(1, null, "New base");
        Assert.True(workspace.IsContentPreviewStale);
        Assert.False(await workspace.ApplyContentPreviewAsync());
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1000001, false)]
    [InlineData(-1000001, false)]
    public async Task InvalidOrZeroShiftDoesNotCommit(int frames, bool valid)
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        Assert.Equal(valid, workspace.PrepareFrameShiftPreview(frames)!.IsValid);
        Assert.False(await workspace.ApplyContentPreviewAsync());
        Assert.Single(workspace.HistoryEntries);
    }

    [Fact]
    public async Task NamingDraftRevisionRequiresExplicitFreshPreview()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.ChapterNameModeIndex = 1;
        workspace.PrepareNamingPreview();
        workspace.OrderShift = 2;
        Assert.True(workspace.IsContentPreviewStale);
        Assert.False(await workspace.ApplyContentPreviewAsync());
        Assert.Single(workspace.HistoryEntries);
        workspace.PrepareNamingPreview();
        Assert.False(workspace.IsContentPreviewStale);
        Assert.True(await workspace.ApplyContentPreviewAsync());
        Assert.Equal(3, workspace.Rows[0].Number);
        var applied = workspace.Preview().Content;
        await workspace.UndoAsync();
        await workspace.RedoAsync();
        Assert.Equal(applied, workspace.Preview().Content);
    }

    [Fact]
    public async Task Cancelled_signed_shift_preserves_history_cursor_and_export()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        var path = TestSupport.TestRepository.CoreFixture("Importing", "Disc", "Mpls", "00001_fch.mpls");
        await workspace.LoadAsync(path, await File.ReadAllBytesAsync(path));
        var before = workspace.Preview().Content;
        var cursor = Assert.Single(workspace.HistoryEntries).Id;
        Assert.True(workspace.PrepareFrameShiftPreview(-1)!.IsValid);
        Assert.Equal(before, workspace.Preview().Content);
        workspace.CancelContentPreview();
        Assert.False(await workspace.ApplyContentPreviewAsync());
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Equal(cursor, Assert.Single(workspace.HistoryEntries).Id);
    }

    [Fact]
    public async Task FrameConversionReviewAndCancellationPreserveCommittedContent()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        var path = TestSupport.TestRepository.CoreFixture("Importing", "Disc", "Mpls", "00001_fch.mpls");
        await workspace.LoadAsync(path, await File.ReadAllBytesAsync(path));
        workspace.SelectedFrameRateIndex = 3;
        var before = workspace.Preview().Content;
        var preview = workspace.PrepareFrameRatePreview();
        Assert.True(preview!.IsValid, string.Join("; ", preview.Errors));
        Assert.True(workspace.ContentPreviewProjection!.HasChanges);
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Single(workspace.HistoryEntries);
        workspace.CancelContentPreview();
        Assert.False(await workspace.ApplyContentPreviewAsync());
        Assert.Equal(before, workspace.Preview().Content);
        var reviewed = workspace.PrepareFrameRatePreview()!;
        Assert.True(await workspace.ApplyContentPreviewAsync());
        Assert.Equal(reviewed.Candidate.Tracks[0].Chapters.Select(chapter => chapter.StartTicks),
            workspace.PrepareNamingPreview()!.Before.Tracks[0].Chapters.Select(chapter => chapter.StartTicks));
        Assert.Equal(2, workspace.HistoryEntries.Count);
        await workspace.UndoAsync();
        Assert.Equal(before, workspace.Preview().Content);
        await workspace.RedoAsync();
        Assert.Equal(2, workspace.HistoryEntries.Count);
    }

    [Fact]
    public async Task Clip_navigation_stops_at_bounds_and_invalid_frames_do_not_commit()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        workspace.SelectAdjacentClip(1);
        Assert.Empty(workspace.ClipOptions);
        var path = TestSupport.TestRepository.CoreFixture("Importing", "Disc", "Mpls", "00001_Hidan_no_Aria_AA.mpls");
        await workspace.LoadAsync(path, await File.ReadAllBytesAsync(path));
        var first = workspace.SelectedClipId;
        workspace.SelectAdjacentClip(-1);
        Assert.Equal(first, workspace.SelectedClipId);
        workspace.SelectAdjacentClip(1);
        var last = workspace.SelectedClipId;
        Assert.NotEqual(first, last);
        workspace.SelectAdjacentClip(1);
        Assert.Equal(last, workspace.SelectedClipId);
        workspace.ToggleClipCombine();
        Assert.Single(workspace.ClipOptions);
        var before = workspace.Preview().Content;
        var draft = workspace.BeginCellEdit(0, ChapterCellField.Frame)!;
        Assert.NotNull(await workspace.CommitCellEditAsync(draft, "invalid"));
        Assert.Equal(before, workspace.Preview().Content);
    }

    [Fact]
    public async Task UnchangedCellAndStaleIdentityDoNotCommit()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectRow(1);
        var id = workspace.Rows[1].Id;
        var draft = workspace.BeginCellEdit(1, ChapterCellField.StartTime)!;
        Assert.Null(await workspace.CommitCellEditAsync(draft, draft.OriginalValue));
        Assert.Single(workspace.HistoryEntries);
        workspace.UpdateRow(0, null, "Changed base");
        Assert.NotNull(await workspace.CommitCellEditAsync(draft, "00:00:42.000"));
        Assert.Equal(id, workspace.Rows[workspace.SelectedRowIndex].Id);
        await workspace.UndoAsync();
        Assert.Equal(id, workspace.Rows[workspace.SelectedRowIndex].Id);
    }

    [Fact]
    public async Task FrameCellUsesCapturedIdentityAndOneTransaction()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        var before = workspace.Preview().Content;
        var draft = workspace.BeginCellEdit(1, ChapterCellField.Frame)!;
        Assert.Null(await workspace.CommitCellEditAsync(draft, "240"));
        Assert.Equal(2, workspace.HistoryEntries.Count);
        Assert.NotNull(await workspace.CommitCellEditAsync(draft, "480"));
        await workspace.UndoAsync();
        Assert.Equal(before, workspace.Preview().Content);
    }
}
