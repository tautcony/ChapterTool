using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform.Expressions;
using DeleteRowsTimingMode = ChapterTool.Contracts.Configuration.DeleteRowsTimingMode;

namespace ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;

/// <summary>Owns the narrow session ports consumed by secondary tools.</summary>
public sealed class MainWindowPortAdapters
{
    public MainWindowPortAdapters(MainWindowViewModel owner)
    {
        Notifications = new MainShellNotificationPort(owner);
        Expression = new ExpressionSessionPortAdapter(owner, Notifications);
        Preferences = new PreferenceSinkAdapter(owner);
        ExportPreferences = new ExportPreferencePortAdapter(owner);
        NamingPreferences = new NamingPreferencePortAdapter(owner);
        ChapterEdit = new ChapterEditPortAdapter(owner);
    }

    public ExpressionSessionPortAdapter Expression { get; }

    public IMainShellNotificationPort Notifications { get; }

    public PreferenceSinkAdapter Preferences { get; }

    public ExportPreferencePortAdapter ExportPreferences { get; }

    public NamingPreferencePortAdapter NamingPreferences { get; }

    public ChapterEditPortAdapter ChapterEdit { get; }
}

public sealed class ExpressionSessionPortAdapter(MainWindowViewModel owner, IMainShellNotificationPort? notifications = null) : IExpressionSessionPort
{
    private readonly IMainShellNotificationPort notifications = notifications ?? new MainShellNotificationPort(owner);

    public IAppLocalizer Localizer => owner.Localizer;

    public IReadOnlyList<ChapterExpressionPreset> ExpressionPresets => owner.ExpressionEngine.Presets;

    public string Expression => owner.Workspace.Projection.Expression;

    public bool ApplyExpression => owner.Workspace.Projection.ApplyExpression;

    public string ExpressionPresetId => owner.Workspace.Projection.ExpressionPresetId;

    public string ExpressionSourceName => owner.Workspace.Projection.ExpressionSourceName;

    public async ValueTask<ChapterDiagnostic?> LoadScriptAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var diagnostic = ApplyLuaExpressionSettings(
            string.IsNullOrWhiteSpace(text) ? "t" : text,
            applyExpression: true,
            expressionPresetId: string.Empty,
            expressionSourceName: Path.GetFileName(path));

        if (diagnostic is null)
        {
            owner.SetStatus("Status.LuaExpressionScriptLoaded", ("path", ExpressionSourceName));
            owner.LogStatus();
        }

        return diagnostic;
    }

    public ChapterDiagnostic? ApplyLuaExpressionSettings(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName)
    {
        owner.Workspace.ApplyExpressionFields(expression, applyExpression, expressionPresetId, expressionSourceName);
        notifications.RefreshExpressionFields();
        notifications.RefreshRows();

        if (!ApplyExpression)
        {
            owner.SetStatus("Status.Updated");
            notifications.RefreshStatus();
            return null;
        }

        var diagnostic = ValidateLuaExpressionScript(Expression, logDiagnostics: true);
        if (diagnostic is null)
        {
            owner.SetStatus("Status.Updated");
        }
        else
        {
            owner.SetStatus(null, diagnostic);
        }

        notifications.RefreshStatus();
        return diagnostic;
    }

    public ChapterDiagnostic? ValidateLuaExpressionScript(string scriptText, bool logDiagnostics)
    {
        var result = owner.ExpressionEngine.Evaluate(
            string.IsNullOrWhiteSpace(scriptText) ? "t" : scriptText,
            ChapterExpressionValidation.CreateContext(owner.CurrentChapterSet));
        if (logDiagnostics)
        {
            owner.LogDiagnostics("Lua expression script", result.Diagnostics);
        }

        return result.Diagnostics.FirstOrDefault();
    }

    public string FormatDiagnosticForDisplay(ChapterDiagnostic diagnostic) => owner.LocalizeDiagnostic(diagnostic);
}

public sealed class PreferenceSinkAdapter(MainWindowViewModel owner) : IPreferenceSink
{
    public IAppLocalizer Localizer => owner.Localizer;

    public string UiLanguage => owner.UiLanguage;

    public int SaveFormatIndex => owner.SaveFormatIndex;

    public string XmlLanguage => owner.XmlLanguage;

    public OutputTextEncoding OutputTextEncoding => owner.OutputTextEncoding;

    public decimal FrameAccuracyTolerance => owner.FrameAccuracyTolerance;

    public ChapterEditingOptions EditingOptions => owner.EditingOptions;

    public void ApplyLoadedSettings(AppSettings settings) => ApplyPreferences(settings, applyDefaultSaveFormat: true);

    public void ApplyLivePreferences(AppSettings settings) => ApplyPreferences(settings, applyDefaultSaveFormat: false);

    public async ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken)
    {
        owner.UiLanguage = AppLanguage.Normalize(language);
        Localizer.SetCulture(owner.UiLanguage);
        if (owner.SettingsStore is null)
        {
            return;
        }

        await owner.SettingsStore.UpdateAsync(
            current => current with { Application = current.Application with { Language = owner.UiLanguage } },
            cancellationToken);
        owner.Log("Log.LanguageSet", ("language", owner.UiLanguage));
        owner.NotifyStateChanged();
    }

    private void ApplyPreferences(AppSettings settings, bool applyDefaultSaveFormat)
    {
        owner.SaveDirectory = ChapterSavePath.CleanOptionalPath(settings.SavingPath);
        owner.UiLanguage = AppLanguage.Normalize(settings.Language);
        Localizer.SetCulture(owner.UiLanguage);
        if (applyDefaultSaveFormat
            && Enum.TryParse<ChapterExportFormat>(settings.DefaultSaveFormat, ignoreCase: true, out var format))
        {
            owner.SaveFormat = format;
        }

        owner.FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
        owner.ApplyEditingOptions(new ChapterEditingOptions(
            DeleteRowsTimingModes.ParseOrDefault(settings.DeleteRowsTimingMode) == DeleteRowsTimingMode.Preserve
                ? ChapterTool.Core.Editing.DeleteRowsTimingMode.Preserve
                : ChapterTool.Core.Editing.DeleteRowsTimingMode.Normalize,
            ChapterTool.Contracts.Configuration.FrameDisplayModes.ParseOrDefault(settings.FrameDisplayMode) == ChapterTool.Contracts.Configuration.FrameDisplayMode.Round
                ? ChapterTool.Core.Editing.FrameDisplayMode.Round
                : ChapterTool.Core.Editing.FrameDisplayMode.DecimalPlaces,
            ChapterTool.Contracts.Configuration.FrameDisplayModes.NormalizeDecimalPlaces(settings.FrameDecimalPlaces)));
        owner.RoundFrames = ChapterTool.Contracts.Configuration.FrameDisplayModes.ParseOrDefault(settings.FrameDisplayMode) == ChapterTool.Contracts.Configuration.FrameDisplayMode.Round;
        owner.XmlLanguage = string.IsNullOrWhiteSpace(settings.DefaultXmlLanguage) ? "und" : settings.DefaultXmlLanguage;
        owner.EmitBom = settings.EmitBom;
        owner.OutputTextEncoding = OutputTextEncodings.ParseOrDefault(settings.OutputTextEncoding);
        owner.NotifyStateChanged();
    }
}

/// <summary>
/// Export format port backed by workspace export preferences.
/// Uses ViewModel property setters so bindable notifications stay correct.
/// </summary>
public sealed class ExportPreferencePortAdapter(MainWindowViewModel owner) : IExportPreferencePort
{
    public int SaveFormatIndex
    {
        get => owner.SaveFormatIndex;
        set => owner.SaveFormatIndex = value;
    }

    public ChapterExportFormat SaveFormat
    {
        get => owner.SaveFormat;
        set => owner.SaveFormat = value;
    }
}

/// <summary>
/// Naming mode port backed by workspace projection state through ViewModel setters.
/// </summary>
public sealed class NamingPreferencePortAdapter(MainWindowViewModel owner) : INamingPreferencePort
{
    public bool AutoGenerateNames
    {
        get => owner.AutoGenerateNames;
        set => owner.AutoGenerateNames = value;
    }

    public bool UseTemplateNames
    {
        get => owner.UseTemplateNames;
        set => owner.UseTemplateNames = value;
    }
}

/// <summary>
/// Chapter edit port that applies frame shifts through the clip editing coordinator.
/// </summary>
public sealed class ChapterEditPortAdapter(MainWindowViewModel owner) : IChapterEditPort
{
    public void ShiftFramesForward(int frames)
    {
        if (owner.CurrentChapterSet is null)
        {
            return;
        }

        owner.ApplyEditFromPort(
            owner.ClipEditingCoordinator.ShiftFramesForward(owner.CurrentChapterSet, frames),
            owner.Localizer.Format(LocalizedMessage.Create("Action.ShiftFramesForward", ("frames", frames))));
    }
}

public sealed class MainShellNotificationPort(MainWindowViewModel owner) : IMainShellNotificationPort
{
    public void RefreshExpressionFields()
    {
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.Expression));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ApplyExpression));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ExpressionPresetId));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ExpressionSourceName));
    }

    public void RefreshRows() => owner.RefreshRowsFromPort();

    public void RefreshStatus() => owner.NotifyStateChanged();
}
