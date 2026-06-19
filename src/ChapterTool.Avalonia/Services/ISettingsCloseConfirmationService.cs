using Avalonia.Controls;

namespace ChapterTool.Avalonia.Services;

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
