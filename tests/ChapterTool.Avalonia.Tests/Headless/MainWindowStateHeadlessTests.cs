using ChapterTool.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowStateHeadlessTests
{
    [AvaloniaFact]
    public async Task Initial_state_renders_from_view_model_bindings()
    {
        using var host = new MainWindowHeadlessTestHost();

        await host.LayoutAsync();

        var loadButton = host.RequiredControl<Button>("LoadButton");
        var saveButton = host.RequiredControl<Button>("SaveButton");
        var clipBox = host.RequiredControl<ComboBox>("ClipBox");
        var chapterGrid = host.RequiredControl<DataGrid>("ChapterGrid");
        var formatBox = host.RequiredControl<ComboBox>("FormatBox");
        var frameRateBox = host.RequiredControl<ComboBox>("FrameRateBox");
        var status = host.RequiredControl<TextBlock>("StatusBlock");
        var progress = host.RequiredControl<ProgressBar>("ProgressBar");

        Assert.True(loadButton.IsVisible);
        Assert.True(saveButton.IsVisible);
        Assert.False(saveButton.IsEnabled);
        Assert.False(clipBox.IsVisible);
        Assert.Empty(chapterGrid.ItemsSource!.Cast<object>());
        Assert.Equal(ChapterExportFormats.IndexOf(ChapterExportFormat.Txt), formatBox.SelectedIndex);
        Assert.Equal(0, frameRateBox.SelectedIndex);
        Assert.Equal(host.ViewModel.StatusText, status.Text);
        Assert.Equal(0, progress.Value);
    }

    [AvaloniaFact]
    public async Task Load_action_updates_rendered_path_status_grid_and_options()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.mpls",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00001", "Opening", "Middle"),
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Mpls, "00002", "Alt")));

        await host.LoadAsync("movie.mpls");

        var saveButton = host.RequiredControl<Button>("SaveButton");
        var clipBox = host.RequiredControl<ComboBox>("ClipBox");
        var chapterGrid = host.RequiredControl<DataGrid>("ChapterGrid");
        var frameRateBox = host.RequiredControl<ComboBox>("FrameRateBox");
        var status = host.RequiredControl<TextBlock>("StatusBlock");
        var progress = host.RequiredControl<ProgressBar>("ProgressBar");

        Assert.Equal("movie.mpls", host.ViewModel.CurrentPath);
        Assert.True(saveButton.IsEnabled);
        Assert.True(clipBox.IsVisible);
        Assert.Equal(0, clipBox.SelectedIndex);
        Assert.Equal(2, chapterGrid.ItemsSource!.Cast<object>().Count());
        Assert.Equal(host.ViewModel.SelectedFrameRateIndex, frameRateBox.SelectedIndex);
        Assert.Equal(host.ViewModel.StatusText, status.Text);
        Assert.Equal(1, progress.Value);
        Assert.True(host.ContainsRenderedText("Opening"));
        Assert.True(host.ContainsRenderedText("Middle"));
    }

    [AvaloniaFact]
    public async Task Save_options_changed_through_rendered_controls_route_to_save_service()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Intro")));
        await host.LoadAsync("movie.txt");

        var formatBox = host.RequiredControl<ComboBox>("FormatBox");
        var xmlLanguageGroup = host.RequiredControl<Grid>("XmlLanguageOptionsGroup");
        var xmlLanguageBox = host.RequiredControl<ComboBox>("XmlLanguageBox");
        var chapterNameModeBox = host.RequiredControl<ComboBox>("ChapterNameModeBox");
        var orderShiftBox = host.RequiredControl<NumericUpDown>("OrderShiftBox");
        var expressionCheckBox = host.RequiredControl<CheckBox>("ApplyExpressionBox");
        var expressionBox = host.RequiredControl<ExpressionEditor>("ExpressionBox");
        var frameRateBox = host.RequiredControl<ComboBox>("FrameRateBox");
        var roundFramesBox = host.RequiredControl<CheckBox>("RoundFramesBox");

        formatBox.SelectedIndex = ChapterExportFormats.IndexOf(ChapterExportFormat.Xml);
        await host.LayoutAsync();
        Assert.True(xmlLanguageGroup.IsEnabled);

        xmlLanguageBox.SelectedIndex = host.ViewModel.XmlLanguageOptions.ToList().IndexOf("jpn");
        chapterNameModeBox.SelectedIndex = 1;
        orderShiftBox.Value = 2;
        expressionCheckBox.IsChecked = true;
        expressionBox.Text = "t + 1";
        Assert.True(expressionBox.Bounds.Height <= 34);
        frameRateBox.SelectedIndex = 3;
        roundFramesBox.IsChecked = false;
        host.FilePickerService.SaveDirectoryPath = "out";

        await host.Window.SaveToCommand.ExecuteAsync();
        await host.LayoutAsync();

        Assert.Equal(1, host.SaveService.Calls);
        Assert.NotNull(host.SaveService.LastOptions);
        Assert.Equal(ChapterExportFormat.Xml, host.SaveService.LastOptions.Format);
        Assert.Equal("jpn", host.SaveService.LastOptions.XmlLanguage);
        Assert.Equal("out", host.SaveService.LastDirectory);
        Assert.NotNull(host.SaveService.LastInfo);
        Assert.Equal(3, host.SaveService.LastInfo.Chapters[0].DisplayNumber);
        Assert.Equal("Chapter 01", host.SaveService.LastInfo.Chapters[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(1), host.SaveService.LastInfo.Chapters[0].StartTime);

        formatBox.SelectedIndex = ChapterExportFormats.IndexOf(ChapterExportFormat.Txt);
        await host.LayoutAsync();
        Assert.False(xmlLanguageGroup.IsEnabled);
    }
}
