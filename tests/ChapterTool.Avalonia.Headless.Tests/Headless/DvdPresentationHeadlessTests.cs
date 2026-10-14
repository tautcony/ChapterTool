using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class DvdPresentationHeadlessTests
{
    [AvaloniaTheory]
    [InlineData("zh-CN", "标题 2", "19 个章节", "1 个章节", "已读取 DVD 光盘：3 个标题，39 个章节，2 个标题集。")]
    [InlineData("en-US", "Title 2", "19 chapters", "1 chapter", "DVD loaded: titles=3, chapters=39, title sets=2.")]
    [InlineData("ja-JP", "タイトル 2", "19 個のチャプター", "1 個のチャプター", "DVD を読み込みました：3 個のタイトル、39 個のチャプター、2 個のタイトルセット。")]
    public async Task DvdSelectionAndLocalizedTextRemainUsableAcrossWindowSizes(string culture, string title, string chapters, string singularChapters, string diagnosticText)
    {
        var directory = Directory.CreateTempSubdirectory("chaptertool-dvd-presentation-");
        try
        {
            var fixture = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VmgRecovery", "TheresaDisc4");
            foreach (var name in new[] { "VIDEO_TS.IFO", "VTS_01_0.IFO", "VTS_02_0.IFO" })
            {
                File.Copy(Path.Combine(fixture, name), Path.Combine(directory.FullName, name));
            }

            File.Copy(Path.Combine(fixture, "VTS_01_0.IFO"), Path.Combine(directory.FullName, "VTS_01_0.BUP"));
            for (var number = 1; number <= 9; number++)
            {
                await File.WriteAllBytesAsync(Path.Combine(directory.FullName, $"VTS_01_{number}.VOB"), []);
            }

            var path = Path.Combine(directory.FullName, "VIDEO_TS.IFO");
            var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);
            Assert.True(result.Success);
            using var host = new MainWindowHeadlessTestHost(result, new AppLocalizationManager(culture));
            await host.LoadAsync(path);
            var selector = host.RequiredControl<ComboBox>("ClipBox");
            selector.SelectedIndex = 1;
            var sources = string.Join("+", Enumerable.Range(1, 9).Select(static number => $"VTS_01_{number}"));
            var expected = $"{title} (1:02:16) [{sources}]（{chapters}）";
            var diagnostic = Assert.Single(result.Diagnostics, static item => item.Code == ChapterDiagnosticCode.IfoDiscLoaded);
            Assert.Equal(diagnosticText, host.ViewModel.LocalizeDiagnostic(diagnostic));
            Assert.Equal(9, diagnostic.Arguments!["titleVobCount"]);

            foreach (var (name, width, height) in new[] { ("default", 800d, 600d), ("wide", 1280d, 720d), ("narrow", 760d, 520d) })
            {
                await host.LayoutAsync(width, height);
                Assert.Equal(1, host.ViewModel.SelectedClipIndex);
                Assert.Equal(expected, selector.SelectionBoxItem?.ToString());
                Assert.Equal(expected, ToolTip.GetTip(selector));
                Assert.True(MainWindowHeadlessTestHost.ContainsRenderedText(selector, expected));
                Assert.Equal(19, host.ViewModel.Rows.Count);
                Assert.Equal("Chapter 01", host.ViewModel.Rows[0].Name);
                Assert.True(host.ViewModel.Rows[1].IsFrameAccurate);
                Assert.Contains(result.Diagnostics, item => item.Code == ChapterDiagnosticCode.IfoTimingEstimated);
                Assert.True(host.ViewModel.CanSave);
                Assert.True(selector.Bounds.Width >= selector.MinWidth);
                Assert.True(selector.Bounds.Width <= selector.MaxWidth);
                MainWindowHeadlessTestHost.CaptureRenderedFrame(host.Window, Path.Combine("artifacts", "dvd-presentation", culture, $"main-{name}.png"));
            }

            selector.SelectedIndex = 2;
            await host.LayoutAsync();
            Assert.Single(host.ViewModel.Rows);
            Assert.EndsWith($"（{singularChapters}）", selector.SelectionBoxItem?.ToString(), StringComparison.Ordinal);
            Assert.Equal(singularChapters, host.ViewModel.ChapterCountDisplay);

            host.Localizer.SetCulture("en-US");
            await host.LayoutAsync();
            Assert.StartsWith("Title 3 (", selector.SelectionBoxItem?.ToString(), StringComparison.Ordinal);
            Assert.EndsWith("（1 chapter）", selector.SelectionBoxItem?.ToString(), StringComparison.Ordinal);
            Assert.Equal("Title 3", result.Groups.Single().Entries[2].ChapterSet.Title);
            Assert.Equal("Chapter 01", host.ViewModel.Rows[0].Name);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
