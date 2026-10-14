using System.Runtime.Versioning;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Models;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmMainOptionsParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamingAfterAnAppliedExpressionKeepsTimesAndUndoRedoUseCurrentBranch(bool advanced)
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.Expression = "t + 1";
        if (advanced)
        {
            workspace.PrepareExpressionPreview(workspace.Expression);
            Assert.True(await workspace.ApplyExpressionPreviewAsync());
        }
        else
        {
            workspace.PrepareMainOptionsPreview();
            Assert.True(await workspace.ApplyMainOptionsPreviewAsync());
        }
        workspace.UpdateRow(1, null, "Manual name");
        workspace.ChapterNameModeIndex = 1;
        var naming = workspace.PrepareMainOptionsPreview()!;
        Assert.Equal(TimeSpan.TicksPerSecond, naming.Candidate.Tracks[0].Chapters[0].StartTicks);
        Assert.Equal(844_560_000L, naming.Candidate.Tracks[0].Chapters[1].StartTicks);
        Assert.Equal(string.Empty, naming.OperationDescriptor!.Parameters.Single(parameter => parameter.Name == "script").Value);
        Assert.True(await workspace.ApplyMainOptionsPreviewAsync());

        await workspace.UndoAsync();
        await workspace.UndoAsync();
        await workspace.UndoAsync();
        Assert.Equal(TimeSpan.TicksPerSecond, workspace.PrepareMainOptionsPreview()!.Candidate.Tracks[0].Chapters[0].StartTicks);
        workspace.CancelMainOptionsPreview();
        await workspace.RedoAsync();
        workspace.ChapterNameModeIndex = 1;
        Assert.Equal(TimeSpan.TicksPerSecond, workspace.PrepareMainOptionsPreview()!.Candidate.Tracks[0].Chapters[0].StartTicks);
    }

    [Fact]
    public async Task FrameCellUsesExactRateAndUnchangedDisplayKeepsStoredTicks()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        workspace.ApplyOptionsAndRefresh();
        var draft = workspace.BeginCellEdit(1, ChapterCellField.Frame)!;
        Assert.Null(await workspace.CommitCellEditAsync(draft, "1000000"));
        workspace.Expression = string.Empty;
        var unchanged = workspace.BeginCellEdit(1, ChapterCellField.Frame)!;
        var before = workspace.Preview().Content;
        var historyCount = workspace.HistoryEntries.Count;
        Assert.Null(await workspace.CommitCellEditAsync(unchanged, unchanged.OriginalValue));
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Equal(historyCount, workspace.HistoryEntries.Count);
        var comparison = workspace.PrepareMainOptionsPreview()!.Candidate.Tracks[0].Chapters[1];
        Assert.Equal(417_083_333_333L, comparison.StartTicks);
    }

    [Fact]
    public async Task CombinedOptionsReviewCommitsOneCandidateAndUndoRestoresOriginal()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        var before = workspace.Preview().Content;
        workspace.ChapterNameModeIndex = 1;
        workspace.OrderShift = 2;
        workspace.Expression = "t + 1";
        var candidate = workspace.PrepareMainOptionsPreview();
        Assert.True(candidate!.IsValid, string.Join("; ", candidate.Errors));
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Single(workspace.HistoryEntries);
        var first = candidate.Candidate.Tracks[0].Chapters[0];
        Assert.Equal(3, first.DisplayNumber);
        Assert.Equal(TimeSpan.TicksPerSecond, first.StartTicks);
        Assert.NotEqual("Opening", first.Name);
        Assert.True(await workspace.ApplyMainOptionsPreviewAsync());
        Assert.Equal(first.Name, workspace.Rows[0].Name);
        Assert.Equal(first.DisplayNumber, workspace.Rows[0].Number);
        Assert.Equal(2, workspace.HistoryEntries.Count);
        Assert.False(await workspace.ApplyMainOptionsPreviewAsync());
        var applied = workspace.Preview().Content;
        await workspace.UndoAsync();
        Assert.Equal(before, workspace.Preview().Content);
        await workspace.RedoAsync();
        Assert.Equal(applied, workspace.Preview().Content);
    }

    [Fact]
    public async Task DisplayRefreshPreservesReviewedCandidateAndVisibleFrameBasis()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.SelectedFrameRateIndex = 1;
        workspace.ApplyOptionsAndRefresh();
        workspace.SelectRow(1);
        workspace.SelectRow(2, ctrl: true);
        var before = workspace.Preview().Content;
        workspace.Expression = "t + 1";
        var candidate = workspace.PrepareMainOptionsPreview();
        var originalRow = workspace.Rows[1];
        var comparison = workspace.MainOptionsPreviewProjection!.Chapters.Single(row => row.Id == originalRow.Id);
        Assert.Equal(workspace.FormatRowFramePresentation(1, originalRow.FramesInfo, false),
            workspace.FormatPreviewFramePresentation(comparison, false, false));
        Assert.Equal(originalRow.IsFrameAccurate ? FrameAccuracy.Accurate : FrameAccuracy.Inexact,
            workspace.GetPreviewFrameAccuracy(comparison.Before!.StartTicks));
        workspace.RoundFrames = false;
        workspace.ApplyOptionsAndRefresh();
        Assert.Same(candidate, workspace.MainOptionsPreview);
        Assert.False(workspace.IsMainOptionsPreviewStale);
        Assert.Equal(before, workspace.Preview().Content);
        Assert.True(await workspace.ApplyMainOptionsPreviewAsync());
        Assert.Equal(new[] { 1, 2 }, workspace.SelectedRowIndexes.Order().ToArray());
    }

    [Fact]
    public async Task ChangedMainDraftRequiresFreshReviewAndCancellationDoesNotCommit()
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.Expression = "t + 1";
        workspace.PrepareMainOptionsPreview();
        workspace.OrderShift = 3;
        Assert.True(workspace.IsMainOptionsPreviewStale);
        Assert.False(await workspace.ApplyMainOptionsPreviewAsync());
        workspace.PrepareMainOptionsPreview();
        Assert.False(workspace.IsMainOptionsPreviewStale);
        var before = workspace.Preview().Content;
        workspace.CancelMainOptionsPreview();
        Assert.False(await workspace.ApplyMainOptionsPreviewAsync());
        Assert.Equal(before, workspace.Preview().Content);
        Assert.Single(workspace.HistoryEntries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task InvalidNumberingRejectsTheWholeCombinedCandidate(int offset)
    {
        using var workspace = new WasmWorkspace(new WasmChapterService());
        await workspace.LoadSampleAsync();
        workspace.OrderShift = offset;
        workspace.Expression = "t + 1";
        Assert.False(workspace.PrepareMainOptionsPreview()!.IsValid);
        Assert.False(await workspace.ApplyMainOptionsPreviewAsync());
        Assert.Single(workspace.HistoryEntries);
    }
}
