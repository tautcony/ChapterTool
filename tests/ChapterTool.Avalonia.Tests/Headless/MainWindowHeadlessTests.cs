using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Text;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests.Headless;

public sealed class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public async Task MainWindow_loads_compiled_xaml_resources_and_layout()
    {
        using var host = new MainWindowHeadlessTestHost();

        await host.LayoutAsync();

        Assert.Equal("[VCB-Studio] ChapterTool", host.Window.Title);
        Assert.NotNull(host.RequiredControl<DataGrid>("ChapterGrid"));
        Assert.NotNull(host.RequiredControl<Button>("LoadButton"));
        Assert.True(host.Window.Bounds.Width > 0);
        Assert.True(host.Window.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public async Task Xml_edition_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "movie.xml",
            MainWindowHeadlessTestHost.Option("XML", "edition-1", "XML A1", "XML A2"),
            MainWindowHeadlessTestHost.Option("XML", "edition-2", "XML B1", "XML B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "movie.xml", selectedIndex: 1, "XML B1", "XML B2");
    }

    [AvaloniaFact]
    public async Task Ifo_option_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "VIDEO_TS.IFO",
            MainWindowHeadlessTestHost.Option("DVD", "pgc-1", "IFO A1", "IFO A2"),
            MainWindowHeadlessTestHost.Option("DVD", "pgc-2", "IFO B1", "IFO B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "VIDEO_TS.IFO", selectedIndex: 1, "IFO B1", "IFO B2");
    }

    [AvaloniaFact]
    public async Task Mpls_clip_selection_displays_selected_chapter_names()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Option("MPLS", "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Option("MPLS", "00002", "MPLS B1", "MPLS B2"));

        await AssertSelectingOptionDisplaysNamesAsync(host, "00000.mpls", selectedIndex: 1, "MPLS B1", "MPLS B2");
    }

    [AvaloniaFact]
    public async Task Clip_selector_keeps_selected_text_after_selection_refreshes_option()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Option("MPLS", "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Option("MPLS", "00002", "MPLS B1", "MPLS B2"));

        await host.LoadAsync("00000.mpls");
        var clipSelector = host.RequiredControl<ComboBox>("ClipBox");

        clipSelector.SelectedIndex = 1;
        await host.LayoutAsync();

        Assert.Equal(1, host.ViewModel.SelectedClipIndex);
        Assert.Equal("00002（2 chapters）", clipSelector.SelectionBoxItem?.ToString());
        Assert.True(
            host.ContainsRenderedText(clipSelector, "00002（2 chapters）"),
            $"Expected selected clip label to remain visible. Rendered selector texts:{Environment.NewLine}{host.DescribeRenderedTexts(clipSelector)}");
    }

    [AvaloniaFact]
    public async Task Clip_combine_context_menu_shows_checked_toggle_state()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Option("MPLS", "00001", "MPLS A1", "MPLS A2"),
            MainWindowHeadlessTestHost.Option("MPLS", "00002", "MPLS B1", "MPLS B2"));

        await host.LoadAsync("00000.mpls");
        var menuItem = host.RequiredControl<MenuItem>("ClipCombineMenuItem");

        Assert.False(menuItem.IsChecked);

        await host.Window.CombineCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.True(host.ViewModel.IsClipCombineChecked);
        Assert.True(menuItem.IsChecked);

        await host.Window.CombineCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.False(host.ViewModel.IsClipCombineChecked);
        Assert.False(menuItem.IsChecked);
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

        await AssertDefaultSelectionDisplaysLabelAsync(host, "real.xml", "Edition 01（1 chapters）");
        await AssertSelectorDisplaysLabelAsync(host, "real.xml", selectedIndex: 1, "Edition 02（1 chapters）");
    }

    [AvaloniaFact]
    public async Task Ifo_importer_option_labels_render_in_clip_selector()
    {
        var importer = new IfoChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VTS_33_0.IFO");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "VTS_33_1（47 chapters）");
        await AssertSelectorDisplaysLabelAsync(host, path, selectedIndex: 1, "VTS_33_2（47 chapters）");
    }

    [AvaloniaFact]
    public async Task Mpls_importer_option_labels_render_in_clip_selector()
    {
        var importer = new MplsChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_Hidan_no_Aria_AA.mpls");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "00002（6 chapters）");
        await AssertSelectorDisplaysLabelAsync(host, path, selectedIndex: 1, "00003（6 chapters）");
    }

    [AvaloniaFact]
    public async Task Main_window_selector_display_captures_screenshot_artifacts()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Option("MPLS", "00002", "A1", "A2", "A3", "A4", "A5", "A6"),
            MainWindowHeadlessTestHost.Option("MPLS", "00003", "B1", "B2", "B3", "B4", "B5", "B6"));

        await host.LoadAsync("00000.mpls");
        host.ViewModel.SaveFormat = ChapterExportFormat.Xml;
        host.ViewModel.XmlLanguageIndex = host.ViewModel.XmlLanguageOptions.ToList().IndexOf("jpn");

        foreach (var (name, width, height) in new[]
        {
            ("default", 736d, 576d),
            ("wide", 944d, 576d),
            ("narrow", 608d, 576d)
        })
        {
            await host.LayoutAsync(width, height);
            var artifactPath = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "artifacts", $"main-window-selectors-{name}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            var bitmap = host.Window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"Main window selector frame '{name}' was not rendered.");
            await using (var stream = File.Create(artifactPath))
            {
                bitmap.Save(stream);
            }

            Assert.True(File.Exists(artifactPath));
            Assert.True(new FileInfo(artifactPath).Length > 0);
        }
    }

    [AvaloniaFact]
    public async Task Main_window_grid_keeps_compact_column_alignment()
    {
        using var host = CreateMultiOptionHost(
            "00000.mpls",
            MainWindowHeadlessTestHost.Option(
                "MPLS",
                "00002",
                "A1",
                "A2",
                "A3",
                "A4",
                "A5",
                "A6",
                "A7",
                "A8",
                "A9",
                "A10"));

        await host.LoadAsync("00000.mpls");
        await host.LayoutAsync(608, 576);
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");

        Assert.True(grid.Columns[0].ActualWidth >= 56);
        Assert.True(grid.Columns[0].ActualWidth < 70);
        AssertCellTextAlignment(grid, "2", TextAlignment.Center);
        AssertCellTextAlignment(grid, "00:00:10.000", TextAlignment.Center);
        AssertCellTextAlignment(grid, "A2", TextAlignment.Center);
        AssertCellTextAlignment(grid, "240", TextAlignment.Center);
    }

    private static MainWindowHeadlessTestHost CreateMultiOptionHost(
        string path,
        params Core.Models.ChapterSourceOption[] options) =>
        new(MainWindowHeadlessTestHost.ImportResult(path, options));

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
            $"Expected selector to render '{expectedLabel}'. Rendered window texts:{Environment.NewLine}{host.DescribeRenderedTexts(host.Window)}");
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
            host.ContainsRenderedText(clipSelector, expectedLabel),
            $"Expected default selected label '{expectedLabel}'. Rendered selector texts:{Environment.NewLine}{host.DescribeRenderedTexts(clipSelector)}");
    }

    private static void AssertCellTextAlignment(Control scope, string text, TextAlignment expected)
    {
        var textBlock = scope
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(block => string.Equals(block.Text, text, StringComparison.Ordinal));

        Assert.NotNull(textBlock);
        Assert.Equal(expected, textBlock.TextAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, textBlock.HorizontalAlignment);
    }
}
