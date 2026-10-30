using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Avalonia.UI.ViewModels;

namespace ChapterTool.Avalonia.Views;

public sealed partial class MainWindow
{
    private bool closeWorkflowActive;
    private bool allowClose;

    internal IMainWindowClosePrompt ClosePrompt { get; set; } = new MainWindowClosePrompt();

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (allowClose)
        {
            return;
        }

        if (closeWorkflowActive)
        {
            args.Cancel = true;
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.RequiresDesktopCloseDecision)
        {
            return;
        }

        args.Cancel = true;
        closeWorkflowActive = true;
        _ = ProcessCloseRequestAsync(viewModel, viewModel.CaptureCloseRequestState());
    }

    private async Task ProcessCloseRequestAsync(MainWindowViewModel viewModel, MainWindowCloseRequestState requestState)
    {
        var closeScheduled = false;
        try
        {
            var decision = await ClosePrompt.ShowAsync(
                this,
                viewModel.HasUnexportedContentAcrossTracks,
                viewModel.IsContentPreviewPending,
                CancellationToken.None);
            switch (decision)
            {
                case DesktopCloseDecision.Cancel:
                    return;
                case DesktopCloseDecision.DiscardAndClose:
                    if (!viewModel.IsCloseRequestStateCurrent(requestState))
                    {
                        viewModel.ReportCloseRequestStateChanged();
                        return;
                    }

                    ScheduleCloseAfterDecision(viewModel, requestState, requireAllTracksExported: false);
                    closeScheduled = true;
                    return;
                case DesktopCloseDecision.ExportAndClose:
                    var result = await viewModel.ExportForCloseAsync(requestState, CancellationToken.None);
                    if (result == CloseExportResult.Exported)
                    {
                        ScheduleCloseAfterDecision(viewModel, requestState, requireAllTracksExported: true);
                        closeScheduled = true;
                    }
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // A canceled export leaves the existing window and session intact.
        }
        catch (Exception exception)
        {
            viewModel.ReportCloseExportFailure(exception);
        }
        finally
        {
            if (!closeScheduled)
            {
                closeWorkflowActive = false;
            }
        }
    }

    private void ScheduleCloseAfterDecision(
        MainWindowViewModel viewModel,
        MainWindowCloseRequestState requestState,
        bool requireAllTracksExported)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(DataContext, viewModel)
                || !viewModel.IsCloseRequestStateCurrent(requestState)
                || (requireAllTracksExported && viewModel.HasUnexportedContentAcrossTracks))
            {
                viewModel.ReportCloseRequestStateChanged();
                closeWorkflowActive = false;
                return;
            }

            allowClose = true;
            Close();
            if (IsVisible)
            {
                allowClose = false;
            }
            closeWorkflowActive = false;
        }, DispatcherPriority.Background);
    }
}

internal enum DesktopCloseDecision
{
    Cancel,
    DiscardAndClose,
    ExportAndClose,
}

internal interface IMainWindowClosePrompt
{
    ValueTask<DesktopCloseDecision> ShowAsync(
        Window owner,
        bool hasUnexportedContent,
        bool hasPendingCandidate,
        CancellationToken cancellationToken);
}

internal sealed class MainWindowClosePrompt : IMainWindowClosePrompt
{
    internal Window? ActiveDialog { get; private set; }

    public async ValueTask<DesktopCloseDecision> ShowAsync(
        Window owner,
        bool hasUnexportedContent,
        bool hasPendingCandidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (owner.DataContext is not MainWindowViewModel viewModel)
        {
            return DesktopCloseDecision.Cancel;
        }

        var localizer = viewModel.Localizer;
        var content = new StackPanel
        {
            Spacing = 14,
            Margin = new global::Avalonia.Thickness(20),
            Width = 440,
            Children =
            {
                new TextBlock
                {
                    Text = localizer.GetString(hasUnexportedContent
                        ? "CloseGuard.Message"
                        : "CloseGuard.PendingOnlyMessage"),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        if (hasPendingCandidate)
        {
            content.Children.Add(new TextBlock
            {
                Text = localizer.GetString("CloseGuard.PendingCandidateNote"),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold,
            });
        }

        var dialog = new Window
        {
            Title = localizer.GetString("CloseGuard.Title"),
            CanResize = false,
            SizeToContent = SizeToContent.Height,
            Content = content,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        ActiveDialog = dialog;
        var buttons = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = content.Width,
        };
        content.Children.Add(buttons);

        AddButton(buttons, localizer.GetString("CloseGuard.Cancel"), DesktopCloseDecision.Cancel, isDefault: false, isCancel: true);
        AddButton(buttons, localizer.GetString("CloseGuard.DiscardAndClose"), DesktopCloseDecision.DiscardAndClose);
        AddButton(buttons, localizer.GetString("CloseGuard.ExportAndClose"), DesktopCloseDecision.ExportAndClose, isDefault: true);

        try
        {
            return await dialog.ShowDialog<DesktopCloseDecision>(owner);
        }
        finally
        {
            ActiveDialog = null;
        }

        void AddButton(Panel panel, string text, DesktopCloseDecision result, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button
            {
                Name = $"CloseGuard.{result}",
                Content = text,
                MinWidth = 88,
                Margin = new global::Avalonia.Thickness(4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsDefault = isDefault,
                IsCancel = isCancel,
            };
            button.Click += (_, _) => dialog.Close(result);
            panel.Children.Add(button);
        }
    }
}
