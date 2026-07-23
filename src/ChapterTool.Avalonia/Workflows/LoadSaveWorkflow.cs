using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Session;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Workflows;

/// <summary>
/// Owns revision-aware load, append, and save service orchestration for one chapter workspace.
/// </summary>
internal sealed class LoadSaveWorkflow(
    ChapterWorkspace workspace,
    IChapterLoadService loadService,
    IChapterSaveService saveService)
{
    public async ValueTask<LoadWorkflowResult> LoadAsync(
        string path,
        Action<ChapterImportProgress> reportProgress,
        CancellationToken cancellationToken)
    {
        var operationRevision = workspace.BeginLoadOperation();
        if (string.IsNullOrWhiteSpace(path))
        {
            return LoadWorkflowResult.EmptyPath;
        }

        var result = await loadService.LoadAsync(
            path,
            new ChapterImportProgressForwarder(progress =>
            {
                if (workspace.IsCurrentRevision(operationRevision))
                {
                    reportProgress(progress);
                }
            }),
            cancellationToken);
        if (!workspace.IsCurrentRevision(operationRevision))
        {
            return LoadWorkflowResult.Stale;
        }

        if (!result.Success || result.Groups.Count == 0)
        {
            return new LoadWorkflowResult(LoadWorkflowState.Failed, result, null);
        }

        var session = ClipSessionTransitions.FromLoad(result.Groups[0]);
        return workspace.TryCommitLoad(operationRevision, path, session)
            ? new LoadWorkflowResult(LoadWorkflowState.Succeeded, result, session)
            : LoadWorkflowResult.Stale;
    }

    public async ValueTask<AppendWorkflowResult> AppendAsync(string path, CancellationToken cancellationToken)
    {
        var operationRevision = workspace.CaptureRevision();
        var expectedSession = workspace.ClipSession;
        if (expectedSession is null)
        {
            return AppendWorkflowResult.NoSession;
        }

        var expectedSessionId = expectedSession.SessionId;
        var result = await loadService.LoadAsync(path, cancellationToken);
        if (!workspace.IsCurrentRevision(operationRevision)
            || workspace.ClipSession?.SessionId != expectedSessionId)
        {
            return AppendWorkflowResult.Stale;
        }

        if (!result.Success || result.Groups.Count == 0)
        {
            return new AppendWorkflowResult(AppendWorkflowState.FailedLoad, result, null, null);
        }

        var transition = ClipSessionTransitions.Append(workspace.ClipSession ?? expectedSession, result.Groups[0]);
        if (!transition.Succeeded || transition.Session is null)
        {
            return new AppendWorkflowResult(AppendWorkflowState.FailedTransition, result, transition, null);
        }

        return workspace.TryCommitAppend(operationRevision, expectedSessionId, transition.Session)
            ? new AppendWorkflowResult(AppendWorkflowState.Succeeded, result, transition, transition.Session)
            : AppendWorkflowResult.Stale;
    }

    public ValueTask<ChapterExportResult> SaveAsync(
        ChapterSet chapterSet,
        ChapterExportOptions options,
        string? directory,
        CancellationToken cancellationToken) =>
        saveService.SaveAsync(chapterSet, options, directory, cancellationToken, workspace.CurrentPath);

    private sealed class ChapterImportProgressForwarder(Action<ChapterImportProgress> report) : IChapterImportProgressReporter
    {
        public void Report(ChapterImportProgress progress) => report(progress);
    }
}

internal enum LoadWorkflowState
{
    EmptyPath,
    Failed,
    Succeeded,
    Stale
}

internal sealed record LoadWorkflowResult(
    LoadWorkflowState State,
    ChapterImportResult? Result,
    ClipSession? Session)
{
    public static LoadWorkflowResult EmptyPath { get; } = new(LoadWorkflowState.EmptyPath, null, null);

    public static LoadWorkflowResult Stale { get; } = new(LoadWorkflowState.Stale, null, null);
}

internal enum AppendWorkflowState
{
    NoSession,
    FailedLoad,
    FailedTransition,
    Succeeded,
    Stale
}

internal sealed record AppendWorkflowResult(
    AppendWorkflowState State,
    ChapterImportResult? ImportResult,
    ClipAppendTransitionResult? Transition,
    ClipSession? Session)
{
    public static AppendWorkflowResult NoSession { get; } = new(AppendWorkflowState.NoSession, null, null, null);

    public static AppendWorkflowResult Stale { get; } = new(AppendWorkflowState.Stale, null, null, null);
}
