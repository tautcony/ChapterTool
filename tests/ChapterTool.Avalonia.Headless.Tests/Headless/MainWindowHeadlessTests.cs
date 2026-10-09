using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public async Task Inline_candidate_keeps_original_rows_selection_and_workspace_bounds()
    {
        var chapterNames = Enumerable.Range(1, 1000).Select(index => $"Chapter {index:D4} · {new string('x', 80)}").ToArray();
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", chapterNames)));
        await host.LoadAsync("movie.txt");
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var workspace = host.RequiredControl<Grid>("ChapterWorkspaceGrid");
        var viewportSizes = new (double Width, double Height)[]
        {
            (760, 600),
            (1280, 800),
            (760, 520),
            (860, 600),
            (861, 600)
        };
        host.SelectRows(0, 999);
        Assert.Equal(host.Localizer.GetString("Grid.Time"), grid.Columns[1].Header);
        var selectedRows = host.ViewModel.SelectedRowCount;
        var rows = host.ViewModel.Rows.ToArray();
        var chapterIds = rows.Select(row => row.ChapterId).ToArray();
        Assert.Equal(1000, chapterIds.Distinct().Count());
        Assert.All(rows, row =>
        {
            Assert.False(row.HasPreviewTimeChange);
            Assert.False(row.HasPreviewNameChange);
            Assert.False(row.HasPreviewNumberChange);
            Assert.False(row.HasPreviewFramesChange);
        });
        var originalBounds = new Dictionary<(double Width, double Height), (double GridHeight, double WorkspaceHeight)>();
        foreach (var size in viewportSizes)
        {
            await host.LayoutAsync(size.Width, size.Height);
            originalBounds[size] = (grid.Bounds.Height, workspace.Bounds.Height);
        }

        host.ViewModel.Expression = "t + 1";
        Assert.True(host.ViewModel.IsContentPreviewPending);
        Assert.False(host.ViewModel.IsInlineCandidateVisible);
        foreach (var (width, height) in viewportSizes)
        {
            await host.LayoutAsync(width, height);
            var baseline = originalBounds[(width, height)];
            Assert.InRange(Math.Abs(grid.Bounds.Height - baseline.GridHeight), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - baseline.WorkspaceHeight), 0, 1);
        }

        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.True(host.ViewModel.IsInlineCandidateVisible);
        Assert.Equal(host.Localizer.GetString("Grid.Time"), grid.Columns[1].Header);
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewSummary"));
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewDetailsButton"));
        Assert.True(host.RequiredControl<Button>("ApplyContentPreviewButton").IsEnabled);
        Assert.True(grid.IsReadOnly);
        Assert.False(host.ViewModel.CanEditRows);
        Assert.Equal("00:00:00.000", rows[0].TimeText);
        Assert.Equal("00:00:01.000", rows[0].PreviewTimeText);
        Assert.Contains("00:00:00.000", rows[0].PreviewTimeAccessibleName, StringComparison.Ordinal);
        Assert.Contains("00:00:01.000", rows[0].PreviewTimeAccessibleName, StringComparison.Ordinal);
        Assert.Contains(grid.GetVisualDescendants(), control => control.GetType().Name == "Icon" && control.IsVisible);
        var accessibleTimePreview = grid.GetVisualDescendants().OfType<StackPanel>().First(panel => panel.IsVisible
            && panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == rows[0].PreviewTimeText));
        Assert.Equal(rows[0].PreviewTimeAccessibleName, AutomationProperties.GetName(accessibleTimePreview));
        Assert.False(rows[0].HasPreviewNameChange);
        Assert.Equal("Chapter 0001 · " + new string('x', 80), rows[0].Name);
        Assert.Equal(chapterIds, host.ViewModel.Rows.Select(row => row.ChapterId).ToArray());

        var nameColumn = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterTool.Avalonia.UI.ViewModels.ChapterGridColumnIds.Name);
        grid.SelectedItem = rows[^1];
        grid.ScrollIntoView(rows[^1], nameColumn);
        Assert.Same(rows[^1], grid.SelectedItem);
        Assert.Equal("Chapter 1000 · " + new string('x', 80), rows[^1].Name);
        Assert.True(rows[^1].HasPreviewTimeChange);

        var verificationSizes = new (double Width, double Height)[]
        {
            (760, 600),
            (1280, 800),
            (760, 520),
            (860, 600),
            (861, 600)
        };
        foreach (var (width, height) in verificationSizes)
        {
            await host.LayoutAsync(width, height);
            Assert.Single(host.RequiredControl<Grid>("ChapterWorkspaceGrid").RowDefinitions);
            var baseline = originalBounds[(width, height)];
            Assert.InRange(Math.Abs(grid.Bounds.Height - baseline.GridHeight), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - baseline.WorkspaceHeight), 0, 1);
            Assert.Equal(selectedRows, host.ViewModel.SelectedRowCount);
            Assert.Equal("t + 1", host.ViewModel.Expression);
            Assert.Equal(width <= 860, rows[0].IsNarrowPreviewLayout);
        }

        var englishCandidate = host.ViewModel.ExpressionPreviewText;
        foreach (var width in new[] { 860d, 861d, 1280d, 760d })
        {
            await host.LayoutAsync(width, width == 760 ? 520 : 600);
            Assert.Equal(englishCandidate, host.ViewModel.ExpressionPreviewText);
            Assert.True(host.RequiredControl<Button>("ApplyContentPreviewButton").IsEnabled);
            Assert.True(host.RequiredControl<Button>("CancelContentPreviewButton").IsVisible);
            Assert.Equal(selectedRows, host.ViewModel.SelectedRowCount);
            Assert.Equal("t + 1", host.ViewModel.Expression);
        }
        Assert.DoesNotContain("Chapter 1000", host.ViewModel.ExpressionPreviewText, StringComparison.Ordinal);

        host.Localizer.SetCulture("zh-CN");
        host.ViewModel.RefreshExpressionPreviewNow();
        foreach (var (width, height) in verificationSizes)
        {
            await host.LayoutAsync(width, height);
            Assert.Single(host.RequiredControl<Grid>("ChapterWorkspaceGrid").RowDefinitions);
            var baseline = originalBounds[(width, height)];
            Assert.InRange(Math.Abs(grid.Bounds.Height - baseline.GridHeight), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - baseline.WorkspaceHeight), 0, 1);
            Assert.Equal(selectedRows, host.ViewModel.SelectedRowCount);
            Assert.Equal("t + 1", host.ViewModel.Expression);
        }

        host.ViewModel.Expression = "t * 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        if (host.ViewModel.CanApplyContentPreview)
        {
            Assert.Contains(rows, row => row.HasPreviewFramesChange);
            await host.MainView.ApplyContentPreviewCommand.ExecuteAsync();
            rows = host.ViewModel.Rows.ToArray();
        }

        host.ViewModel.Expression = "t * 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.False(host.ViewModel.CanApplyContentPreview, host.ViewModel.ExpressionPreviewText);
        Assert.False(host.ViewModel.IsInlineCandidateVisible);
        Assert.False(rows[0].HasPreviewTimeChange);
        foreach (var (width, height) in viewportSizes)
        {
            await host.LayoutAsync(width, height);
            var baseline = originalBounds[(width, height)];
            Assert.InRange(Math.Abs(grid.Bounds.Height - baseline.GridHeight), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - baseline.WorkspaceHeight), 0, 1);
        }

        host.ViewModel.Expression = "t + (";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.False(host.ViewModel.CanApplyContentPreview);
        Assert.False(host.ViewModel.IsInlineCandidateVisible);
        Assert.False(rows[0].HasPreviewTimeChange);
        foreach (var (width, height) in viewportSizes)
        {
            await host.LayoutAsync(width, height);
            var baseline = originalBounds[(width, height)];
            Assert.InRange(Math.Abs(grid.Bounds.Height - baseline.GridHeight), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - baseline.WorkspaceHeight), 0, 1);
        }

        await host.LayoutAsync(width: 760, height: 520);

        var historyButton = host.RequiredControl<Button>("HistoryButton");

        Assert.Single(workspace.RowDefinitions);
        Assert.True(grid.Bounds.Height >= workspace.Bounds.Height - 1);
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewRegion"));
        Assert.Null(host.Window.FindControl<Control>("SessionHistoryPanel"));
        Assert.True(historyButton.IsEnabled);
        grid.SelectedItem = rows[^1];
        grid.ScrollIntoView(rows[^1], nameColumn);
        Assert.Same(rows[^1], grid.SelectedItem);
        Assert.Equal("Chapter 1000 · " + new string('x', 80), rows[^1].Name);
        Assert.False(rows[^1].HasPreviewTimeChange);
        grid.ScrollIntoView(rows[0], nameColumn);
        await host.LayoutAsync(width: 760, height: 520);
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewDetailsButton"));
    }

    [AvaloniaFact]
    public async Task Visible_display_frames_survive_preview_even_when_source_frame_information_is_missing()
    {
        var info = new ChapterSet(
            "movie.txt",
            "movie.txt",
            ChapterImportFormat.Ogm,
            24000d / 1001d,
            TimeSpan.Zero,
            [
                new Chapter(1, TimeSpan.Zero, "Opening"),
                new Chapter(2, TimeSpan.FromMilliseconds(348557), "Next")
            ]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            new ChapterImportEntry("movie.txt", "movie.txt", info)));
        await host.LoadAsync("movie.txt");
        host.ViewModel.SetFrameOptions(frameRateIndex: 1, roundFrames: true);
        host.ViewModel.RefreshRowsFromPort();
        var rows = host.ViewModel.Rows.ToArray();

        Assert.Equal("0", rows[0].FramesInfo);
        Assert.Equal("8357", rows[1].FramesInfo);
        host.ViewModel.Expression = "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        await host.LayoutAsync(1280, 800);

        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.Equal("0", rows[0].PreviewBeforeFrames);
        Assert.Equal("24", rows[0].PreviewFrames);
        Assert.Equal("8357", rows[1].PreviewBeforeFrames);
        Assert.Equal("8381", rows[1].PreviewFrames);
        Assert.Equal(24000m / 1001m, rows[1].PreviewBeforeFramesPerSecond);
        Assert.True(rows[1].PreviewComparison?.BeforeFrames?.IsMissing);
    }

    [AvaloniaFact]
    public async Task Inline_frame_preview_uses_visible_and_candidate_accuracy_colors()
    {
        var info = new ChapterSet(
            "movie.txt",
            "movie.txt",
            ChapterImportFormat.Ogm,
            24000d / 1001d,
            TimeSpan.Zero,
            [
                new Chapter(1, TimeSpan.Zero, "Accurate before"),
                new Chapter(2, TimeSpan.FromMilliseconds(1), "Inexact before"),
                new Chapter(3, TimeSpan.FromMilliseconds(348557), "Visible frame value")
            ]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            new ChapterImportEntry("movie.txt", "movie.txt", info)));
        var application = global::Avalonia.Application.Current!;
        var themeKeys = new[]
        {
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey,
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey,
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey
        };
        var originalResources = themeKeys.ToDictionary(key => key, key => application.Resources[key]);
        var originalThemeVariant = application.RequestedThemeVariant;
        try
        {
            await host.LoadAsync("movie.txt");
            host.ViewModel.FrameAccuracyTolerance = 0.01m;
            host.ViewModel.SetFrameOptions(frameRateIndex: 1, roundFrames: true);
            host.ViewModel.RefreshRowsFromPort();
            var rows = host.ViewModel.Rows.ToArray();
            Assert.All(rows, row => Assert.True(row.PreviewComparison is null));
            var sourceChapters = Assert.Single(host.ViewModel.Workspace.ContentSession!.Snapshot.Document.Tracks).Chapters;
            Assert.All(sourceChapters, chapter => Assert.True(string.IsNullOrEmpty(chapter.FramesInfo)));
            Assert.All(sourceChapters, chapter => Assert.Equal(FrameAccuracy.Neutral, chapter.FrameAccuracy));
            var chapterIds = rows.Select(row => row.ChapterId!.Value).ToArray();

            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            await host.LayoutAsync(1280, 800);

            Assert.Equal(FrameAccuracy.Accurate, rows[0].Chapter.FrameAccuracy);
            Assert.Equal(FrameAccuracy.Inexact, rows[1].Chapter.FrameAccuracy);
            Assert.Equal(FrameAccuracy.Neutral, rows[0].PreviewComparison?.BeforeFrames?.Accuracy);
            Assert.Equal(FrameAccuracy.Accurate, rows[0].PreviewBeforeFrameAccuracy);
            Assert.Equal(FrameAccuracy.Inexact, rows[0].PreviewCandidateFrameAccuracy);
            Assert.Equal(FrameAccuracy.Inexact, rows[1].PreviewBeforeFrameAccuracy);
            Assert.Equal(FrameAccuracy.Accurate, rows[1].PreviewCandidateFrameAccuracy);
            Assert.Equal("8357", rows[2].PreviewBeforeFrames);
            Assert.Equal(FrameAccuracy.Inexact, rows[2].PreviewBeforeFrameAccuracy);

            var themeService = new ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService();
            foreach (var theme in new[] { "default", "ayu-dark" })
            {
                themeService.Apply(theme == "default"
                    ? ChapterTool.Contracts.Configuration.ThemeSettings.Default
                    : new ChapterTool.Contracts.Configuration.ThemeSettings(theme));
                await host.LayoutAsync(1280, 800);

                AssertFramePreviewColor(host, rows[0], rows[0].PreviewBeforeFrames,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey);
                AssertFramePreviewColor(host, rows[0], rows[0].PreviewFrames,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey);
                AssertFramePreviewColor(host, rows[1], rows[1].PreviewBeforeFrames,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey);
                AssertFramePreviewColor(host, rows[1], rows[1].PreviewFrames,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey);
                AssertFramePreviewColor(host, rows[2], rows[2].PreviewBeforeFrames,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey);
            }

            await host.LayoutAsync(760, 520);
            AssertFramePreviewColor(host, rows[0], rows[0].PreviewBeforeFrames,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey);
            AssertFramePreviewColor(host, rows[0], rows[0].PreviewFrames,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey);
            await host.LayoutAsync(1280, 800);
            await host.ViewModel.ApplyContentPreviewCommand.ExecuteAsync();
            await host.LayoutAsync(1280, 800);
            var appliedRows = host.ViewModel.Rows.ToDictionary(row => row.ChapterId!.Value);
            var appliedRow0 = appliedRows[chapterIds[0]];
            var appliedRow1 = appliedRows[chapterIds[1]];
            Assert.False(appliedRow0.HasPreviewFramesChange);
            Assert.False(appliedRow1.HasPreviewFramesChange);
            AssertNormalFrameColor(host, appliedRow0, appliedRow0.FramesInfo,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey);
            AssertNormalFrameColor(host, appliedRow1, appliedRow1.FramesInfo,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey);
        }
        finally
        {
            foreach (var (key, value) in originalResources)
            {
                application.Resources[key] = value;
            }

            application.RequestedThemeVariant = originalThemeVariant;
        }
    }

    [AvaloniaFact]
    public async Task Neutral_inline_frame_values_keep_neutral_colors_and_discard_restores_rows()
    {
        var application = global::Avalonia.Application.Current!;
        var themeKeys = new[]
        {
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey,
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey,
            ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey
        };
        var originalResources = themeKeys.ToDictionary(key => key, key => application.Resources[key]);
        var originalThemeVariant = application.RequestedThemeVariant;
        var themeService = new ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService();
        try
        {
            themeService.Apply(ChapterTool.Contracts.Configuration.ThemeSettings.Default);
            using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                "movie.txt",
                MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Opening", "Next")));
            await host.LoadAsync("movie.txt");
            host.ViewModel.SetFrameOptions(frameRateIndex: 1, roundFrames: false);
            host.ViewModel.RefreshRowsFromPort();
            var rows = host.ViewModel.Rows.ToArray();
            Assert.All(rows, row => Assert.True(row.IsFrameNeutral));

            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            await host.LayoutAsync(1280, 800);
            Assert.All(rows, row => Assert.Equal(FrameAccuracy.Neutral, row.PreviewBeforeFrameAccuracy));

            var comparison = Assert.IsType<ChapterTool.Core.Session.ExpressionChapterComparison>(rows[0].PreviewComparison);
            var neutralCandidateFrames = comparison.CandidateFrames is { } candidateFrames
                ? candidateFrames with { Accuracy = FrameAccuracy.Neutral }
                : new ChapterTool.Core.Session.ExpressionFrameValue(
                    rows[0].PreviewFrames,
                    null,
                    null,
                    FrameAccuracy.Neutral);
            rows[0].UpdatePreviewComparison(
                comparison with { CandidateFrames = neutralCandidateFrames },
                host.ViewModel.DisplayFrameRate,
                host.Localizer.GetString("Expression.Value.NotCalculated"));
            await host.LayoutAsync(1280, 800);
            AssertFramePreviewColor(host, rows[0], rows[0].PreviewBeforeFrames,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey);
            AssertFramePreviewColor(host, rows[0], rows[0].PreviewFrames,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey);

            rows[0].UpdatePreviewComparison(
                comparison,
                host.ViewModel.DisplayFrameRate,
                host.Localizer.GetString("Expression.Value.NotCalculated"));
            await host.ViewModel.CancelContentPreviewCommand.ExecuteAsync();
            await host.LayoutAsync(1280, 800);
            Assert.All(rows, row => Assert.Null(row.PreviewComparison));
            AssertNormalFrameColor(host, rows[0], rows[0].FramesInfo,
                ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey);
        }
        finally
        {
            foreach (var (key, value) in originalResources)
            {
                application.Resources[key] = value;
            }

            application.RequestedThemeVariant = originalThemeVariant;
        }
    }

    [AvaloniaFact]
    public async Task History_button_tracks_document_session_availability()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();
        var historyButton = host.RequiredControl<Button>("HistoryButton");

        Assert.False(historyButton.IsEnabled);

        await host.LoadAsync("movie.txt");
        Assert.True(historyButton.IsEnabled);
        Assert.Equal("HistoryButton", AutomationProperties.GetAutomationId(historyButton));
    }

    [AvaloniaFact]
    public async Task Empty_state_and_loaded_grid_share_the_same_workspace()
    {
        using var host = new MainWindowHeadlessTestHost([]);
        await host.LayoutAsync(width: 760, height: 600);
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var workspace = host.RequiredControl<Grid>("ChapterWorkspaceGrid");
        var emptyImage = host.RequiredControl<Control>("ChapterGridEmptyImage");
        var emptyBounds = grid.Bounds;

        Assert.True(emptyImage.IsVisible);
        Assert.False(emptyImage.IsHitTestVisible);
        Assert.True(host.ViewModel.IsChapterGridEmpty);

        await host.LoadAsync("movie.txt");
        await host.LayoutAsync(width: 760, height: 600);

        Assert.False(emptyImage.IsVisible);
        Assert.False(host.ViewModel.IsChapterGridEmpty);
        Assert.Equal(emptyBounds, grid.Bounds);
        Assert.Single(workspace.RowDefinitions);
    }

    [AvaloniaFact]
    public async Task Invalid_expression_preview_clears_inline_values_and_cannot_be_applied()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");
        host.ViewModel.Expression = "t + 1";
        host.ViewModel.RefreshExpressionPreviewNow();
        Assert.True(host.ViewModel.CanApplyContentPreview);
        host.ViewModel.OrderShift = 1;
        Assert.False(host.ViewModel.CanApplyContentPreview);
        Assert.Empty(host.ViewModel.ExpressionPreviewText);

        host.ViewModel.Expression = "t + (";
        host.ViewModel.RefreshExpressionPreviewNow();
        await host.LayoutAsync(width: 760, height: 520);

        Assert.Null(host.Window.FindControl<Control>("ContentPreviewRegion"));
        Assert.False(host.ViewModel.CanApplyContentPreview);
        Assert.False(host.RequiredControl<Button>("ApplyContentPreviewButton").IsEnabled);
        Assert.False(host.ViewModel.Rows[0].HasPreviewTimeChange);
        Assert.False(host.RequiredControl<DataGrid>("ChapterGrid").IsReadOnly);
        Assert.NotEmpty(host.ViewModel.ExpressionPreviewText);
        Assert.Equal(host.ViewModel.ExpressionPreviewText, host.RequiredControl<Control>("ExpressionBox").GetValue(ToolTip.TipProperty));

        await host.ViewModel.CancelContentPreviewCommand.ExecuteAsync();
        await host.LayoutAsync(width: 760, height: 520);
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewDetailsButton"));
    }

    [AvaloniaFact]
    public async Task Naming_order_and_frame_rate_candidates_project_into_the_original_rows()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Intro", "Ending")));
        await host.LoadAsync("movie.txt");
        host.ViewModel.AutoGenerateNames = false;
        host.ViewModel.UseTemplateNames = false;
        host.ViewModel.OrderShift = 0;
        await host.ViewModel.PreviewContentOptionsCommand.ExecuteAsync();
        Assert.False(host.ViewModel.CanApplyContentPreview);
        await host.ViewModel.CancelContentPreviewCommand.ExecuteAsync();

        host.ViewModel.AutoGenerateNames = true;
        host.ViewModel.OrderShift = 2;
        await host.ViewModel.PreviewContentOptionsCommand.ExecuteAsync();
        await host.LayoutAsync(width: 760, height: 600);

        Assert.True(host.RequiredControl<Button>("ApplyContentPreviewButton").IsVisible);
        Assert.Null(host.Window.FindControl<Control>("ContentPreviewDetailsButton"));
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.Empty(host.ViewModel.ContentPreviewMetadataTooltip);
        Assert.Empty(host.ViewModel.ExpressionPreviewText);
        Assert.Equal(1, host.ViewModel.Rows[0].Number);
        Assert.True(host.ViewModel.Rows[0].HasPreviewNumberChange);
        Assert.Equal("3", host.ViewModel.Rows[0].PreviewNumber);

        host.ViewModel.OrderShift = 3;
        await host.ViewModel.PreviewContentOptionsCommand.ExecuteAsync();
        Assert.Equal("4", host.ViewModel.Rows[0].PreviewNumber);
        Assert.True(host.ViewModel.Rows[0].HasPreviewNumberChange);
        Assert.True(host.ViewModel.CanApplyContentPreview);

        await host.ViewModel.CancelContentPreviewCommand.ExecuteAsync();
        host.ViewModel.SelectedFrameRateIndex = 1;
        await host.ViewModel.ChangeFpsCommand.ExecuteAsync();
        await host.LayoutAsync(width: 760, height: 600);

        Assert.True(host.RequiredControl<Button>("ApplyContentPreviewButton").IsVisible);
        Assert.Contains(host.Localizer.GetString("Expression.Property.FrameRate"), host.ViewModel.ContentPreviewMetadataTooltip, StringComparison.Ordinal);
        Assert.Empty(host.ViewModel.ExpressionPreviewText);
    }

    [AvaloniaFact]
    public async Task Frame_rate_property_only_preview_keeps_missing_row_values_and_exposes_metadata_on_apply()
    {
        var info = new ChapterSet(
            "movie.txt",
            "movie.txt",
            ChapterImportFormat.Ogm,
            24,
            TimeSpan.Zero,
            [new Chapter(1, TimeSpan.Zero, "Opening")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            new ChapterImportEntry("movie.txt", "movie.txt", info)));
        await host.LoadAsync("movie.txt");
        var row = Assert.Single(host.ViewModel.Rows);
        var original = (row.TimeText, row.Name, row.Number, row.FramesInfo);
        host.ViewModel.SelectedFrameRateIndex = 1;
        await host.ViewModel.ChangeFpsCommand.ExecuteAsync();
        await host.LayoutAsync(760, 600);

        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.True(host.ViewModel.IsInlineCandidateVisible);
        Assert.Equal(original, (row.TimeText, row.Name, row.Number, row.FramesInfo));
        Assert.False(row.HasPreviewTimeChange);
        Assert.False(row.HasPreviewNameChange);
        Assert.False(row.HasPreviewNumberChange);
        Assert.False(row.HasPreviewFramesChange);
        var applyButton = host.RequiredControl<Button>("ApplyContentPreviewButton");
        Assert.NotEmpty(host.ViewModel.ContentPreviewMetadataTooltip);
        Assert.Equal(host.ViewModel.ContentPreviewMetadataTooltip, applyButton.GetValue(ToolTip.TipProperty));
        Assert.DoesNotContain("Opening", host.ViewModel.ContentPreviewMetadataTooltip, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Inline_projection_keeps_submillisecond_before_and_candidate_values_distinct()
    {
        var start = TimeSpan.FromTicks(1_000);
        var info = new ChapterSet(
            "movie.txt",
            "movie.txt",
            ChapterImportFormat.Ogm,
            24,
            start,
            [new Chapter(1, start, "Submillisecond")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            new ChapterImportEntry("movie.txt", "movie.txt", info)));
        await host.LoadAsync("movie.txt");
        host.ViewModel.Expression = "t + 0.0001";
        host.ViewModel.RefreshExpressionPreviewNow();
        await host.LayoutAsync(1280, 800);

        var row = Assert.Single(host.ViewModel.Rows);
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.False(row.IsNarrowPreviewLayout);
        Assert.True(row.HasPreviewTimeChange);
        Assert.NotEqual(row.PreviewBeforeTimeText, row.PreviewTimeText);
        Assert.NotEqual("00:00:00.000", row.PreviewTimeText);
        Assert.NotEqual("+00:00:00.000", row.PreviewTimeDelta);

        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var timeCell = Assert.Single(grid.GetVisualDescendants().OfType<DataGridCell>(), cell =>
            ReferenceEquals(cell.DataContext, row)
            && cell.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == row.PreviewBeforeTimeText));
        MainWindowHeadlessTestHost.CaptureRenderedFrame(host.Window,
            "artifacts/compact-content-preview-review/preview-submillisecond.png");
        foreach (var text in new[] { row.PreviewBeforeTimeText, row.PreviewTimeText, row.PreviewTimeDelta })
        {
            var textBlocks = timeCell.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsVisible
                    && block.GetVisualAncestors().Any(ancestor => ancestor is StackPanel)
                    && string.Equals(block.Text, text, StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(textBlocks);
            foreach (var textBlock in textBlocks)
            {
                var position = textBlock.TranslatePoint(new Point(0, 0), timeCell);
                Assert.NotNull(position);
                Assert.True(position.Value.X >= 0, $"{text}: x={position.Value.X}, cell={timeCell.Bounds}");
                Assert.True(position.Value.Y >= 0, $"{text}: y={position.Value.Y}, cell={timeCell.Bounds}");
                Assert.True(position.Value.X + textBlock.Bounds.Width <= timeCell.Bounds.Width,
                    $"{text}: right={position.Value.X + textBlock.Bounds.Width}, cell={timeCell.Bounds}");
                Assert.True(position.Value.Y + textBlock.Bounds.Height <= timeCell.Bounds.Height,
                    $"{text}: pos={position.Value}, bounds={textBlock.Bounds}, bottom={position.Value.Y + textBlock.Bounds.Height}, cell={timeCell.Bounds}");
            }
        }
    }

    [AvaloniaFact]
    public async Task Missing_source_frame_information_preserves_the_visible_calculated_zero_frame()
    {
        var info = new ChapterSet(
            "movie.txt",
            "movie.txt",
            ChapterImportFormat.Ogm,
            0,
            TimeSpan.Zero,
            [new Chapter(1, TimeSpan.Zero, "Opening")]);
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            new ChapterImportEntry("movie.txt", "movie.txt", info)));
        await host.LoadAsync("movie.txt");
        host.ViewModel.Expression = "t";
        host.ViewModel.RefreshExpressionPreviewNow();
        await host.LayoutAsync(1280, 800);

        var row = Assert.Single(host.ViewModel.Rows);
        Assert.True(host.ViewModel.CanApplyContentPreview);
        Assert.True(row.HasPreviewFramesChange);
        Assert.True(row.PreviewComparison?.BeforeFrames?.IsMissing);
        Assert.Equal("0", row.FramesInfo);
        Assert.Equal("0", row.PreviewBeforeFrames);
        Assert.Equal("0", row.PreviewFrames);
        Assert.True(host.ContainsRenderedText("0"));

        var comparison = row.PreviewComparison;
        row.FramesInfo = string.Empty;
        row.UpdatePreviewComparison(null, host.ViewModel.DisplayFrameRate, host.Localizer.GetString("Expression.Value.NotCalculated"));
        row.UpdatePreviewComparison(comparison, host.ViewModel.DisplayFrameRate, host.Localizer.GetString("Expression.Value.NotCalculated"));
        row.PreviewBeforeFrames = row.PreviewFrameBaseline;
        Assert.Equal(host.Localizer.GetString("Expression.Value.NotCalculated"), row.PreviewBeforeFrames);
    }

    [AvaloniaFact]
    public async Task Inline_preview_default_wide_and_narrow_screenshots_are_captured()
    {
        var sizes = new (string Name, double Width, double Height)[]
        {
            ("default", 760, 600),
            ("wide", 1280, 800),
            ("narrow", 760, 520)
        };
        foreach (var (name, width, height) in sizes)
        {
            using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                "movie.txt",
                MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", Enumerable.Range(1, 20)
                    .Select(index => $"Chapter {index:D2} · A longer preview title").ToArray())));
            await host.LoadAsync("movie.txt");
            await host.LayoutAsync(width, height);
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                $"artifacts/compact-content-preview-review/normal-{name}.png");

            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            await host.LayoutAsync(width, height);
            AssertBottomInputAlignment(host);
            Assert.True(host.ViewModel.CanApplyContentPreview);
            Assert.True(host.ContainsRenderedText("00:00:01.000"));
            if (width <= 860)
            {
                var renderedRows = host.RequiredControl<DataGrid>("ChapterGrid")
                    .GetVisualDescendants()
                    .OfType<DataGridRow>()
                    .Where(static row => row.IsVisible && row.Bounds.Height > 0)
                    .ToArray();
                Assert.NotEmpty(renderedRows);
                Assert.All(renderedRows, row => Assert.InRange(row.Bounds.Height, 0, 56));
            }
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                $"artifacts/compact-content-preview-review/preview-{name}.png");

            await host.ViewModel.ApplyContentPreviewCommand.ExecuteAsync();
            await host.LayoutAsync(width, height);
            Assert.False(host.ViewModel.IsChapterGridReadOnly);
            Assert.False(host.ViewModel.IsInlineCandidateVisible);
            Assert.Equal(host.Localizer.GetString("Grid.Time"), host.RequiredControl<DataGrid>("ChapterGrid").Columns[1].Header);
            Assert.Equal("00:00:01.000", host.ViewModel.Rows[0].TimeText);
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                $"artifacts/compact-content-preview-review/applied-{name}.png");
        }

        foreach (var (name, width, height) in sizes)
        {
            using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                "movie.txt",
                new ChapterImportEntry("movie.txt", "movie.txt", new ChapterSet(
                    "movie.txt",
                    "movie.txt",
                    ChapterImportFormat.Ogm,
                    24000d / 1001d,
                    TimeSpan.Zero,
                    Enumerable.Range(0, 20)
                        .Select(index => new Chapter(
                            index + 1,
                            index switch
                            {
                                0 => TimeSpan.Zero,
                                1 => TimeSpan.FromMilliseconds(348557),
                                2 => TimeSpan.FromMilliseconds(599600),
                                _ => TimeSpan.FromSeconds(600 + 60 * (index - 3))
                            },
                            $"第 {index + 1:D2} 章 · 较长的预览标题"))
                        .ToArray()))));
            await host.LoadAsync("movie.txt");
            host.Localizer.SetCulture("zh-CN");
            host.ViewModel.FrameAccuracyTolerance = 0.01m;
            host.ViewModel.SetFrameOptions(frameRateIndex: 1, roundFrames: true);
            host.ViewModel.RefreshRowsFromPort();
            host.ViewModel.Expression = "t + 1";
            host.ViewModel.RefreshExpressionPreviewNow();
            await host.LayoutAsync(width, height);
            AssertBottomInputAlignment(host);
            var grid = host.RequiredControl<DataGrid>("ChapterGrid");
            Assert.Equal(host.Localizer.GetString("Grid.Time"), grid.Columns[1].Header);
            Assert.False(host.ContainsRenderedText("原"));
            Assert.False(host.ContainsRenderedText("预览"));
            Assert.Null(host.Window.FindControl<Control>("ContentPreviewSummary"));
            Assert.Null(host.Window.FindControl<Control>("ContentPreviewDetailsButton"));
            Assert.Equal("00:05:48.557", host.ViewModel.Rows[1].PreviewBeforeTimeText);
            Assert.Equal("00:05:49.557", host.ViewModel.Rows[1].PreviewTimeText);
            Assert.Equal("8357", host.ViewModel.Rows[1].PreviewBeforeFrames);
            Assert.Equal("8381", host.ViewModel.Rows[1].PreviewFrames);
            var renderedRows = host.RequiredControl<DataGrid>("ChapterGrid")
                .GetVisualDescendants()
                .OfType<DataGridRow>()
                .Where(static row => row.IsVisible && row.Bounds.Height > 0)
                .ToArray();
            Assert.NotEmpty(renderedRows);
            Assert.All(renderedRows, row => Assert.InRange(row.Bounds.Height, 0, 56));
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                $"artifacts/compact-content-preview-review/preview-zh-{name}.png");
        }

        using (var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                   "movie.txt",
                   MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", Enumerable.Range(1, 20)
                       .Select(index => $"Chapter {index:D2} · Long title").ToArray()))))
        {
            await host.LoadAsync("movie.txt");
            host.Localizer.SetCulture("ja-JP");
            foreach (var (_, width, height) in sizes)
            {
                await host.LayoutAsync(width, height);
                AssertBottomInputAlignment(host);
            }
        }

        using (var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                   "movie.txt",
                   MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Opening", "Middle", "Ending"))))
        {
            var themeService = new ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService();
            var application = global::Avalonia.Application.Current!;
            var themeResourceKeys = ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.ImportedThemeColorKeys
                .Concat(new[]
                {
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameNeutralBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameAccurateBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.FrameInexactBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.DiagnosticErrorBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.LogInformationBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.LogWarningBrushKey,
                    ChapterTool.Avalonia.Services.AvaloniaThemeApplicationService.LogErrorBrushKey,
                    "ChapterTool.FontSize.Small",
                    "ChapterTool.FontSize.Default",
                    "ChapterTool.FontSize.Large"
                })
                .ToArray();
            var originalResources = themeResourceKeys.ToDictionary(key => key, key => application.Resources[key]);
            var originalThemeVariant = application.RequestedThemeVariant;
            try
            {
                await host.LoadAsync("movie.txt");
                host.ViewModel.Expression = "t + 1";
                host.ViewModel.RefreshExpressionPreviewNow();
                themeService.Apply(new ChapterTool.Contracts.Configuration.ThemeSettings("ayu-dark"));
                await host.LayoutAsync(760, 600);
                AssertBottomInputAlignment(host);
                Assert.Equal("Dark", global::Avalonia.Application.Current!.RequestedThemeVariant?.ToString());
                Assert.True(host.ViewModel.CanApplyContentPreview);
                Assert.True(host.ContainsRenderedText("00:00:01.000"));
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    host.Window,
                    "artifacts/compact-content-preview-review/preview-dark.png");

                themeService.Apply(ChapterTool.Contracts.Configuration.ThemeSettings.Default);
                global::Avalonia.Application.Current!.Resources["ChapterTool.FontSize.Small"] = 16d;
                global::Avalonia.Application.Current.Resources["ChapterTool.FontSize.Default"] = 18d;
                global::Avalonia.Application.Current.Resources["ChapterTool.FontSize.Large"] = 20d;
                await host.LayoutAsync(760, 600);
                AssertBottomInputAlignment(host);
                Assert.True(host.ViewModel.CanApplyContentPreview);
                var enlargedCandidate = host.RequiredControl<DataGrid>("ChapterGrid")
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .FirstOrDefault(block => block.Text == "00:00:01.000" && block.FontSize >= 18);
                Assert.NotNull(enlargedCandidate);
                var statusBar = host.RequiredControl<Border>("StatusBar");
                var logButton = host.RequiredControl<Button>("LogButton");
                var logButtonOrigin = logButton.TranslatePoint(new Point(0, 0), statusBar);
                Assert.NotNull(logButtonOrigin);
                Assert.True(logButtonOrigin.Value.X >= 0);
                Assert.True(logButtonOrigin.Value.Y >= 0);
                Assert.True(logButtonOrigin.Value.X + logButton.Bounds.Width <= statusBar.Bounds.Width);
                Assert.True(logButtonOrigin.Value.Y + logButton.Bounds.Height <= statusBar.Bounds.Height);
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    host.Window,
                    "artifacts/compact-content-preview-review/preview-large-font.png");
            }
            finally
            {
                foreach (var (key, value) in originalResources)
                {
                    application.Resources[key] = value;
                }

                application.RequestedThemeVariant = originalThemeVariant;
            }
        }
    }

    private static void AssertFramePreviewColor(
        MainWindowHeadlessTestHost host,
        ChapterTool.Avalonia.UI.ViewModels.ChapterRowViewModel row,
        string text,
        string brushKey)
    {
        var renderedRow = host.RequiredControl<DataGrid>("ChapterGrid")
            .GetVisualDescendants()
            .OfType<DataGridRow>()
            .Single(item => ReferenceEquals(item.DataContext, row));
        var frameText = Assert.Single(renderedRow.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsVisible && block.Bounds.Width > 0 && block.Bounds.Height > 0
                && block.Text == text && block.Classes.Contains("framePreview")
                && (row.IsNarrowPreviewLayout ? block.Parent is Grid : block.Parent is StackPanel));
        var expectedBrush = Assert.IsType<SolidColorBrush>(global::Avalonia.Application.Current!.Resources[brushKey]);
        var actualBrush = Assert.IsType<SolidColorBrush>(frameText.Foreground);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static void AssertNormalFrameColor(
        MainWindowHeadlessTestHost host,
        ChapterTool.Avalonia.UI.ViewModels.ChapterRowViewModel row,
        string text,
        string brushKey)
    {
        var renderedRow = host.RequiredControl<DataGrid>("ChapterGrid")
            .GetVisualDescendants()
            .OfType<DataGridRow>()
            .Single(item => ReferenceEquals(item.DataContext, row));
        var frameText = Assert.Single(renderedRow.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsVisible && block.Bounds.Width > 0 && block.Bounds.Height > 0 && block.Text == text
                && block.Classes.Contains("frameText") && !block.Classes.Contains("framePreview"));
        var expectedBrush = Assert.IsType<SolidColorBrush>(global::Avalonia.Application.Current!.Resources[brushKey]);
        var actualBrush = Assert.IsType<SolidColorBrush>(frameText.Foreground);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static void AssertBottomInputAlignment(MainWindowHeadlessTestHost host)
    {
        var options = host.RequiredControl<Grid>("AdvancedOptionsGrid");
        var expressionGroup = host.RequiredControl<Grid>("ExpressionOptionsGroup");
        if (Grid.GetColumn(expressionGroup) == 1)
        {
            var chapterNameGroup = host.RequiredControl<Grid>("ChapterNameOptionsGroup");
            var orderShiftGroup = host.RequiredControl<Grid>("OrderShiftOptionsGroup");
            var chapterName = host.RequiredControl<Control>("ChapterNameModeBox");
            var orderShift = host.RequiredControl<Control>("OrderShiftBox");
            var expression = host.RequiredControl<Control>("ExpressionBox");
            var nameInput = chapterName.TranslatePoint(new Point(0, 0), options)!.Value.X;
            var orderInput = orderShift.TranslatePoint(new Point(0, 0), options)!.Value.X;
            var expressionInput = expression.TranslatePoint(new Point(0, 0), options)!.Value.X;
            var nameGroup = chapterNameGroup.TranslatePoint(new Point(0, 0), options)!.Value.X;
            var orderGroup = orderShiftGroup.TranslatePoint(new Point(0, 0), options)!.Value.X;
            Assert.InRange(Math.Abs((nameInput - nameGroup) - (orderInput - orderGroup)), 0, 1);
            Assert.InRange(Math.Abs(nameInput - expressionInput), 0, 1);
            return;
        }

        var controls = new[]
        {
            host.RequiredControl<Control>("FormatBox"),
            host.RequiredControl<Control>("XmlLanguageBox"),
            host.RequiredControl<Control>("ExpressionBox")
        };
        var positions = controls.Select(control => control.TranslatePoint(new Point(0, 0), options)).ToArray();
        Assert.All(positions, position => Assert.NotNull(position));
        var left = positions.Select(position => position!.Value.X).ToArray();
        Assert.InRange(left.Max() - left.Min(), 0, 1);
    }

    [AvaloniaFact]
    public void Default_width_matches_minimum_width()
    {
        using var host = new MainWindowHeadlessTestHost();

        Assert.Equal(host.Window.MinWidth, host.Window.Width);
    }

    [AvaloniaFact]
    public async Task Xml_edition_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "movie.xml",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.MatroskaXml, "edition-1", "XML A1", "XML A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.MatroskaXml, "edition-2", "XML B1", "XML B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "movie.xml", selectedIndex: 1, "XML B1", "XML B2");
    }

    [AvaloniaFact]
    public async Task Ifo_option_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "VIDEO_TS.IFO",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.DvdIfo, "pgc-1", "IFO A1", "IFO A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.DvdIfo, "pgc-2", "IFO B1", "IFO B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "VIDEO_TS.IFO", selectedIndex: 1, "IFO B1", "IFO B2");
    }

    [AvaloniaFact]
    public async Task Mpls_clip_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "MPLS B1", "MPLS B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "00000.mpls", selectedIndex: 1, "MPLS B1", "MPLS B2");
    }

    [AvaloniaFact]
    public async Task Clip_selector_keeps_selected_text_after_selection_refreshes_option()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "MPLS B1", "MPLS B2"));

        await host.LoadAsync("00000.mpls");
        var clipSelector = host.RequiredControl<ComboBox>("ClipBox");

        clipSelector.SelectedIndex = 1;
        await host.LayoutAsync();

        Assert.Equal(1, host.ViewModel.SelectedClipIndex);
        Assert.Equal("00002（2 chapters）", clipSelector.SelectionBoxItem?.ToString());
        Assert.True(
            MainWindowHeadlessTestHost.ContainsRenderedText(clipSelector, "00002（2 chapters）"),
            $"Expected selected clip label to remain visible. Rendered selector texts:{Environment.NewLine}{MainWindowHeadlessTestHost.DescribeRenderedTexts(clipSelector)}");
    }

    [AvaloniaTheory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public async Task Advanced_options_share_localized_label_columns_across_responsive_layouts(string culture)
    {
        using var host = new MainWindowHeadlessTestHost();
        host.Localizer.SetCulture(culture);

        await host.LayoutAsync(width: 736, height: 576);
        AssertBottomInputAlignment(host);

        var options = host.RequiredControl<Grid>("AdvancedOptionsGrid");
        var expressionGroup = host.RequiredControl<Grid>("ExpressionOptionsGroup");
        var expressionEditor = host.RequiredControl<Control>("ExpressionBox");
        var loadExpressionButton = host.RequiredControl<Button>("LoadExpressionButton");
        var chapterNameMode = host.RequiredControl<ComboBox>("ChapterNameModeBox");
        var formatBox = host.RequiredControl<ComboBox>("FormatBox");
        var xmlLanguageBox = host.RequiredControl<ComboBox>("XmlLanguageBox");
        var orderShiftBox = host.RequiredControl<NumericUpDown>("OrderShiftBox");

        Assert.Equal(2, options.ColumnDefinitions.Count);
        Assert.Equal(2, Grid.GetColumnSpan(expressionGroup));
        Assert.True(expressionEditor.Bounds.Width >= 400);
        Assert.True(expressionEditor.Bounds.Right <= loadExpressionButton.Bounds.Left);
        Assert.Null(host.Window.FindControl<CheckBox>("ApplyExpressionBox"));
        Assert.True(chapterNameMode.Bounds.Width >= 128);
        Assert.True(formatBox.Bounds.Right <= options.Bounds.Right);
        Assert.True(xmlLanguageBox.Bounds.Right <= options.Bounds.Right);
        Assert.True(expressionEditor.Bounds.Right <= options.Bounds.Right);
        Assert.True(orderShiftBox.Bounds.Right <= options.Bounds.Right);

        await host.LayoutAsync(width: 1100, height: 576);
        AssertBottomInputAlignment(host);

        Assert.Equal(3, options.ColumnDefinitions.Count);
        Assert.Equal(2, Grid.GetColumnSpan(expressionGroup));
        Assert.True(expressionEditor.Bounds.Width >= 500);
        Assert.True(expressionEditor.Bounds.Right <= loadExpressionButton.Bounds.Left);
        Assert.True(chapterNameMode.Bounds.Right <= options.Bounds.Right);
        Assert.True(orderShiftBox.Bounds.Right <= options.Bounds.Right);
    }

    [AvaloniaFact]
    public async Task Clip_combine_context_menu_shows_checked_toggle_state()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "MPLS B1", "MPLS B2"));

        await host.LoadAsync("00000.mpls");
        var menuItem = host.RequiredControl<MenuItem>("GridCombineMenuItem");

        Assert.False(menuItem.IsChecked);

        await host.ViewModel.CombineCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.True(host.ViewModel.IsClipCombineChecked);
        Assert.True(menuItem.IsChecked);

        await host.ViewModel.CombineCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.False(host.ViewModel.IsClipCombineChecked);
        Assert.False(menuItem.IsChecked);
    }

    [AvaloniaFact]
    public async Task Clip_combine_binding_updates_when_view_model_command_runs()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "MPLS B1", "MPLS B2"));

        await host.LoadAsync("00000.mpls");
        var gridMenuItem = host.RequiredControl<MenuItem>("GridCombineMenuItem");

        await host.ViewModel.CombineCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.True(gridMenuItem.IsChecked);
    }

    [AvaloniaFact]
    public async Task Xml_importer_option_labels_render_in_clip_selector()
    {
        var importer = new XmlChapterImporter(new ChapterTimeFormatter());
        var result = importer.ImportText(
            """
            <Chapters>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:00.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>First Edition</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
              <EditionEntry>
                <ChapterAtom>
                  <ChapterTimeStart>00:00:10.000000000</ChapterTimeStart>
                  <ChapterDisplay><ChapterString>Second Edition</ChapterString></ChapterDisplay>
                </ChapterAtom>
              </EditionEntry>
            </Chapters>
            """,
            "real.xml");

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, "real.xml", "Edition 01（1 chapter）");
        await AssertSelectorDisplaysLabelAsync(host, "real.xml", selectedIndex: 1, "Edition 02（1 chapter）");
    }

    [AvaloniaFact]
    public async Task Ifo_importer_labels_follow_ptt_title_and_chapter_data()
    {
        var importer = new IfoChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VTS_33_0.IFO");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "VTS_33_1 (0:23:31) [VTS_33_1]（47 chapters）");
    }

    [AvaloniaFact]
    public async Task Mpls_importer_option_labels_render_in_clip_selector()
    {
        var importer = new MplsChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_Hidan_no_Aria_AA.mpls");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "00002.m2ts（6 chapters）");
        await AssertSelectorDisplaysLabelAsync(host, path, selectedIndex: 1, "00003.m2ts（6 chapters）");
    }

    private static MainWindowHeadlessTestHost CreateMultiOptionHost(
        string path,
        params ChapterImportEntry[] entries) =>
        new(MainWindowHeadlessTestHost.ImportResult(path, entries));

    private static async Task AssertSelectingOptionDisplaysNamesAsync(
        MainWindowHeadlessTestHost host,
        string path,
        int selectedIndex,
        params string[] expectedNames)
    {
        await host.LoadAsync(path);
        var clipSelector = host.RequiredControl<ComboBox>("ClipBox");

        Assert.True(clipSelector.IsVisible);
        Assert.NotNull(clipSelector.ItemTemplate);
        clipSelector.SelectedIndex = selectedIndex;
        await host.LayoutAsync();

        Assert.Equal(selectedIndex, host.ViewModel.SelectedClipIndex);
        Assert.Equal(expectedNames, host.ViewModel.Rows.Select(static row => row.Name));
        foreach (var name in expectedNames)
        {
            Assert.True(host.ContainsRenderedText(name), $"Expected rendered chapter grid text '{name}'.");
        }
    }

    private static async Task AssertSelectorDisplaysLabelAsync(
        MainWindowHeadlessTestHost host,
        string path,
        int selectedIndex,
        string expectedLabel)
    {
        await host.LoadAsync(path);
        var clipSelector = host.RequiredControl<ComboBox>("ClipBox");

        Assert.True(clipSelector.IsVisible);
        clipSelector.SelectedIndex = selectedIndex;
        clipSelector.IsDropDownOpen = true;
        await host.LayoutAsync();

        Assert.Equal(selectedIndex, host.ViewModel.SelectedClipIndex);
        Assert.True(
            host.ContainsRenderedText(expectedLabel),
            $"Expected selector to render '{expectedLabel}'. Rendered window texts:{Environment.NewLine}{MainWindowHeadlessTestHost.DescribeRenderedTexts(host.Window)}");

        clipSelector.IsDropDownOpen = false;
        await host.LayoutAsync();
    }

    private static async Task AssertDefaultSelectionDisplaysLabelAsync(
        MainWindowHeadlessTestHost host,
        string path,
        string expectedLabel)
    {
        await host.LoadAsync(path);
        var clipSelector = host.RequiredControl<ComboBox>("ClipBox");

        Assert.True(clipSelector.IsVisible);
        Assert.False(clipSelector.IsDropDownOpen);
        Assert.Equal(0, host.ViewModel.SelectedClipIndex);
        Assert.Equal(expectedLabel, clipSelector.SelectionBoxItem?.ToString());
        Assert.True(
            MainWindowHeadlessTestHost.ContainsRenderedText(clipSelector, expectedLabel),
            $"Expected default selected label '{expectedLabel}'. Rendered selector texts:{Environment.NewLine}{MainWindowHeadlessTestHost.DescribeRenderedTexts(clipSelector)}");
    }
}
