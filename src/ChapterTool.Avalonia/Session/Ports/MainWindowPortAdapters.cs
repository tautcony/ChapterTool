using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Session.Ports;

/// <summary>Owns the narrow session ports consumed by secondary tools.</summary>
internal sealed class MainWindowPortAdapters
{
    public MainWindowPortAdapters(MainWindowViewModel owner)
    {
        Expression = new ExpressionSessionPortAdapter(owner);
        Preferences = new PreferenceSinkAdapter(owner);
        ExportPreferences = new ExportPreferencePortAdapter(owner);
        NamingPreferences = new NamingPreferencePortAdapter(owner);
        ChapterEdit = new ChapterEditPortAdapter(owner);
    }

    public ExpressionSessionPortAdapter Expression { get; }

    public PreferenceSinkAdapter Preferences { get; }

    public ExportPreferencePortAdapter ExportPreferences { get; }

    public NamingPreferencePortAdapter NamingPreferences { get; }

    public ChapterEditPortAdapter ChapterEdit { get; }
}

internal sealed class ExpressionSessionPortAdapter(MainWindowViewModel owner) : IExpressionSessionPort
{
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
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.Expression));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ApplyExpression));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ExpressionPresetId));
        owner.NotifyPropertyChanged(nameof(MainWindowViewModel.ExpressionSourceName));
        owner.RefreshRowsFromPort();

        if (!ApplyExpression)
        {
            owner.SetStatus("Status.Updated");
            owner.LogStatus();
            owner.NotifyStateChanged();
            return null;
        }

        var diagnostic = ValidateLuaExpressionScript(Expression, logDiagnostics: true);
        if (diagnostic is null)
        {
            owner.SetStatus("Status.Updated");
            owner.LogStatus();
        }
        else
        {
            owner.SetStatus(null, diagnostic);
            owner.LogStatus(MainWindowViewModel.LogLevelFor(diagnostic.Severity));
        }

        owner.NotifyStateChanged();
        return diagnostic;
    }

    public ChapterDiagnostic? ValidateLuaExpressionScript(string scriptText, bool logDiagnostics)
    {
        var result = owner.ExpressionEngine.Evaluate(
            string.IsNullOrWhiteSpace(scriptText) ? "t" : scriptText,
            ChapterExpressionValidation.CreateContext(owner.CurrentChapterSet));
        if (logDiagnostics)
        {
            owner.LogDiagnostics(Localizer.GetString("Operation.LuaExpressionScript"), result.Diagnostics);
        }

        return result.Diagnostics.FirstOrDefault();
    }

    public string FormatDiagnosticForDisplay(ChapterDiagnostic diagnostic) => owner.LocalizeDiagnostic(diagnostic);
}

internal sealed class PreferenceSinkAdapter(MainWindowViewModel owner) : IPreferenceSink
{
    public IAppLocalizer Localizer => owner.Localizer;

    public string UiLanguage => owner.UiLanguage;

    public int SaveFormatIndex => owner.SaveFormatIndex;

    public string XmlLanguage => owner.XmlLanguage;

    public OutputTextEncoding OutputTextEncoding => owner.OutputTextEncoding;

    public decimal FrameAccuracyTolerance => owner.FrameAccuracyTolerance;

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
        owner.SaveDirectory = MainWindowViewModel.NormalizeConfiguredDirectory(settings.SavingPath);
        owner.UiLanguage = AppLanguage.Normalize(settings.Language);
        Localizer.SetCulture(owner.UiLanguage);
        if (applyDefaultSaveFormat
            && Enum.TryParse<ChapterExportFormat>(settings.DefaultSaveFormat, ignoreCase: true, out var format))
        {
            owner.SaveFormat = format;
        }

        owner.FrameAccuracyTolerance = settings.FrameAccuracyTolerance;
        owner.XmlLanguage = string.IsNullOrWhiteSpace(settings.DefaultXmlLanguage) ? "und" : settings.DefaultXmlLanguage;
        owner.EmitBom = settings.EmitBom;
        owner.OutputTextEncoding = OutputTextEncodings.ParseOrDefault(settings.OutputTextEncoding);
        owner.NotifyStateChanged();
    }
}

internal sealed class ExportPreferencePortAdapter(MainWindowViewModel owner) : IExportPreferencePort
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

internal sealed class NamingPreferencePortAdapter(MainWindowViewModel owner) : INamingPreferencePort
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

internal sealed class ChapterEditPortAdapter(MainWindowViewModel owner) : IChapterEditPort
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
