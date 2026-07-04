using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests.Localization;

public sealed partial class LocalizationTests
{
    [Fact]
    public void SupportedCulturesHaveMatchingResourceKeys()
    {
        var expected = AppLocalizationResources.Fallback.Keys.Order(StringComparer.Ordinal).ToArray();

        foreach (var (culture, resources) in AppLocalizationResources.All)
        {
            Assert.Equal(expected, resources.Keys.Order(StringComparer.Ordinal).ToArray());
        }
    }

    [Fact]
    public void LocalizedFormatStringsUseCompatiblePlaceholders()
    {
        var expected = AppLocalizationResources.Fallback
            .ToDictionary(pair => pair.Key, pair => Placeholders(pair.Value), StringComparer.Ordinal);

        foreach (var (culture, resources) in AppLocalizationResources.All)
        {
            foreach (var (key, value) in resources)
            {
                Assert.Equal(expected[key], Placeholders(value));
            }
        }
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

        Assert.True(localizer.TryGetString("Diagnostic.InvalidChapterIndex", out _));
        Assert.True(localizer.TryGetString("Diagnostic.MissingDependency", out _));

        var arguments = new Dictionary<string, object?> { ["index"] = 7 };
        Assert.Equal(
            AppLocalizationResources.All["en-US"]["Diagnostic.InvalidChapterIndex"].Replace("{index}", "7", StringComparison.Ordinal),
            localizer.Format("Diagnostic.InvalidChapterIndex", arguments));

        localizer.SetCulture("zh-CN");
        Assert.Equal(
            AppLocalizationResources.All["zh-CN"]["Diagnostic.InvalidChapterIndex"].Replace("{index}", "7", StringComparison.Ordinal),
            localizer.Format("Diagnostic.InvalidChapterIndex", arguments));
    }

    [Fact]
    public void LanguageToolListsAllSupportedLanguages()
    {
        var owner = CreateViewModel(new AppLocalizationManager("en-US"));
        var tool = new LanguageToolViewModel(owner);

        Assert.Equal(["zh-CN", "en-US", "ja-JP"], tool.Languages.Select(static language => language.CultureName).ToArray());
        Assert.Contains(
            tool.Languages,
            language => language.DisplayName == AppLocalizationResources.All["en-US"]["Language.Japanese"]);
    }

    private static string[] Placeholders(string value) =>
        PlaceholderRegex().Matches(value)
            .Select(static match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

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

        return new MainWindowViewModel(
            new FakeLoadService(),
            new FakeSaveService(),
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            new FakeWindowService(),
            new ChapterTimeFormatter(),
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            localizer: localizer);
    }

    private sealed class FakeLoadService : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterImportResult(
                true,
                [new ChapterInfoGroup(path, [new ChapterSourceOption("default", "default", new ChapterInfo(path, path, 0, "OGM", 24, TimeSpan.Zero, []))])],
                []));
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", []));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidTextControlCharacterRegex();
}
