using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels.Tools;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AuxiliaryToolHeadlessTests
{
    [AvaloniaFact]
    public async Task Log_window_filters_selects_copies_clears_and_receives_new_entries()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Headless");
        logger.LogInformation("Initial information");
        logger.LogWarning("Initial warning");
        var clipboard = new FakeClipboardService();
        using var viewModel = new LogToolViewModel(logService, localizer, clipboard);
        var view = new LogToolView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 760, Height = 460 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.Equal(2, list.Items.Count);

            var filter = window.GetVisualDescendants().OfType<ComboBox>().Single();
            filter.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Single(viewModel.FilteredEntries);
            Assert.Equal(LogSeverityFilter.Warning, viewModel.SelectedFilter.Value);

            filter.SelectedIndex = 0;
            list.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.NotNull(viewModel.SelectedEntry);

            await viewModel.CopySummaryCommand.ExecuteAsync();
            Assert.Contains("Initial warning", clipboard.Text, StringComparison.Ordinal);

            await viewModel.ClearCommand.ExecuteAsync();
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.True(viewModel.IsEmpty);

            logger.LogError("After clear");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Single(viewModel.FilteredEntries);
            Assert.Contains("After clear", viewModel.FilteredEntries[0].Summary, StringComparison.Ordinal);

            window.Width = 420;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var actionButtons = window.GetVisualDescendants().OfType<Button>().Where(button => button.Command is not null).ToArray();
            Assert.NotEmpty(actionButtons);
            Assert.All(actionButtons, button => Assert.True(button.Bounds.Right <= window.Bounds.Width + 1));
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Auxiliary_log_surface_refreshes_when_theme_changes()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        logService.CreateLogger("ChapterTool.Headless").LogInformation("Theme check");
        using var viewModel = new LogToolViewModel(logService, localizer);
        var window = new Window
        {
            Content = new LogToolView { DataContext = viewModel },
            Width = 760,
            Height = 460
        };
        var themeService = new AvaloniaThemeApplicationService();

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            themeService.Apply(new ChapterTool.Infrastructure.Configuration.ThemeSettings("ayu-dark"));
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var expected = SourceGitBrushColor("Brush.Contents");
            Assert.Contains(
                window.GetVisualDescendants().OfType<Border>(),
                border => border.Background is SolidColorBrush brush && brush.Color == expected);
            Assert.Equal(expected, ResourceColor(AvaloniaThemeApplicationService.AuxiliaryContentBackgroundBrushKey));
        }
        finally
        {
            themeService.Apply(ChapterTool.Infrastructure.Configuration.ThemeSettings.Default);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    private static Color BrushColor(IBrush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Color ResourceColor(string key) =>
        BrushColor(global::Avalonia.Application.Current!.Resources[key] as IBrush);

    private static Color SourceGitBrushColor(string key)
    {
        var application = global::Avalonia.Application.Current!;
        Assert.True(application.TryGetResource(key, out var resource));
        return BrushColor(resource as IBrush);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public ValueTask<string?> GetTextAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Text);

        public ValueTask SetTextAsync(string value, CancellationToken cancellationToken)
        {
            Text = value;
            return ValueTask.CompletedTask;
        }
    }
}
