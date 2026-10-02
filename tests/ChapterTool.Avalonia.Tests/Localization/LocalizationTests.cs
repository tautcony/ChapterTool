using System.Globalization;
using System.Text.RegularExpressions;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.TestSupport;

namespace ChapterTool.Avalonia.Tests.Localization;

/// <summary>Verifies localization resources and localized text behavior.</summary>
public sealed partial class LocalizationTests
{
    [Fact]
    public void SupportedCulturesHaveMatchingResourceKeys()
    {
        var expected = AppLocalizationResources.Fallback.Keys.Order(StringComparer.Ordinal).ToArray();

        foreach (var (_, resources) in AppLocalizationResources.All)
        {
            Assert.Equal(expected, resources.Keys.Order(StringComparer.Ordinal).ToArray());
        }
    }

    [Fact]
    public void LocalizedFormatStringsUseCompatiblePlaceholders()
    {
        var expected = AppLocalizationResources.Fallback
            .ToDictionary(pair => pair.Key, pair => Placeholders(pair.Value), StringComparer.Ordinal);

        foreach (var (_, resources) in AppLocalizationResources.All)
        {
            foreach (var (key, value) in resources)
            {
                Assert.Equal(expected[key], Placeholders(value));
            }
        }
    }

    [Fact]
    public void DesktopResourcesDoNotOwnCliMessages()
    {
        Assert.DoesNotContain(
            AppLocalizationResources.Fallback.Keys,
            key => key.StartsWith("Cli.", StringComparison.Ordinal));
    }

    [Fact]
    public void NonEnglishResourcesDoNotContainEncodingArtifacts()
    {
        foreach (var culture in new[] { "zh-CN", "ja-JP" })
        {
            foreach (var (key, value) in AppLocalizationResources.All[culture])
            {
                AssertNoEncodingArtifacts(value, $"{culture}:{key}");
            }
        }
    }

    [Fact]
    public void LocalizerFallsBackAndFormatsMessages()
    {
        var localizer = new AppLocalizationManager("missing");

        Assert.Equal("zh-CN", localizer.CurrentCultureName);
        Assert.Equal(
            AppLocalizationResources.Fallback["Status.LoadedChapters"].Replace("{count}", "2", StringComparison.Ordinal),
            localizer.Format("Status.LoadedChapters", new Dictionary<string, object?> { ["count"] = 2 }));

        localizer.SetCulture("en-US");

        Assert.Equal(
            AppLocalizationResources.All["en-US"]["Status.LoadedChapters"].Replace("{count}", "2", StringComparison.Ordinal),
            localizer.Format("Status.LoadedChapters", new Dictionary<string, object?> { ["count"] = 2 }));
    }

    [Fact]
    public void DiagnosticKeysLocalizeAcrossCultures()
    {
        var localizer = new AppLocalizationManager("en-US");
        var invalidIndexKey = $"Diagnostic.{ChapterDiagnosticCode.InvalidChapterIndex.ToDisplayCode()}";

        Assert.True(localizer.TryGetString(invalidIndexKey, out _));
        Assert.True(localizer.TryGetString($"Diagnostic.{ChapterDiagnosticCode.MissingDependency.ToDisplayCode()}", out _));

        var arguments = new Dictionary<string, object?> { ["index"] = 7 };
        Assert.Equal(
            AppLocalizationResources.All["en-US"][invalidIndexKey].Replace("{index}", "7", StringComparison.Ordinal),
            localizer.Format(invalidIndexKey, arguments));

        localizer.SetCulture("zh-CN");
        Assert.Equal(
            AppLocalizationResources.All["zh-CN"][invalidIndexKey].Replace("{index}", "7", StringComparison.Ordinal),
            localizer.Format(invalidIndexKey, arguments));
    }

    [Fact]
    public void ConstructorDoesNotChangeThreadCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

            _ = new AppLocalizationManager("en-US");

            Assert.Equal("zh-CN", CultureInfo.CurrentCulture.Name);
            Assert.Equal("zh-CN", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("zh-CN", "已读取 DVD 光盘：1 个标题，3 个章节，1 个标题集。")]
    [InlineData("en-US", "DVD loaded: titles=1, chapters=3, title sets=1.")]
    [InlineData("ja-JP", "DVD を読み込みました：1 個のタイトル、3 個のチャプター、1 個のタイトルセット。")]
    public async Task DvdDiscDiagnosticFormatsRealImportCountsAcrossCultures(string culture, string expected)
    {
        var path = Path.Combine(TestRepository.Root, "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VmgRecovery", "Srcl8925", "VIDEO_TS.IFO");
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
        using var owner = CreateViewModel(new AppLocalizationManager(culture));

        Assert.Equal(expected, owner.LocalizeDiagnostic(diagnostic));
        Assert.Contains(result.Diagnostics, item => item.Code == ChapterDiagnosticCode.IfoTimingEstimated && item.Severity == DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("zh-CN", "已从 VTS_05_0.IFO 对应的 VOB 导航数据读取 DVD 章节时间。", "无法验证 VTS_05_0.IFO 对应的 VOB 导航时间戳。DVD 章节保留原始 IFO 估算时间，未作对齐调整。")]
    [InlineData("en-US", "DVD chapter times for VTS_05_0.IFO were read from VOB navigation timestamps.", "VOB navigation timestamps for VTS_05_0.IFO could not be verified. DVD chapters retain the original IFO time estimates without frame alignment.")]
    [InlineData("ja-JP", "VTS_05_0.IFO に対応する VOB のナビゲーションデータから DVD チャプター時刻を読み込みました。", "VTS_05_0.IFO に対応する VOB のナビゲーション時刻を検証できませんでした。DVD チャプターは元の IFO 推定時刻を保持し、フレーム位置への調整は行っていません。")]
    public void DvdTimingMessagesDistinguishNavTimesFromUnchangedEstimates(string culture, string captured, string estimated)
    {
        using var owner = CreateViewModel(new AppLocalizationManager(culture));
        var arguments = new Dictionary<string, object?> { ["source"] = "VTS_05_0.IFO" };
        Assert.Equal(captured, owner.LocalizeDiagnostic(new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.IfoTimingFromNav, string.Empty, Arguments: arguments)));
        Assert.Equal(estimated, owner.LocalizeDiagnostic(new ChapterDiagnostic(DiagnosticSeverity.Warning, ChapterDiagnosticCode.IfoTimingEstimated, string.Empty, Arguments: arguments)));
    }

    [Fact]
    public void SetCultureAppliesThreadCultureWithoutEventWhenNameIsUnchanged()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var localizer = new AppLocalizationManager("en-US");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var raised = 0;
            localizer.CultureChanged += (_, _) => raised++;

            localizer.SetCulture("en-US");

            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
            Assert.Equal(0, raised);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void SetCultureAppliesThreadCultureAndRaisesEventOnChange()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var localizer = new AppLocalizationManager("en-US");
            var raised = 0;
            localizer.CultureChanged += (_, _) => raised++;

            localizer.SetCulture("ja-JP");

            Assert.Equal("ja-JP", CultureInfo.CurrentUICulture.Name);
            Assert.Equal(1, raised);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void LanguageToolListsAllSupportedLanguages()
    {
        var owner = CreateViewModel(new AppLocalizationManager("en-US"));
        var tool = new LanguageToolViewModel(owner.ToolSession.Preferences);

        Assert.Equal(["zh-CN", "en-US", "ja-JP"], tool.Languages.Select(static language => language.CultureName).ToArray());
        Assert.Equal(["简体中文", "English", "日本語"], tool.Languages.Select(static language => language.DisplayName).ToArray());
    }

    private static string[] Placeholders(string value) =>
    [
        .. PlaceholderRegex().Matches(value)
            .Select(static match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
    ];

    private static void AssertNoEncodingArtifacts(string value, string context)
    {
        Assert.False(value.Contains('\uFFFD', StringComparison.Ordinal), $"{context} contains the Unicode replacement character.");

        var invalidControlCharacter = InvalidTextControlCharacterRegex().Match(value);
        if (invalidControlCharacter.Success)
        {
            Assert.Fail($"{context} contains invalid control character U+{(int)invalidControlCharacter.Value[0]:X4}.");
        }
    }

    private static MainWindowViewModel CreateViewModel(IAppLocalizer localizer)
    {
        var logService = new ApplicationLogPanelProvider();
        var formatter = new ChapterTimeFormatter();
        var expressionEngine = new LuaExpressionScriptService();

        return new MainWindowViewModel(
            new FakeLoadService(),
            new FakeSaveService(),
            new ChapterEditingService(formatter),
            new ChapterSegmentService(),
            new FakeWindowService(),
            formatter,
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            new FrameRateService(),
            localizer,
            expressionEngine,
            new ChapterExportService(formatter, expressionEngine));
    }

    private sealed class FakeLoadService : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterImportResult(
                true,
                [new ChapterImportSource(path, [new ChapterImportEntry("default", "default", new ChapterSet(path, path, ChapterImportFormat.Ogm, 24, TimeSpan.Zero, []))])],
                []));
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterSet info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken, string? sourcePath = null) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", []));
    }

    private sealed class FakeWindowService : IAuxiliaryToolHost
    {
        public ValueTask<AuxiliaryToolResult> OpenAsync(ToolId toolId, AuxiliaryToolRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Opened, toolId));

        public ValueTask<AuxiliaryToolResult> CloseAsync(ToolId toolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Closed, toolId));

        public void Dispose()
        {
        }
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTextControlCharacterRegex();
}
