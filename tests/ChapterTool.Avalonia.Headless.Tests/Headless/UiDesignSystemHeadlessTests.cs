using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class UiDesignSystemHeadlessTests
{
    [AvaloniaFact]
    public async Task Frames_cell_uses_semantic_foreground_without_effects()
    {
        var chapters = new[]
        {
            new Chapter(1, TimeSpan.Zero, "Intro"),
            new Chapter(2, TimeSpan.FromMilliseconds(21), "Mid")
        };
        var info = new ChapterSet("movie.txt", "movie.txt", ChapterImportFormat.Ogm, 24, chapters[^1].StartTime, chapters);
        using var host = new MainWindowHeadlessTestHost(
            MainWindowHeadlessTestHost.ImportResult("movie.txt", new ChapterImportEntry("movie.txt", "movie.txt", info)));
        await host.LoadAsync("movie.txt");

        Assert.True(host.ViewModel.Rows[0].IsFrameAccurate);
        Assert.True(host.ViewModel.Rows[1].IsFrameInexact);

        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var frameTexts = grid.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.Classes.Contains("frameText"))
            .ToArray();
        Assert.Equal(2, frameTexts.Length);
        Assert.All(frameTexts, block => Assert.Null(block.Effect));

        var accurate = Assert.Single(frameTexts, block => block.Classes.Contains("frameAccurate"));
        var inexact = Assert.Single(frameTexts, block => block.Classes.Contains("frameInexact"));
        Assert.Equal(ResourceBrushColor(AvaloniaThemeApplicationService.FrameAccurateBrushKey), BrushColor(accurate.Foreground));
        Assert.Equal(ResourceBrushColor(AvaloniaThemeApplicationService.FrameInexactBrushKey), BrushColor(inexact.Foreground));
    }

    [AvaloniaFact]
    public async Task Load_split_button_and_change_fps_button_expose_visible_commands()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "A"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "B")));
        await host.LoadAsync("movie.mpls");

        var loadButton = host.RequiredControl<SplitButton>("LoadButton");
        var reload = MainWindowHeadlessTestHost.RequiredFlyoutMenuItem(loadButton, "ReloadMenuItem");
        var append = MainWindowHeadlessTestHost.RequiredFlyoutMenuItem(loadButton, "AppendLoadMenuItem");
        var changeFps = host.RequiredControl<Button>("ChangeFpsButton");

        Assert.NotNull(loadButton.Command);
        Assert.NotNull(reload.Command);
        Assert.Equal(KeyGesture.Parse("Ctrl+R"), reload.InputGesture);
        Assert.True(append.IsEnabled);
        Assert.True(changeFps.IsVisible);
        Assert.Equal(host.ViewModel.ChangeFpsCommand, changeFps.Command);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(changeFps)));
        var clipMenu = host.RequiredControl<ComboBox>("ClipBox").ContextMenu;
        Assert.NotNull(clipMenu);
        var combine = MainWindowHeadlessTestHost.RequiredMenuItem(host.RequiredControl<ComboBox>("ClipBox"), "ClipCombineMenuItem");
        Assert.True(combine.IsEnabled);
        Assert.False(combine.IsChecked);
        Assert.Null(host.RequiredControl<ComboBox>("FrameRateBox").ContextMenu);
    }

    [AvaloniaFact]
    public async Task Icon_only_main_window_controls_have_accessible_names()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        AssertNamed(host.RequiredControl<Button>("PreviewButton"));
        AssertNamed(host.RequiredControl<Button>("RefreshButton"));
        AssertNamed(host.RequiredControl<Button>("SettingsButton"));
        AssertNamed(host.RequiredControl<Button>("ChapterNameTemplateButton"));
        AssertNamed(host.RequiredControl<Button>("LoadExpressionButton"));
        AssertNamed(host.RequiredControl<Button>("ChangeFpsButton"));
        AssertNamed(host.RequiredControl<ComboBox>("FrameRateBox"));
        Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(host.RequiredControl<SplitButton>("LoadButton"))?.ToString()));
    }

    [AvaloniaFact]
    public async Task Main_window_inputs_share_one_height_and_icon_buttons_have_visible_chrome()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        var format = host.RequiredControl<ComboBox>("FormatBox");
        var chapterName = host.RequiredControl<ComboBox>("ChapterNameModeBox");
        var xmlLanguage = host.RequiredControl<ComboBox>("XmlLanguageBox");
        var frameRate = host.RequiredControl<ComboBox>("FrameRateBox");
        var orderShift = host.RequiredControl<NumericUpDown>("OrderShiftBox");
        var preview = host.RequiredControl<Button>("PreviewButton");
        var changeFps = host.RequiredControl<Button>("ChangeFpsButton");

        Assert.InRange(format.Bounds.Height, 31, 33);
        Assert.InRange(Math.Abs(format.Bounds.Height - chapterName.Bounds.Height), 0, 1);
        Assert.InRange(Math.Abs(format.Bounds.Height - xmlLanguage.Bounds.Height), 0, 1);
        Assert.InRange(Math.Abs(format.Bounds.Height - frameRate.Bounds.Height), 0, 1);
        Assert.InRange(Math.Abs(format.Bounds.Height - orderShift.Bounds.Height), 0, 1);
        Assert.True(orderShift.Bounds.Width + 0.5 >= orderShift.MinWidth);
        Assert.True(preview.BorderThickness.Left > 0);
        Assert.True(changeFps.BorderThickness.Left > 0);
        Assert.NotNull(preview.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PART_Border"));
        Assert.NotNull(changeFps.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PART_Border"));
        Assert.False(preview.Background is ISolidColorBrush { Color.A: 0 });

        preview.Background = new SolidColorBrush(Color.Parse("#D6E9F8"));
        await host.LayoutAsync();
        var previewBorder = preview.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_Border");
        Assert.Equal(1, previewBorder.BorderThickness.Left);
        Assert.False(previewBorder.BorderBrush is ISolidColorBrush { Color.A: 0 });
    }

    [AvaloniaFact]
    public async Task Chapter_grid_header_lines_use_shared_border_brush()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        var expected = ResourceBrushColor("Brush.Border1");
        var headers = grid.GetVisualDescendants().OfType<DataGridColumnHeader>().ToArray();
        Assert.NotEmpty(headers);
        Assert.All(headers, header => Assert.Equal(expected, BrushColor(header.SeparatorBrush)));

        var verticalSeparators = grid.GetVisualDescendants()
            .OfType<Rectangle>()
            .Where(rectangle => rectangle.Name == "VerticalSeparator")
            .ToArray();
        Assert.NotEmpty(verticalSeparators);
        Assert.All(verticalSeparators, separator => Assert.Equal(expected, BrushColor(separator.Fill)));

        var headerSeparator = grid.GetVisualDescendants()
            .OfType<Rectangle>()
            .Single(rectangle => rectangle.Name == "PART_ColumnHeadersAndRowsSeparator");
        Assert.Equal(expected, BrushColor(headerSeparator.Fill));
    }

    private static void AssertNamed(Control control)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
            $"Expected an accessible name on '{control.Name}'.");
    }

    private static Color ResourceBrushColor(string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, out var resource), $"Missing resource '{key}'.");
        return BrushColor(resource as IBrush);
    }

    private static Color BrushColor(IBrush? brush)
    {
        return brush switch
        {
            SolidColorBrush solid => solid.Color,
            ISolidColorBrush immutable => immutable.Color,
            _ => throw new InvalidOperationException($"Expected a solid color brush but found {brush?.GetType().FullName ?? "null"}.")
        };
    }
}
