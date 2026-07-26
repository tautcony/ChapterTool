using ChapterTool.Avalonia.Localization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Session.Ports;

/// <summary>Expression read/apply surface for the expression tool.</summary>
public interface IExpressionSessionPort
{
    IAppLocalizer Localizer { get; }

    IReadOnlyList<ChapterExpressionPreset> ExpressionPresets { get; }

    string Expression { get; }

    bool ApplyExpression { get; }

    string ExpressionPresetId { get; }

    string ExpressionSourceName { get; }

    ChapterDiagnostic? ApplyLuaExpressionSettings(
        string expression,
        bool applyExpression,
        string expressionPresetId,
        string expressionSourceName);

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
    void ShiftFramesForward(int frames);
}
