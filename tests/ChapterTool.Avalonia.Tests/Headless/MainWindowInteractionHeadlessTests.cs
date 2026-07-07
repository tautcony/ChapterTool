using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class MainWindowInteractionHeadlessTests
{
    [AvaloniaFact]
    public async Task Chapter_grid_empty_image_is_visible_until_rows_load()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        var emptyImage = host.RequiredControl<Control>("ChapterGridEmptyImage");
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");

        Assert.True(host.ViewModel.IsChapterGridEmpty);
        Assert.True(emptyImage.IsVisible);
        Assert.True(grid.IsVisible);

        await host.LoadAsync("movie.txt");

        Assert.False(host.ViewModel.IsChapterGridEmpty);
        Assert.False(emptyImage.IsVisible);
        Assert.NotEmpty(host.ViewModel.Rows);
    }

    [AvaloniaFact]
    public async Task Row_context_commands_are_exposed_from_visible_grid_surface()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Option("OGM", "movie.txt", "Intro", "Ending")));
        await host.LoadAsync("movie.txt");

        host.SelectRows(1);
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");

        Assert.Same(host.ViewModel.Rows[1], Assert.Single(grid.SelectedItems.Cast<object>()));
        Assert.True(host.RequiredMenuItem(grid, "InsertMenuItem").IsEnabled);
        Assert.True(host.RequiredMenuItem(grid, "DeleteMenuItem").IsEnabled);
        Assert.NotNull(host.RequiredMenuItem(grid, "ZonesMenuItem").Command);
        Assert.NotNull(host.RequiredMenuItem(grid, "ForwardShiftMenuItem").Command);
    }

    [AvaloniaFact]
    public async Task Keyboard_shortcuts_route_through_rendered_window()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.mpls",
            MainWindowHeadlessTestHost.Option("MPLS", "00001", "A"),
            MainWindowHeadlessTestHost.Option("MPLS", "00002", "B")));
        host.FilePickerService.SourcePath = "movie.mpls";
        host.FilePickerService.SaveDirectoryPath = "out";

        await host.LayoutAsync();
        await host.FocusAndPressAsync(Key.O, KeyModifiers.Control);
        await host.FocusAndPressAsync(Key.S, KeyModifiers.Control);
        await host.FocusAndPressAsync(Key.S, KeyModifiers.Alt);
        await host.FocusAndPressAsync(Key.R, KeyModifiers.Control);
        await host.FocusAndPressAsync(Key.F5);
        await host.FocusAndPressAsync(Key.L, KeyModifiers.Control);
        await host.FocusAndPressAsync(Key.F11);

        Assert.Equal(["movie.mpls", "movie.mpls", "movie.mpls"], host.LoadService.Paths);
        Assert.True(host.SaveService.Calls >= 2);
        Assert.Contains("log", host.WindowService.Opened);
        Assert.Contains("preview", host.WindowService.Opened);
    }

    [AvaloniaFact]
    public async Task Delete_key_in_expression_editor_does_not_delete_selected_chapter_rows()
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Option("OGM", "movie.txt", "Intro", "Ending")));
        await host.LoadAsync("movie.txt");
        host.SelectRows(1);

        var expressionBox = host.RequiredControl<ExpressionEditor>("ExpressionBox");
        expressionBox.Text = "time";
        expressionBox.MoveCaretToEnd();
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);

        host.Window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, string.Empty);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);

        Assert.Equal(2, host.ViewModel.Rows.Count);
    }

    [AvaloniaFact]
    public async Task Context_menu_items_respect_capability_flags()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mediaPath = Path.Combine(root, "clip.m2ts");
        await File.WriteAllBytesAsync(mediaPath, [0]);
        try
        {
            using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
                Path.Combine(root, "movie.mpls"),
                MainWindowHeadlessTestHost.OptionWithMedia(
                    "MPLS",
                    "00001",
                    new SourceMediaReference("clip.m2ts", "clip.m2ts"),
                    "A"),
                MainWindowHeadlessTestHost.Option("MPLS", "00002", "B")));
            await host.LoadAsync(Path.Combine(root, "movie.mpls"));

            var loadButton = host.RequiredControl<Button>("LoadButton");
            var clipBox = host.RequiredControl<ComboBox>("ClipBox");
            var grid = host.RequiredControl<DataGrid>("ChapterGrid");

            Assert.True(host.RequiredMenuItem(loadButton, "AppendLoadMenuItem").IsEnabled);
            Assert.True(host.RequiredMenuItem(clipBox, "ClipCombineMenuItem").IsEnabled);
            Assert.True(host.RequiredMenuItem(grid, "GridCombineMenuItem").IsEnabled);
            Assert.True(host.RequiredMenuItem(grid, "OpenMediaMenuItem").IsEnabled);
            Assert.True(host.RequiredMenuItem(grid, "InsertMenuItem").IsEnabled);
            Assert.True(host.RequiredMenuItem(grid, "DeleteMenuItem").IsEnabled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        using var emptyHost = new MainWindowHeadlessTestHost();
        await emptyHost.LayoutAsync();
        var emptyLoadButton = emptyHost.RequiredControl<Button>("LoadButton");
        var emptyGrid = emptyHost.RequiredControl<DataGrid>("ChapterGrid");

        Assert.False(emptyHost.RequiredMenuItem(emptyLoadButton, "AppendLoadMenuItem").IsEnabled);
        Assert.False(emptyHost.RequiredMenuItem(emptyGrid, "InsertMenuItem").IsEnabled);
        Assert.False(emptyHost.RequiredMenuItem(emptyGrid, "DeleteMenuItem").IsEnabled);
    }

    [AvaloniaFact]
    public async Task Auxiliary_command_surfaces_are_visible_and_bound()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");

        var previewButton = host.RequiredControl<Button>("PreviewButton");
        var settingsButton = host.RequiredControl<Button>("SettingsButton");
        var logButton = host.RequiredControl<Button>("LogButton");
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");

        Assert.True(previewButton.IsVisible);
        Assert.True(settingsButton.IsVisible);
        Assert.True(logButton.IsVisible);
        Assert.NotNull(previewButton.Command);
        Assert.NotNull(settingsButton.Command);
        Assert.NotNull(logButton.Command);
        Assert.NotNull(host.RequiredMenuItem(grid, "PreviewMenuItem").Command);
        Assert.NotNull(host.RequiredMenuItem(grid, "ZonesMenuItem").Command);
        Assert.NotNull(host.RequiredMenuItem(grid, "ForwardShiftMenuItem").Command);

        await host.Window.PreviewCommand.ExecuteAsync();

        Assert.Equal(["preview"], host.WindowService.Opened);
        Assert.Same(host.ViewModel, host.WindowService.Parameters.Single());
    }

    [AvaloniaFact]
    public async Task Icon_only_main_window_buttons_have_accessible_names()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        Assert.Equal("Preview", AutomationProperties.GetName(host.RequiredControl<Button>("PreviewButton")));
        Assert.Equal("Refresh", AutomationProperties.GetName(host.RequiredControl<Button>("RefreshButton")));
        Assert.Equal("Settings", AutomationProperties.GetName(host.RequiredControl<Button>("SettingsButton")));
        Assert.Equal("Template file", AutomationProperties.GetName(host.RequiredControl<Button>("ChapterNameTemplateButton")));
    }
}
