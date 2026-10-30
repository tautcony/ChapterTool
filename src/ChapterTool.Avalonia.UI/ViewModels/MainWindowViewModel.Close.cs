using ChapterTool.Core.Session;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Provides the desktop host with close-decision state and export handling.</summary>
public sealed partial class MainWindowViewModel
{
    public bool RequiresDesktopCloseDecision => IsContentPreviewPending || Workspace.HasUnexportedChanges;

    public bool HasUnexportedContentAcrossTracks => Workspace.HasUnexportedChanges;

    public MainWindowCloseRequestState CaptureCloseRequestState()
    {
        var session = Workspace.ContentSession;
        return new MainWindowCloseRequestState(
            session,
            session?.Snapshot.BaseToken,
            pendingContentPreview,
            expressionPreviewTimer.IsEnabled,
            Expression,
            ExpressionPreviewText);
    }

    public bool IsCloseRequestStateCurrent(MainWindowCloseRequestState state) =>
        ReferenceEquals(state.Session, Workspace.ContentSession)
        && Workspace.IsContentTokenCurrent(state.ContentToken)
        && ReferenceEquals(state.PendingPreview, pendingContentPreview)
        && state.PreviewTimerEnabled == expressionPreviewTimer.IsEnabled
        && state.Expression == Expression
        && state.ExpressionPreviewText == ExpressionPreviewText;

    public async ValueTask<CloseExportResult> ExportForCloseAsync(
        MainWindowCloseRequestState state,
        CancellationToken cancellationToken)
    {
        if (!IsCloseRequestStateCurrent(state))
        {
            SetStatus("CloseGuard.StateChanged");
            return CloseExportResult.Stale;
        }

        var session = Workspace.ContentSession;
        var token = session?.Snapshot.BaseToken;
        if (session is null || CurrentInfo is null || token is null)
        {
            return CloseExportResult.Failed;
        }

        try
        {
            var result = await SaveWithResultAsync(null, cancellationToken);
            if (result is null || !result.Success)
            {
                return CloseExportResult.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            return CloseExportResult.Canceled;
        }

        if (!Workspace.IsContentTokenCurrent(token) || !IsCloseRequestStateCurrent(state))
        {
            SetStatus("CloseGuard.StateChanged");
            return CloseExportResult.Stale;
        }

        if (!Workspace.HasUnexportedChanges)
        {
            return CloseExportResult.Exported;
        }

        if (!HasUnexportedContent)
        {
            SetStatus("CloseGuard.OtherTracksRemain");
            return CloseExportResult.RemainingTracks;
        }

        return CloseExportResult.Failed;
    }

    public void ReportCloseExportFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        SetStatus("Status.SaveFailed");
        Log(LogLevel.Error, "Export requested before closing the desktop window failed", "Close", exception.Message);
    }

    public void ReportCloseRequestStateChanged() => SetStatus("CloseGuard.StateChanged");
}

public sealed record MainWindowCloseRequestState(
    SessionState? Session,
    SessionBaseToken? ContentToken,
    object? PendingPreview,
    bool PreviewTimerEnabled,
    string Expression,
    string ExpressionPreviewText);

public enum CloseExportResult
{
    Exported,
    Canceled,
    Failed,
    Stale,
    RemainingTracks,
}
