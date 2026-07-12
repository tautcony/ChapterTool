namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async ValueTask LoadSettingsAsync(CancellationToken cancellationToken)
    {
        if (SettingsStore is null)
        {
            return;
        }

        var settings = await SettingsStore.LoadAsync(cancellationToken);
        PortAdapters.Preferences.ApplyLoadedSettings(settings.Application);
        Log("Log.SettingsLoaded",
            ("savingPath", SaveDirectory ?? string.Empty),
            ("language", UiLanguage));
        NotifyStateChanged();
    }

}
