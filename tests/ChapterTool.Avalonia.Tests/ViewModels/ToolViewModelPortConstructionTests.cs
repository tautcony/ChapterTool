using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class ToolViewModelPortConstructionTests
{
    [Fact]
    public void Secondary_tools_construct_from_only_their_narrow_ports()
    {
        var localizer = new AppLocalizationManager("en-US");
        var preferences = new FakePreferences(localizer);
        var expression = new FakeExpressionSession(localizer);

        using var settings = new SettingsToolViewModel(preferences, null, autoLoad: false);
        var language = new LanguageToolViewModel(preferences);
        var templateNames = new TemplateNamesToolViewModel(new FakeNamingPreferences());
        var forwardShift = new ForwardShiftToolViewModel(new FakeChapterEditPort());
        var expressionTool = new ExpressionToolViewModel(expression);
        var preview = new TextToolViewModel(
            static () => "preview",
            new TextToolOptions { FormatSelector = new TextToolFormatSelector(new FakeExportPreferences()) });

        Assert.NotNull(settings);
        Assert.NotNull(language);
        Assert.NotNull(templateNames);
        Assert.NotNull(forwardShift);
        Assert.NotNull(expressionTool);
        Assert.Equal("preview", preview.Text);
    }

    private sealed class FakePreferences(IAppLocalizer localizer) : IPreferenceSink
    {
        public IAppLocalizer Localizer => localizer;

        public string UiLanguage => "en-US";

        public int SaveFormatIndex => 0;

        public string XmlLanguage => "und";

        public OutputTextEncoding OutputTextEncoding => OutputTextEncoding.Utf8;

        public decimal FrameAccuracyTolerance => 0.001m;

        public void ApplyLoadedSettings(AppSettings settings)
        {
        }

        public void ApplyLivePreferences(AppSettings settings)
        {
        }

        public ValueTask SaveUiLanguageAsync(string language, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FakeExpressionSession(IAppLocalizer localizer) : IExpressionSessionPort
    {
        public IAppLocalizer Localizer => localizer;

        public IReadOnlyList<ChapterExpressionPreset> ExpressionPresets => [];

        public string Expression => "t";

        public bool ApplyExpression => false;

        public string ExpressionPresetId => string.Empty;

        public string ExpressionSourceName => string.Empty;

        public ValueTask<ChapterDiagnostic?> LoadScriptAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult<ChapterDiagnostic?>(null);

        public ChapterDiagnostic? ApplyLuaExpressionSettings(string expression, bool applyExpression, string expressionPresetId, string expressionSourceName) => null;

        public ChapterDiagnostic? ValidateLuaExpressionScript(string scriptText, bool logDiagnostics) => null;

        public string FormatDiagnosticForDisplay(ChapterDiagnostic diagnostic) => diagnostic.ToString();
    }

    private sealed class FakeExportPreferences : IExportPreferencePort
    {
        public int SaveFormatIndex { get; set; }

        public ChapterExportFormat SaveFormat { get; set; }
    }

    private sealed class FakeNamingPreferences : INamingPreferencePort
    {
        public bool AutoGenerateNames { get; set; }

        public bool UseTemplateNames { get; set; }
    }

    private sealed class FakeChapterEditPort : IChapterEditPort
    {
        public void ShiftFramesForward(int frames)
        {
        }
    }
}
