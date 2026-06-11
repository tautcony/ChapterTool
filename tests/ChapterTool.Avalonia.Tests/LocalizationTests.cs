using System.Text.RegularExpressions;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests;

public sealed class LocalizationTests
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
    public void ChineseAndJapaneseResourcesDoNotContainKnownMojibake()
    {
        var banned = new[] { "杞藉叆", "淇濆瓨" };
        foreach (var culture in new[] { "zh-CN", "ja-JP" })
        {
            var text = string.Join('\n', AppLocalizationResources.All[culture].Values);
            Assert.DoesNotContain(banned, token => text.Contains(token, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void LocalizerFallsBackAndFormatsMessages()
    {
        var localizer = new AppLocalizationManager("missing");

        Assert.Equal("zh-CN", localizer.CurrentCultureName);
        Assert.Equal("已载入 2 个章节", localizer.Format("Status.LoadedChapters", new Dictionary<string, object?> { ["count"] = 2 }));

        localizer.SetCulture("en-US");

        Assert.Equal("Loaded 2 chapters", localizer.Format("Status.LoadedChapters", new Dictionary<string, object?> { ["count"] = 2 }));
    }

    [Fact]
    public void LanguageToolListsAllSupportedLanguages()
    {
        var owner = CreateViewModel(new AppLocalizationManager("en-US"));
        var tool = new LanguageToolViewModel(owner);

        Assert.Equal(["zh-CN", "en-US", "ja-JP"], tool.Languages.Select(static language => language.CultureName).ToArray());
        Assert.Contains(tool.Languages, static language => language.DisplayName == "Japanese");
    }

    private static string[] Placeholders(string value) =>
        Regex.Matches(value, @"\{(?<name>[A-Za-z0-9_]+)\}")
            .Select(static match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static MainWindowViewModel CreateViewModel(IAppLocalizer localizer) =>
        new(
            new FakeLoadService(),
            new FakeSaveService(),
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            new FakeWindowService(),
            new ChapterTimeFormatter(),
            localizer: localizer);

    private sealed class FakeLoadService : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterImportResult(
                true,
                [new ChapterInfoGroup(path, [new ChapterSourceOption("default", "default", new ChapterInfo(path, path, 0, "OGM", 24, TimeSpan.Zero, Array.Empty<Chapter>()))], 0)],
                Array.Empty<ChapterDiagnostic>()));
    }

    private sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(ChapterInfo info, ChapterExportOptions options, string? directory, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", Array.Empty<ChapterDiagnostic>()));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
