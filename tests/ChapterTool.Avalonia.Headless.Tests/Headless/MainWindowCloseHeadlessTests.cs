using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowCloseHeadlessTests
{
    [AvaloniaFact]
    public async Task Closing_ends_edited_session_without_session_loss_confirmation()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        host.ViewModel.Expression = "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.IsContentPreviewPending);

        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var lifetimeToken = session.LifetimeToken;
        var confirmationCalls = 0;
        host.ViewModel.SessionLossConfirmation = _ =>
        {
            confirmationCalls++;
            return ValueTask.FromResult(false);
        };

        host.Window.Close();

        Assert.False(host.Window.IsVisible);
        Assert.True(session.IsEnded);
        Assert.True(lifetimeToken.IsCancellationRequested);
        Assert.Null(host.ViewModel.Workspace.ContentSession);
        Assert.False(host.ViewModel.IsContentPreviewPending);
        Assert.Equal(0, confirmationCalls);
    }

    [AvaloniaFact]
    public async Task Cancelled_close_keeps_document_session_alive()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var lifetimeToken = session.LifetimeToken;
        EventHandler<WindowClosingEventArgs> cancelClose = (_, args) => args.Cancel = true;
        host.Window.Closing += cancelClose;

        try
        {
            host.Window.Close();

            Assert.True(host.Window.IsVisible);
            Assert.False(session.IsEnded);
            Assert.False(lifetimeToken.IsCancellationRequested);
            Assert.Same(session, host.ViewModel.Workspace.ContentSession);
        }
        finally
        {
            host.Window.Closing -= cancelClose;
        }
    }
}
