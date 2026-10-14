using Avalonia.Controls;
using Avalonia.Input;
using ChapterTool.Avalonia.UI.ViewModels;

namespace ChapterTool.Avalonia.UI.Views.Tools;

/// <summary>Provides the settings tool view.</summary>
public sealed partial class SettingsToolView : UserControl
{
    public SettingsToolView()
    {
        InitializeComponent();
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is not TextBox { DataContext: ShortcutRowViewModel { IsEditable: true } row })
        {
            return;
        }

        switch (args.Key)
        {
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin:
                args.Handled = true;
                return;
            case Key.Back:
                row.Gesture = string.Empty;
                args.Handled = true;
                return;
        }

        var gesture = new KeyGesture(args.Key, args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta));
        row.Gesture = gesture.ToString()
            .Replace("Command+", "Meta+", StringComparison.Ordinal)
            .Replace("Cmd+", "Meta+", StringComparison.Ordinal);
        args.Handled = true;
    }
}
