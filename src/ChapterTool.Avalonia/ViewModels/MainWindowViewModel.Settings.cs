using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async ValueTask LoadSettingsAsync(CancellationToken cancellationToken)
    {
        if (settingsStore is null)
        {
            return;
        }

        var settings = await settingsStore.LoadAsync(cancellationToken);
        ApplyLoadedSettings(settings.Application);
        Log("Log.SettingsLoaded",
            ("savingPath", SaveDirectory ?? string.Empty),
            ("language", UiLanguage));
        NotifyStateChanged();
    }

    /// <summary>
    /// Applies preferences loaded at startup, including the default save format.
    /// </summary>
    public void ApplyLoadedSettings(AppSettings settings) => ApplyPreferences(settings, applyDefaultSaveFormat: true);

    /// <summary>
    /// Applies live preference edits from the settings tool without resetting the session save format.
    /// </summary>
    public void ApplyLivePreferences(AppSettings settings) => ApplyPreferences(settings, applyDefaultSaveFormat: false);

    private void ApplyPreferences(AppSettings settings, bool applyDefaultSaveFormat)
    {
        SaveDirectory = NormalizeConfiguredDirectory(settings.SavingPath);
        UiLanguage = AppLanguage.Normalize(settings.Language);
        Localizer.SetCulture(UiLanguage);
        if (applyDefaultSaveFormat
            && Enum.TryParse<ChapterExportFormat>(settings.DefaultSaveFormat, ignoreCase: true, out var format))
        {
            SaveFormat = format;
        }

        FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
        XmlLanguage = string.IsNullOrWhiteSpace(settings.DefaultXmlLanguage)
            ? "und"
            : settings.DefaultXmlLanguage;
        EmitBom = settings.EmitBom;
        OutputTextEncoding = OutputTextEncodings.ParseOrDefault(settings.OutputTextEncoding);
        NotifyStateChanged();
    }

    public async ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
    {
        UiLanguage = AppLanguage.Normalize(language);
        Localizer.SetCulture(UiLanguage);
        if (settingsStore is null)
        {
            return;
        }

        await settingsStore.UpdateAsync(
            current => current with { Application = current.Application with { Language = UiLanguage } },
            cancellationToken);
        Log("Log.LanguageSet", ("language", UiLanguage));
        NotifyStateChanged();
    }
}
