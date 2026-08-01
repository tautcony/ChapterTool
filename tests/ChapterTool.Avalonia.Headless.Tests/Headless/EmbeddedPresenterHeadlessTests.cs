using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.Views;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class EmbeddedPresenterHeadlessTests
{
    [AvaloniaFact]
    public async Task Explicit_presenter_controls_embedded_content_visibility()
    {
        using var host = new MainWindowHeadlessTestHost();
        var presenter = new EmbeddedToolPresenter();
        var view = new MainView(host.ViewModel, _ => host.FilePickerService, presenter);
        var window = new Window { Content = view, Width = 736, Height = 576 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var contentHost = view.FindControl<ContentControl>("SecondarySurface")
                ?? throw new InvalidOperationException("The embedded content host was not created.");

            Assert.False(contentHost.IsVisible);

            var content = new Border { DataContext = new object() };
            presenter.SetContent(ToolIds.Settings, content);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(contentHost.IsVisible);
            Assert.Same(content, contentHost.Content);

            presenter.SetContent(null, null);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.False(contentHost.IsVisible);
            Assert.Null(contentHost.Content);
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }
}
