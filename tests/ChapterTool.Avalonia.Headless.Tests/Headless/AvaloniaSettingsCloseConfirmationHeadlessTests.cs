using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.PlatformPorts;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AvaloniaSettingsCloseConfirmationHeadlessTests
{
    [AvaloniaFact]
    public async Task ConfirmCloseAsync_returns_save_when_save_button_is_clicked()
    {
        Assert.Equal(SettingsCloseAction.Save, await ClickButtonAsync("Common.Save"));
    }

    [AvaloniaFact]
    public async Task ConfirmCloseAsync_returns_discard_when_discard_button_is_clicked()
    {
        Assert.Equal(SettingsCloseAction.Discard, await ClickButtonAsync("Settings.Unsaved.Discard"));
    }

    [AvaloniaFact]
    public async Task ConfirmCloseAsync_returns_cancel_when_cancel_button_is_clicked()
    {
        Assert.Equal(SettingsCloseAction.Cancel, await ClickButtonAsync("Common.Cancel"));
    }

    private static async Task<SettingsCloseAction> ClickButtonAsync(string buttonKey)
    {
        using var host = new MainWindowHeadlessTestHost();
        var service = new AvaloniaSettingsCloseConfirmationService(host.Localizer);
        var owner = new Window { Width = 200, Height = 120 };
        owner.Show();

        var confirmation = service.ConfirmCloseAsync(owner, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        var dialog = FindDialog(owner);
        var button = dialog.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => Equals(candidate.Content, host.Localizer.GetString(buttonKey)));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        try
        {
            return await confirmation;
        }
        finally
        {
            owner.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Window FindDialog(Window owner)
    {
        var dialog = Assert.Single(owner.OwnedWindows);
        return dialog;
    }
}
