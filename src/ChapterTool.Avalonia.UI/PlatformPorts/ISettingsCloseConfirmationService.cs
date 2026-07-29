using Avalonia.Controls;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public interface ISettingsCloseConfirmationService
{
    ValueTask<SettingsCloseAction> ConfirmCloseAsync(Window owner, CancellationToken cancellationToken);
}

public enum SettingsCloseAction
{
    Cancel,
    Save,
    Discard
}
