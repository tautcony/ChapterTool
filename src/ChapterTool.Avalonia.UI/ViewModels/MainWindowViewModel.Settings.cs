using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains settings loading behavior for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    public async ValueTask LoadSettingsAsync(CancellationToken cancellationToken)
    {
        if (SettingsStore is null)
        {
            return;
        }

        var settings = await SettingsStore.LoadAsync(cancellationToken);
        ToolSession.Preferences.ApplyLoadedSettings(settings.Application);
        Log(LogLevel.Information,
            $"Settings loaded: savingPath='{SaveDirectory ?? string.Empty}', language='{UiLanguage}', " +
            $"defaultSaveFormat={SaveFormat}, frameDisplay={EditingOptions.FrameDisplay}, " +
            $"frameAccuracy={FrameAccuracyTolerance}, xmlLanguage='{XmlLanguage}'",
            "Settings",
            ("savingPath", SaveDirectory ?? string.Empty),
            ("language", UiLanguage),
            ("defaultSaveFormat", SaveFormat),
            ("frameDisplay", EditingOptions.FrameDisplay),
            ("frameAccuracy", FrameAccuracyTolerance),
            ("xmlLanguage", XmlLanguage));
        NotifyStateChanged();
    }

}
