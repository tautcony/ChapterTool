using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ExactFrameRateHeadlessTests
{
    [AvaloniaFact]
    public async Task Frame_time_frame_edits_commit_and_round_trip_through_history()
    {
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, FrameRateValue.Rational(25, 1),
            TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "First"), new Chapter(2, TimeSpan.FromSeconds(10), "Second"),
             new Chapter(3, TimeSpan.FromSeconds(20), "Third")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var content = host.ViewModel.Workspace.ContentSession!;

        await host.ViewModel.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(0, "25"));
        Assert.Equal(TimeSpan.TicksPerSecond, content.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        await host.ViewModel.EditTimeCommand.ExecuteAsync(new ChapterCellEdit(0, "00:00:02.000"));
        Assert.Equal(2 * TimeSpan.TicksPerSecond, content.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        await host.ViewModel.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(0, "25"));

        Assert.Equal(TimeSpan.TicksPerSecond, content.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        await host.ViewModel.UndoCommand.ExecuteAsync();
        Assert.Equal(2 * TimeSpan.TicksPerSecond, content.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        await host.ViewModel.RedoCommand.ExecuteAsync();
        Assert.Equal(TimeSpan.TicksPerSecond, content.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
    }

    [AvaloniaFact]
    public async Task Normalize_delete_commits_and_round_trips_through_history()
    {
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, FrameRateValue.Rational(25, 1),
            TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "First"), new Chapter(2, TimeSpan.FromSeconds(10), "Second"),
             new Chapter(3, TimeSpan.FromSeconds(20), "Third")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        host.ViewModel.ApplyEditingOptions(new ChapterEditingOptions(DeleteRowsTimingMode.Normalize));
        var content = host.ViewModel.Workspace.ContentSession!;

        Assert.Equal(DeleteRowsTimingMode.Normalize, host.ViewModel.EditingOptions.DeleteRowsTiming);
        await host.ViewModel.DeleteCommand.ExecuteAsync(new HashSet<int> { 0 });
        Assert.Equal(new[] { 0L, 10 * TimeSpan.TicksPerSecond },
            content.Snapshot.Document.Tracks[0].Chapters.Select(static chapter => chapter.StartTicks));

        await host.ViewModel.UndoCommand.ExecuteAsync();
        Assert.Equal(new[] { 0L, 10 * TimeSpan.TicksPerSecond, 20 * TimeSpan.TicksPerSecond },
            content.Snapshot.Document.Tracks[0].Chapters.Select(static chapter => chapter.StartTicks));
        await host.ViewModel.RedoCommand.ExecuteAsync();
        Assert.Equal(new[] { 0L, 10 * TimeSpan.TicksPerSecond },
            content.Snapshot.Document.Tracks[0].Chapters.Select(static chapter => chapter.StartTicks));
    }

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

    [AvaloniaFact]
    public async Task Frame_rate_conversion_review_preserves_exact_rates_candidate_and_history_across_languages()
    {
        var sourceRate = FrameRateValue.Rational(24000, 1001);
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, sourceRate,
            TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "Opening"), new Chapter(2, TimeSpan.FromSeconds(10), "Scene")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var target = new FrameRateService().Options.Single(option => option.Code == "Fps25");
        host.ViewModel.SelectedFrameRateIndex = target.MplsFrameRateCode;
        var originalTicks = host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters
            .Select(chapter => chapter.StartTicks).ToArray();
        var historyCursor = host.ViewModel.ToolSession.History.Snapshot!.CursorId;

        await host.ViewModel.ChangeFpsCommand.ExecuteAsync();
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.Contains("24000/1001", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
        Assert.Contains("25/1", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
        Assert.Contains(host.Localizer.GetString("Main.FrameRateReviewTimesChanged"), host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
        Assert.Equal(host.ViewModel.FrameRateReviewTooltip, ToolTip.GetTip(host.RequiredControl<Button>("ChangeFpsButton")));
        Assert.DoesNotContain(host.Window.GetVisualDescendants().OfType<TextBlock>(), block => block.Name == "FrameRateConversionReview");
        Assert.Null(host.Window.FindControl<Button>("FrameAccuracyInfoButton"));
        Assert.Equal(originalTicks, host.ViewModel.Workspace.ContentSession.Snapshot.Document.Tracks[0].Chapters
            .Select(chapter => chapter.StartTicks));
        Assert.Equal(historyCursor, host.ViewModel.ToolSession.History.Snapshot!.CursorId);

        host.RequiredControl<CheckBox>("RoundFramesBox").IsChecked = false;
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP" })
        {
            host.Localizer.SetCulture(culture);
            await host.LayoutAsync();
            Assert.True(host.ViewModel.CanApplyContentPreview);
            Assert.Contains("24000/1001", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
            Assert.Contains("25/1", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
            Assert.Contains(host.Localizer.GetString("Main.FrameRateReviewTimesChanged"), host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
            Assert.Equal(originalTicks, host.ViewModel.Workspace.ContentSession.Snapshot.Document.Tracks[0].Chapters
                .Select(chapter => chapter.StartTicks));
            Assert.Equal(historyCursor, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
        }

        await host.ViewModel.CancelContentPreviewCommand.ExecuteAsync();
        Assert.Equal(host.Localizer.GetString("Main.ChangeFps"), host.ViewModel.FrameRateReviewTooltip);
        Assert.False(host.ViewModel.IsContentPreviewPending);
    }

    [AvaloniaFact]
    public async Task User_adjusted_time_column_width_survives_responsive_layout_changes()
    {
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, FrameRateValue.Rational(25, 1),
            TimeSpan.FromSeconds(30), [new Chapter(1, TimeSpan.Zero, "A chapter name that needs readable width")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var time = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterGridColumnIds.Time);
        var name = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterGridColumnIds.Name);
        var width = new DataGridLength(420);
        time.Width = width;

        foreach (var (viewportWidth, viewportHeight) in new[] { (1280d, 800d), (900d, 600d), (760d, 520d) })
        {
            await host.LayoutAsync(viewportWidth, viewportHeight);
            Assert.Equal(width.Value, time.Width.Value);
            Assert.Equal(width.UnitType, time.Width.UnitType);
            Assert.True(name.MinWidth >= 128);
        }
    }

    [AvaloniaFact]
    public async Task Approximate_frame_rate_conversion_review_marks_its_provenance()
    {
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm,
            FrameRateValue.Approximate(23.975m), TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "Opening"), new Chapter(2, TimeSpan.FromSeconds(10), "Scene")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var target = new FrameRateService().Options.Single(option => option.Code == "Fps25");
        host.ViewModel.SelectedFrameRateIndex = target.MplsFrameRateCode;

        await host.ViewModel.ChangeFpsCommand.ExecuteAsync();

        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.Contains("≈23.975", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
        Assert.Contains("25/1", host.ViewModel.FrameRateReviewTooltip, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Loading_template_replaces_frame_conversion_review_with_name_candidate()
    {
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm,
            FrameRateValue.Rational(24000, 1001), TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.Zero, "Opening"), new Chapter(2, TimeSpan.FromSeconds(10), "Scene")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var target = new FrameRateService().Options.Single(option => option.Code == "Fps25");
        host.ViewModel.SelectedFrameRateIndex = target.MplsFrameRateCode;
        await host.ViewModel.ChangeFpsCommand.ExecuteAsync();
        Assert.True(host.ViewModel.HasFrameRateReview);
        Assert.NotEmpty(host.ViewModel.FrameRateReviewTooltip);
        Assert.Null(host.Window.FindControl<TextBlock>("FrameRateConversionReview"));
        var actionGroup = host.RequiredControl<StackPanel>("ExpressionActions");
        var optionsButton = host.RequiredControl<Button>("PreviewContentOptionsButton");
        var applyButton = host.RequiredControl<Button>("ApplyContentPreviewButton");
        var cancelButton = host.RequiredControl<Button>("CancelContentPreviewButton");
        var expressionEditor = host.RequiredControl<Control>("ExpressionBox");
        var optionsGrid = host.RequiredControl<Grid>("ExpressionOptionsGroup");
        var conversionButton = host.RequiredControl<Button>("ChangeFpsButton");
        foreach (var (size, width, height) in new[]
        {
            ("default", 1280d, 800d),
            ("wide", 1920d, 1080d),
            ("narrow", 760d, 520d)
        })
        {
            await host.LayoutAsync(width, height);
            var groupOrigin = actionGroup.TranslatePoint(default, host.Window);
            var groupParentOrigin = optionsGrid.TranslatePoint(default, host.Window);
            var conversionOrigin = conversionButton.TranslatePoint(default, host.Window);
            Assert.NotNull(groupOrigin);
            Assert.NotNull(groupParentOrigin);
            Assert.NotNull(conversionOrigin);
            Assert.Equal(
                groupParentOrigin.Value.X + optionsGrid.Bounds.Width,
                groupOrigin.Value.X + actionGroup.Bounds.Width,
                precision: 0);
            Assert.Equal(
                groupOrigin.Value.X + actionGroup.Bounds.Width,
                conversionOrigin.Value.X + conversionButton.Bounds.Width,
                precision: 0);
            var optionsOrigin = optionsButton.TranslatePoint(default, host.Window);
            var editorOrigin = expressionEditor.TranslatePoint(default, host.Window);
            Assert.NotNull(optionsOrigin);
            Assert.NotNull(editorOrigin);
            Assert.Equal(
                optionsOrigin.Value.Y + optionsButton.Bounds.Height / 2,
                editorOrigin.Value.Y + expressionEditor.Bounds.Height / 2,
                precision: 1);
            Assert.Equal(expressionEditor.Bounds.Height, applyButton.Bounds.Height, precision: 0);
            Assert.Equal(expressionEditor.Bounds.Height, cancelButton.Bounds.Height, precision: 0);
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                $"artifacts/clarify-editing-workflow-ux/desktop/frame-conversion-{size}.png");
        }

        var path = Path.Combine(Path.GetTempPath(), $"chaptertool-template-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(path, "Opening template\nScene template\n");
            await host.ViewModel.LoadChapterNameTemplateFromPathAsync(path, TestContext.Current.CancellationToken);

            Assert.True(host.ViewModel.IsContentPreviewPending);
            Assert.False(host.ViewModel.HasFrameRateReview);
            Assert.Equal(host.Localizer.GetString("Main.ChangeFps"), host.ViewModel.FrameRateReviewTooltip);
            Assert.True(host.ViewModel.CanApplyContentPreview);
            Assert.Contains(host.ViewModel.Rows, row => row.HasPreviewNameChange);
        }
        finally
        {
            File.Delete(path);
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
        foreach (var (name, width, height) in new[] { ("default", 800d, 600d), ("medium", 900d, 600d), ("wide", 1280d, 800d), ("narrow", 760d, 520d) })
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

    [AvaloniaFact]
    public async Task Unchanged_rounded_frame_cell_commit_keeps_ticks_and_history()
    {
        const long startTicks = 100_000_000;
        var source = new ChapterSet("Custom", "custom.txt", ChapterImportFormat.Ogm, FrameRateValue.Rational(24000, 1001),
            TimeSpan.FromSeconds(30),
            [new Chapter(1, TimeSpan.FromTicks(startTicks), "Opening", "240")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult("custom.txt",
            new ChapterImportEntry("custom", "Custom", source)));
        await host.LoadAsync("custom.txt");
        var cursor = host.ViewModel.ToolSession.History.Snapshot!.CursorId;
        host.RequiredControl<CheckBox>("RoundFramesBox").IsChecked = true;
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        grid.SelectedItem = host.ViewModel.Rows[0];
        grid.CurrentColumn = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterGridColumnIds.Frames);
        grid.Focus();
        host.Window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, string.Empty);
        await host.LayoutAsync();

        var editor = Assert.Single(grid.GetVisualDescendants().OfType<TextBox>(), box => box.Classes.Contains("gridEditor"));
        Assert.Equal("240", editor.Text);
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, string.Empty);
        await host.LayoutAsync();

        Assert.Equal(startTicks, host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks[0].Chapters[0].StartTicks);
        Assert.Equal(cursor, host.ViewModel.ToolSession.History.Snapshot!.CursorId);
    }
}
