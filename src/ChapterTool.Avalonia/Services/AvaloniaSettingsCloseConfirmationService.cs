using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ChapterTool.Avalonia.Localization;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaSettingsCloseConfirmationService(IAppLocalizer localizer) : ISettingsCloseConfirmationService
{
    public async ValueTask<SettingsCloseAction> ConfirmCloseAsync(Window owner, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new Window
        {
            Title = localizer.GetString("Settings.Unsaved.Title"),
            Width = 380,
            Height = 170,
            MinWidth = 360,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var message = new TextBlock
        {
            Text = localizer.GetString("Settings.Unsaved.Message"),
            TextWrapping = TextWrapping.Wrap
        };
        var saveButton = new Button
        {
            Content = localizer.GetString("Common.Save"),
            MinWidth = 82
        };
        var discardButton = new Button
        {
            Content = localizer.GetString("Settings.Unsaved.Discard"),
            MinWidth = 82
        };
        var cancelButton = new Button
        {
            Content = localizer.GetString("Common.Cancel"),
            MinWidth = 82
        };

        saveButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Save);
        discardButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Discard);
        cancelButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Cancel);

        dialog.Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                message,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        saveButton,
                        discardButton,
                        cancelButton
                    }
                }
            }
        };
        Grid.SetRow(message, 0);
        Grid.SetRow(((Grid)dialog.Content).Children[1], 1);

        var result = await dialog.ShowDialog<SettingsCloseAction?>(owner);
        cancellationToken.ThrowIfCancellationRequested();
        return result ?? SettingsCloseAction.Cancel;
    }
}
