using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ChapterGridCaretHeadlessTests
{
    [AvaloniaTheory]
    [InlineData(ChapterGridColumnIds.Time, 800, 600)]
    [InlineData(ChapterGridColumnIds.Name, 800, 600)]
    [InlineData(ChapterGridColumnIds.Frames, 800, 600)]
    [InlineData(ChapterGridColumnIds.Time, 1280, 800)]
    [InlineData(ChapterGridColumnIds.Name, 1280, 800)]
    [InlineData(ChapterGridColumnIds.Frames, 1280, 800)]
    [InlineData(ChapterGridColumnIds.Time, 760, 520)]
    [InlineData(ChapterGridColumnIds.Name, 760, 520)]
    [InlineData(ChapterGridColumnIds.Frames, 760, 520)]
    public async Task Caret_tracks_rendered_text_when_entering_and_navigating_cell_edit(
        string columnId, double width, double height)
    {
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", "Chapter 01")));
        await host.LoadAsync("movie.txt");
        await host.LayoutAsync(width, height);
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        grid.SelectedItem = host.ViewModel.Rows[0];
        grid.CurrentColumn = Assert.Single(grid.Columns, column => column.Tag?.ToString() == columnId);
        grid.Focus();
        host.Window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, string.Empty);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);

        var editor = Assert.Single(grid.GetVisualDescendants().OfType<TextBox>(),
            textBox => textBox.Classes.Contains("gridEditor"));
        Assert.True(editor.IsFocused);
        editor.CaretBrush = Brushes.Magenta;
        AssertCaretMatchesText(editor);

        foreach (var (key, physicalKey, index) in new[]
                 {
                     (Key.End, PhysicalKey.End, editor.Text!.Length),
                     (Key.Left, PhysicalKey.ArrowLeft, editor.Text.Length - 1),
                     (Key.Home, PhysicalKey.Home, 0)
                 })
        {
            host.Window.KeyPress(key, RawInputModifiers.None, physicalKey, string.Empty);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            Assert.Equal(index, editor.CaretIndex);
            AssertCaretMatchesText(editor);
        }

        host.Window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, string.Empty);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
        var presenter = Assert.Single(editor.GetVisualDescendants().OfType<TextPresenter>());
        var textBounds = presenter.TextLayout.HitTestTextRange(0, editor.Text!.Length).Single();
        var textCenter = presenter.TranslatePoint(textBounds.Center, editor);
        Assert.NotNull(textCenter);
        Assert.InRange(Math.Abs(textCenter.Value.X - editor.Bounds.Width / 2), 0, 1.5);
        if (columnId == ChapterGridColumnIds.Name)
        {
            MainWindowHeadlessTestHost.CaptureRenderedFrame(host.Window,
                Path.Combine("artifacts", "chapter-grid-caret", $"name-{width}x{height}.png"));
            host.Window.KeyTextInput(" appended");
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            Assert.Equal("Chapter 01 appended", editor.Text);
            AssertCaretMatchesText(editor);
            host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, string.Empty);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
            Assert.Equal("Chapter 01 appended", host.ViewModel.Rows[0].Name);
        }
        else
        {
            host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, string.Empty);
        }
    }

    [AvaloniaFact]
    public async Task Long_name_caret_remains_visible_after_scrolling_resizing_and_typing()
    {
        var name = string.Concat(Enumerable.Repeat("章节 Chapter ", 20));
        using var host = new MainWindowHeadlessTestHost(MainWindowHeadlessTestHost.ImportResult(
            "movie.txt",
            MainWindowHeadlessTestHost.Entry(ChapterImportFormat.Ogm, "movie.txt", name)));
        await host.LoadAsync("movie.txt");
        await host.LayoutAsync(760, 520);
        var grid = host.RequiredControl<DataGrid>("ChapterGrid");
        grid.SelectedItem = host.ViewModel.Rows[0];
        grid.CurrentColumn = Assert.Single(grid.Columns, column => column.Tag?.ToString() == ChapterGridColumnIds.Name);
        grid.Focus();
        host.Window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, string.Empty);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
        var editor = Assert.Single(grid.GetVisualDescendants().OfType<TextBox>(),
            textBox => textBox.Classes.Contains("gridEditor"));
        editor.CaretBrush = Brushes.Magenta;

        foreach (var (width, height) in new[] { (760, 520), (1280, 800), (800, 600) })
        {
            await host.LayoutAsync(width, height);
            foreach (var (key, physicalKey) in new[] { (Key.End, PhysicalKey.End), (Key.Home, PhysicalKey.Home) })
            {
                host.Window.KeyPress(key, RawInputModifiers.None, physicalKey, string.Empty);
                await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
                AssertCaretMatchesText(editor);
                AssertCaretIsVisible(editor);
            }
        }

        host.Window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, string.Empty);
        host.Window.KeyTextInput("末尾");
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
        Assert.Equal(name + "末尾", editor.Text);
        AssertCaretMatchesText(editor);
        AssertCaretIsVisible(editor);
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, string.Empty);
        await MainWindowHeadlessTestHost.ExecuteLayoutAsync(host.Window);
        Assert.Equal(name + "末尾", host.ViewModel.Rows[0].Name);
    }

    private static void AssertCaretIsVisible(TextBox editor)
    {
        var request = RequestInputClient(editor);
        Assert.InRange(request.Client!.CursorRectangle.X, -1, editor.Bounds.Width + 1);
    }

    private static void AssertCaretMatchesText(TextBox editor)
    {
        var presenter = Assert.Single(editor.GetVisualDescendants().OfType<TextPresenter>());
        presenter.ShowCaret();
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            presenter.Render(context);
        }

        var caret = Assert.Single(drawing.Children.OfType<GeometryDrawing>(), child =>
            child.Pen?.Brush == Brushes.Magenta);
        Assert.NotNull(caret.Geometry);
        var renderedCaretX = caret.Geometry.Bounds.X;
        var textPosition = presenter.TextLayout.HitTestTextPosition(editor.CaretIndex);
        Assert.True(Math.Abs(textPosition.X - renderedCaretX) <= 1.5,
            $"Rendered caret {editor.CaretIndex}: text X={textPosition.X:F2}, caret X={renderedCaretX:F2}.");
        var request = RequestInputClient(editor);
        var transform = presenter.TransformToVisual(editor);
        Assert.NotNull(transform);
        var expected = presenter.TextLayout.HitTestTextPosition(editor.CaretIndex)
            .TransformToAABB(transform.Value);
        var actual = request.Client!.CursorRectangle;
        Assert.True(Math.Abs(expected.X - actual.X) <= 1,
            $"Caret {editor.CaretIndex}: text X={expected.X:F2}, caret X={actual.X:F2}.");
    }

    private static TextInputMethodClientRequestedEventArgs RequestInputClient(TextBox editor)
    {
        var request = new TextInputMethodClientRequestedEventArgs
        {
            RoutedEvent = InputElement.TextInputMethodClientRequestedEvent
        };
        editor.RaiseEvent(request);
        Assert.NotNull(request.Client);
        return request;
    }
}
