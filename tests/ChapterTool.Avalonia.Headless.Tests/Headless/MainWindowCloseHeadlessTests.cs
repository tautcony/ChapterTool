using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowCloseHeadlessTests
{
    [AvaloniaFact]
    public async Task Cancel_close_keeps_document_candidate_history_and_session()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        host.ViewModel.Expression = "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.IsContentPreviewPending);

        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var lifetimeToken = session.LifetimeToken;
        var prompt = new TestClosePrompt(DesktopCloseDecision.Cancel);
        host.Window.ClosePrompt = prompt;

        host.Window.Close();

        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);
        Assert.False(lifetimeToken.IsCancellationRequested);
        Assert.Same(session, host.ViewModel.Workspace.ContentSession);
        Assert.True(host.ViewModel.HasHistory);
        Assert.True(host.ViewModel.IsContentPreviewPending);
        Assert.Equal(1, prompt.Calls);
        Assert.True(prompt.LastHadUnexportedContent);
        Assert.True(prompt.LastHadPendingCandidate);
    }

    [AvaloniaFact]
    public async Task Discard_close_ends_document_session()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var lifetimeToken = session.LifetimeToken;
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.DiscardAndClose);

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.False(host.Window.IsVisible);
        Assert.True(session.IsEnded);
        Assert.True(lifetimeToken.IsCancellationRequested);
        Assert.Null(host.ViewModel.Workspace.ContentSession);
    }

    [AvaloniaFact]
    public async Task Export_close_exports_current_track_and_closes_when_all_tracks_are_exported()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.ExportAndClose);

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.Equal(1, host.SaveService.Calls);
        Assert.False(host.Window.IsVisible);
        Assert.Null(host.ViewModel.Workspace.ContentSession);
    }

    [AvaloniaFact]
    public async Task Successful_export_makes_history_only_close_immediate()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        await host.ViewModel.SaveCommand.ExecuteAsync(null, CancellationToken.None);
        Assert.True(host.ViewModel.HasHistory);
        Assert.False(host.ViewModel.RequiresDesktopCloseDecision);

        var prompt = new TestClosePrompt(DesktopCloseDecision.Cancel);
        host.Window.ClosePrompt = prompt;
        host.Window.Close();

        Assert.False(host.Window.IsVisible);
        Assert.Equal(0, prompt.Calls);
        Assert.Null(host.ViewModel.Workspace.ContentSession);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_canceled_export_keeps_window_and_session_open(bool canceled)
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        host.SaveService.Handler = (_, _, _, _) => canceled
            ? ValueTask.FromException<ChapterExportResult>(new OperationCanceledException())
            : ValueTask.FromResult(new ChapterExportResult(false, "failed", ".txt", []));
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.ExportAndClose);

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);
        Assert.Same(session, host.ViewModel.Workspace.ContentSession);
        Assert.True(host.ViewModel.HasUnexportedContentAcrossTracks);
    }

    [AvaloniaFact]
    public async Task Stale_export_completion_keeps_window_open()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited"));
        host.SaveService.Handler = async (_, _, _, cancellationToken) =>
        {
            await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Edited during export"), cancellationToken);
            return new ChapterExportResult(true, "ok", ".txt", []);
        };
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.ExportAndClose);

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.True(host.Window.IsVisible);
        Assert.True(host.ViewModel.HasUnexportedContentAcrossTracks);
        Assert.Contains("changed while the close decision was open", host.ViewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Partial_track_export_keeps_window_open_and_reports_remaining_tracks()
    {
        using var host = new MainWindowHeadlessTestHost(
        [
            MainWindowHeadlessTestHost.ImportResult(
                "movie.txt",
                MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "first.txt", "Intro"),
                MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "second.txt", "Credits")),
        ]);
        await host.LoadAsync("movie.txt");
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.ExportAndClose);

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.Equal(1, host.SaveService.Calls);
        Assert.True(host.Window.IsVisible);
        Assert.True(host.ViewModel.HasUnexportedContentAcrossTracks);
        Assert.Contains("tracks still have unexported content", host.ViewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Repeated_close_while_prompt_is_active_keeps_one_decision_workflow()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var prompt = new TestClosePrompt();
        host.Window.ClosePrompt = prompt;

        host.Window.Close();
        host.Window.Close();

        Assert.Equal(1, prompt.Calls);
        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);

        prompt.Complete(DesktopCloseDecision.Cancel);
        await prompt.Completion;
        await Task.Yield();
        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);
    }

    [AvaloniaFact]
    public async Task State_change_before_dispatched_close_retry_keeps_session_open()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.DiscardAndClose);

        host.Window.Close();
        await host.ViewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(0, "Changed after decision"));
        await DrainPostedWorkAsync();

        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);
        Assert.Same(session, host.ViewModel.Workspace.ContentSession);
        Assert.Contains("changed while the close decision was open", host.ViewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Another_closing_handler_can_cancel_discard_without_ending_session()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        host.Window.ClosePrompt = new TestClosePrompt(DesktopCloseDecision.DiscardAndClose);
        EventHandler<WindowClosingEventArgs> cancelClose = (_, args) => args.Cancel = true;
        host.Window.Closing += cancelClose;

        host.Window.Close();
        await DrainPostedWorkAsync();

        Assert.True(host.Window.IsVisible);
        Assert.False(session.IsEnded);
        Assert.Same(session, host.ViewModel.Workspace.ContentSession);
        host.Window.Closing -= cancelClose;
    }

    [AvaloniaTheory]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    public async Task Rendered_close_dialog_cancel_button_fits_localized_content(string culture)
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager(culture));
        await host.LoadAsync("movie.txt");
        var prompt = Assert.IsType<MainWindowClosePrompt>(host.Window.ClosePrompt);

        host.Window.Close();

        var dialog = Assert.IsType<Window>(prompt.ActiveDialog);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(dialog);
        var buttonRow = dialog.GetVisualDescendants().OfType<WrapPanel>()
            .Single(panel => panel.Children.OfType<Button>().Count() == 3);
        var buttons = buttonRow.Children.OfType<Button>().ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.All(buttons, button => Assert.True(button.Bounds.Right <= buttonRow.Bounds.Width));

        var cancel = buttons.Single(button => button.Name == "CloseGuard.Cancel");
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await DrainPostedWorkAsync();

        Assert.True(host.Window.IsVisible);
        Assert.Null(prompt.ActiveDialog);
        Assert.NotNull(host.ViewModel.Workspace.ContentSession);
    }

    [AvaloniaFact]
    public async Task Rendered_close_dialog_discard_button_closes_and_ends_session()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        var session = Assert.IsType<SessionState>(host.ViewModel.Workspace.ContentSession);
        var prompt = Assert.IsType<MainWindowClosePrompt>(host.Window.ClosePrompt);

        host.Window.Close();

        var dialog = Assert.IsType<Window>(prompt.ActiveDialog);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(dialog);
        var discard = dialog.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "CloseGuard.DiscardAndClose");
        discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await DrainPostedWorkAsync();

        Assert.False(host.Window.IsVisible);
        Assert.True(session.IsEnded);
    }

    private static async ValueTask DrainPostedWorkAsync()
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class TestClosePrompt(params DesktopCloseDecision[] decisions) : IMainWindowClosePrompt
    {
        private readonly Queue<DesktopCloseDecision> decisions = new(decisions);
        private readonly TaskCompletionSource<DesktopCloseDecision> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public bool LastHadUnexportedContent { get; private set; }

        public bool LastHadPendingCandidate { get; private set; }

        public Task<DesktopCloseDecision> Completion => completionSource.Task;

        public ValueTask<DesktopCloseDecision> ShowAsync(
            Window owner,
            bool hasUnexportedContent,
            bool hasPendingCandidate,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastHadUnexportedContent = hasUnexportedContent;
            LastHadPendingCandidate = hasPendingCandidate;
            if (decisions.TryDequeue(out var decision))
            {
                completionSource.TrySetResult(decision);
                return ValueTask.FromResult(decision);
            }

            return new ValueTask<DesktopCloseDecision>(completionSource.Task);
        }

        public void Complete(DesktopCloseDecision decision) => completionSource.TrySetResult(decision);
    }
}
