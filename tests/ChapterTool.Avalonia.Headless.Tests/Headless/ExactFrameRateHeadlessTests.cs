using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ExactFrameRateHeadlessTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Custom_rate_reselection_and_language_refresh_preserve_authority(bool approximate)
    {
        var rate = approximate ? FrameRateValue.Approximate(23.975m) : FrameRateValue.Rational(12345, 512);
        var info = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, rate, TimeSpan.FromSeconds(60),
            [new Chapter(1, TimeSpan.FromTicks(520_940_000), "Custom chapter")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", info)));
        await host.LoadAsync("custom.txt");
        var selector = host.RequiredControl<ComboBox>("FrameRateBox");
        var customIndex = selector.SelectedIndex;
        Assert.True(customIndex >= 8);
        var cursor = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP" })
        {
            host.Localizer.SetCulture(culture);
            selector.SelectedIndex = 0;
            await host.LayoutAsync();
            Assert.Equal(rate, host.ViewModel.ExpressionFrameRate);
            selector.SelectedIndex = customIndex;
            await host.LayoutAsync();
            Assert.Equal(customIndex, selector.SelectedIndex);
            Assert.Equal(rate, host.ViewModel.ExpressionFrameRate);
            Assert.Equal(rate, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.FrameRate);
            Assert.Equal(520_940_000, host.ViewModel.Rows[0].Chapter.StartTime.Ticks);
            Assert.Equal(cursor, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedText(selector, rate.ToString()));
        }
    }

    [AvaloniaTheory]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    public async Task Exact_import_unchanged_frame_editor_candidate_and_history_survive_resizing(string culture)
    {
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_fch.mpls");
        var imported = await new MplsChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);
        Assert.True(imported.Success);
        using var host = new MainWindowHeadlessTestHost(imported, new AppLocalizationManager(culture));
        await host.LoadAsync(path);
        Assert.Equal(FrameRateValue.Rational(24000, 1001), host.ViewModel.Workspace.ContentSession!.Snapshot.Document.FrameRate);
        var original = host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks;
        var cursor = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        host.RequiredControl<CheckBox>("RoundFramesBox").IsChecked = true;
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        grid.SelectedItem = host.ViewModel.Rows[1];
        grid.CurrentColumn = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterGridColumnIds.Frames);
        grid.Focus();
        host.Window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, string.Empty);
        await host.LayoutAsync();
        Assert.Single(grid.GetVisualDescendants().OfType<TextBox>(), box => box.Classes.Contains("gridEditor"));
        MainWindowHeadlessTestHost.CaptureRenderedFrame(host.Window, $"artifacts/exact-frame-rate-domain/{culture}/desktop-editor.png");
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, string.Empty);
        await host.LayoutAsync();
        Assert.Equal(original, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks);
        Assert.Equal(cursor, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
        host.ViewModel.Expression = "t + 0.0002";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.CanApplyContentPreview);
        var preview = host.ViewModel.ExpressionPreviewText;
        host.RequiredControl<CheckBox>("RoundFramesBox").IsChecked = false;
        Assert.Equal(preview, host.ViewModel.ExpressionPreviewText);
        foreach (var (name, width, height) in new[] { ("default", 800d, 600d), ("wide", 1280d, 800d), ("narrow", 760d, 520d) })
        {
            await host.LayoutAsync(width, height);
            Assert.True(host.ViewModel.CanApplyContentPreview);
            Assert.Equal(original, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks);
            Assert.True(host.ViewModel.Rows[1].HasPreviewFramesChange);
            Assert.Equal(host.ViewModel.SelectedFrameRateIndex, host.RequiredControl<ComboBox>("FrameRateBox").SelectedIndex);
            MainWindowHeadlessTestHost.CaptureRenderedFrame(host.Window, $"artifacts/exact-frame-rate-domain/{culture}/desktop-{name}.png");
        }

        await host.ViewModel.ApplyContentPreviewCommand.ExecuteAsync();
        Assert.Equal(original + 2000, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks);
        await host.ViewModel.UndoCommand.ExecuteAsync();
        Assert.Equal(original, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks);
        await host.ViewModel.RedoCommand.ExecuteAsync();
        Assert.Equal(original + 2000, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[1].StartTicks);
    }
}
