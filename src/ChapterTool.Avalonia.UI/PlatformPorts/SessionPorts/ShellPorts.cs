using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;

/// <summary>Expression read/apply surface for the expression tool.</summary>
public interface IExpressionSessionPort
{
    IAppLocalizer Localizer { get; }

    IReadOnlyList<ChapterExpressionPreset> ExpressionPresets { get; }

    string Expression { get; }

    bool ApplyExpression { get; }

    string ExpressionPresetId { get; }

    string ExpressionSourceName { get; }

    ValueTask<ChapterDiagnostic?> LoadScriptAsync(string path, CancellationToken cancellationToken);

    ChapterDiagnostic? ApplyLuaExpressionSettings(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName);

    void SaveAppliedExpressionPreview(
        string expression,
        string expressionPresetId,
        string expressionSourceName,
        ExpressionPreviewProjection projection);

    ChapterDiagnostic? ValidateLuaExpressionScript(string scriptText, bool logDiagnostics);

    string FormatDiagnosticForDisplay(ChapterDiagnostic diagnostic);
}

/// <summary>Live preference apply surface shared by Settings and Language tools.</summary>
public interface IPreferenceSink
{
    IAppLocalizer Localizer { get; }

    string UiLanguage { get; }

    int SaveFormatIndex { get; }

    string XmlLanguage { get; }

    OutputTextEncoding OutputTextEncoding { get; }

    decimal FrameAccuracyTolerance { get; }

    ChapterEditingOptions EditingOptions => ChapterEditingOptions.Default;

    void ApplyLoadedSettings(AppSettings settings);

    void ApplyLivePreferences(AppSettings settings);

    ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken);
}

/// <summary>Session save-format surface for preview format selection.</summary>
public interface IExportPreferencePort
{
    int SaveFormatIndex { get; set; }

    ChapterExportFormat SaveFormat { get; set; }
}

/// <summary>Naming-mode surface for the template-names tool.</summary>
public interface INamingPreferencePort
{
    bool AutoGenerateNames { get; set; }

    bool UseTemplateNames { get; set; }
}

/// <summary>Chapter edit surface for tools such as forward-shift.</summary>
public interface IChapterEditPort
{
    ValueTask ShiftFramesForwardAsync(int frames, CancellationToken cancellationToken = default);
}

/// <summary>Builds one-shot content previews and applies them through the active history session.</summary>
public interface IChapterContentOperationPort
{
    bool CanPrepareExpression => true;

    ChapterContentPreview PrepareExpression(string expression);

    ChapterContentPreview PrepareTemplateNames(bool autoGenerateNames, bool useTemplateNames);

    ChapterContentPreview PrepareContentOptions();

    ChapterContentPreview PrepareFrameShift(int frames);

    ChapterContentPreview PrepareFrameRateConversion(decimal sourceFps, decimal targetFps);

    ValueTask<TransactionOutcome> ApplyAsync(ChapterContentPreview preview, CancellationToken cancellationToken = default);

    void Cancel(ChapterContentPreview preview);
}

public interface IMainShellNotificationPort
{
    void RefreshExpressionFields();

    void RefreshRows();

    void RefreshStatus();
}
