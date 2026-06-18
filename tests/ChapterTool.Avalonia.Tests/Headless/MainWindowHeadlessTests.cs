using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Importing.Text;
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

        await AssertDefaultSelectionDisplaysLabelAsync(host, "real.xml", "Edition 01");
        await AssertSelectorDisplaysLabelAsync(host, "real.xml", selectedIndex: 1, "Edition 02");
    }

    [AvaloniaFact]
    public async Task Ifo_importer_option_labels_render_in_clip_selector()
    {
        var importer = new IfoChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Ifo", "VTS_33_0.IFO");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "VTS_33_1__47");
        await AssertSelectorDisplaysLabelAsync(host, path, selectedIndex: 1, "VTS_33_2__47");
    }

    [AvaloniaFact]
    public async Task Mpls_importer_option_labels_render_in_clip_selector()
    {
        var importer = new MplsChapterImporter();
        var path = Path.Combine(MainWindowHeadlessTestHost.RepositoryRoot(), "tests", "ChapterTool.Core.Tests", "Fixtures", "Importing", "Disc", "Mpls", "00001_Hidan_no_Aria_AA.mpls");
        var result = await importer.ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        using var host = new MainWindowHeadlessTestHost(result);

        await AssertDefaultSelectionDisplaysLabelAsync(host, path, "00002__6");
        await AssertSelectorDisplaysLabelAsync(host, path, selectedIndex: 1, "00003__6");
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
}
