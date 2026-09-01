using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;
using Optris.Icons.Avalonia;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AuxiliaryToolHeadlessTests
{
    [AvaloniaFact]
    public async Task Log_window_is_list_first_and_inspects_entries_on_explicit_request()
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

            CaptureIfRequested(window, "log-default.png");

            window.Width = 1200;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            CaptureIfRequested(window, "log-wide.png");
            window.Width = 760;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.Equal(2, list.Items.Count);
            Assert.Null(viewModel.SelectedEntry);
            Assert.False(viewModel.IsDetailsOpen);
            Assert.False(viewModel.ShowDetails);

            var filterButton = window.GetVisualDescendants().OfType<Button>().Single(static item => item.Name == "LogFilterButton");
            if (filterButton.Flyout is not null)
            {
                filterButton.Flyout.ShowAt(filterButton);
                await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            }

            var filter = window.GetVisualDescendants().OfType<ComboBox>()
                .SingleOrDefault(static item => item.Name == "LogSeverityFilter");
            if (filter is not null)
            {
                filter.SelectedIndex = 2;
                Dispatcher.UIThread.RunJobs();
                await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
                Assert.Single(viewModel.FilteredEntries);
                Assert.Equal(LogSeverityFilter.Warning, viewModel.SelectedFilter.Value);

                filter.SelectedIndex = 0;
            }

            // Close the transient filter surface before exercising later layout states.
            filterButton.Flyout?.Hide();
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var search = window.GetVisualDescendants().OfType<TextBox>().Single(static box => box.Name == "LogSearch");
            search.Text = "warning";
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Single(viewModel.FilteredEntries);

            search.Text = string.Empty;
            var warning = Assert.Single(viewModel.FilteredEntries, entry => entry.Summary == "Initial warning");
            list.SelectedItem = warning;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Same(warning, viewModel.SelectedEntry);
            Assert.False(viewModel.IsDetailsOpen);

            var detailsButton = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("logDetailsAction")
                    && button.IsVisible
                    && ReferenceEquals(button.CommandParameter, warning));
            Assert.NotNull(detailsButton.Command);
            detailsButton.Command!.Execute(detailsButton.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(viewModel.IsDetailsOpen);
            Assert.True(viewModel.ShowDetails);
            var detailsHeader = window.GetVisualDescendants().OfType<Border>()
                .Single(control => control.Name == "LogDetailsHeader");
            Assert.Equal(42, detailsHeader.Bounds.Height);
            Assert.Equal(new Thickness(0), detailsHeader.BorderThickness);
            Assert.Equal(1, detailsHeader.GetVisualDescendants().OfType<Button>()
                .Count(static button => button.IsEffectivelyVisible));
            var detailsTabs = window.GetVisualDescendants().OfType<TabControl>()
                .Single(control => control is { IsVisible: true, Name: "LogDetailsTabs" });
            Assert.Equal(2, detailsTabs.Items.Count);

            var rawExpander = window.GetVisualDescendants().OfType<Expander>()
                .FirstOrDefault(expander => string.Equals(expander.Header?.ToString(), localizer.GetString("Tool.Log.Raw"), StringComparison.Ordinal));
            if (rawExpander is not null)
            {
                rawExpander.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            }

            CaptureIfRequested(window, "log-raw.png");
            if (rawExpander is not null)
            {
                Assert.Contains(
                    window.GetVisualDescendants().OfType<SelectableTextBlock>(),
                    block => block.IsVisible && block.Text?.Contains("\"message\": \"Initial warning\"", StringComparison.Ordinal) == true);
                Assert.DoesNotContain(localizer.GetString("Tool.Log.NoDetails"), MainWindowHeadlessTestHost.RenderedTexts(window));
            }

            await viewModel.CopySummaryCommand.ExecuteAsync();
            Assert.Contains("Initial warning", clipboard.Text, StringComparison.Ordinal);

            await viewModel.CopyDetailsCommand.ExecuteAsync();
            Assert.Contains("\"message\": \"Initial warning\"", clipboard.Text, StringComparison.Ordinal);

            var closeName = localizer.GetString("Tool.Log.CloseDetails");
            var closeButton = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.IsVisible && string.Equals(AutomationProperties.GetName(button), closeName, StringComparison.Ordinal));
            closeButton.Command!.Execute(closeButton.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Same(warning, viewModel.SelectedEntry);
            Assert.False(viewModel.IsDetailsOpen);

            await viewModel.OpenDetailsCommand.ExecuteAsync(warning);
            Assert.True(viewModel.IsDetailsOpen);
            list.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
                Source = view
            });
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.False(viewModel.IsDetailsOpen);
            Assert.Same(warning, viewModel.SelectedEntry);
            var focused = TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement();
            Assert.True(
                focused is Control focusedControl
                && (ReferenceEquals(focusedControl, list)
                    || ReferenceEquals(focusedControl, detailsButton)
                    || (focusedControl.FindAncestorOfType<ListBoxItem>()?.DataContext is LogEntryViewModel focusedEntry
                        && ReferenceEquals(focusedEntry, warning))),
                $"Expected focus to return to the selected log row, got {focused?.GetType().Name ?? "none"}.");

            await viewModel.ClearCommand.ExecuteAsync();
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.True(viewModel.IsEmpty);

            logger.LogError("After clear");
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.Single(viewModel.FilteredEntries);
            Assert.Contains("After clear", viewModel.FilteredEntries[0].Summary, StringComparison.Ordinal);
            Assert.Null(viewModel.SelectedEntry);
            Assert.False(viewModel.IsDetailsOpen);

            window.Width = 420;
            await viewModel.OpenDetailsCommand.ExecuteAsync(viewModel.FilteredEntries[0]);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            CaptureIfRequested(window, "log-narrow.png");
            var actionButtons = window.GetVisualDescendants().OfType<Button>()
                .Where(button => button is { IsEffectivelyVisible: true, Command: not null })
                .ToArray();
            Assert.NotEmpty(actionButtons);
            var contentSurface = window.GetVisualDescendants().OfType<Grid>()
                .SingleOrDefault(static control => control.Name == "LogContentSurface");
            var listHost = window.GetVisualDescendants().OfType<Grid>()
                .SingleOrDefault(static control => control.Name == "LogListHost");
            var detailsPanel = window.GetVisualDescendants().OfType<Border>()
                .SingleOrDefault(static control => control.Name == "LogDetailsPanel");
            Assert.All(actionButtons, button => Assert.True(
                button.Bounds.Right <= window.Bounds.Width + 1,
                $"{button.Name}/{AutomationProperties.GetName(button)} classes={string.Join(',', button.Classes)} right={button.Bounds.Right} width={button.Bounds.Width} window={window.Bounds.Width} surface={contentSurface?.Bounds} listVisible={listHost?.IsVisible} details={detailsPanel?.Bounds}"));
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Log_inspector_keeps_the_list_visible_at_wide_width()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        var logger = logService.CreateLogger("ChapterTool.Headless");
        logger.LogInformation("Wide information");
        logger.LogWarning("Wide warning");
        using var viewModel = new LogToolViewModel(logService, localizer, new FakeClipboardService());
        var view = new LogToolView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 1200, Height = 460 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var entry = Assert.Single(viewModel.FilteredEntries, item => item.Summary == "Wide warning");
            await viewModel.OpenDetailsCommand.ExecuteAsync(entry);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            var listHost = window.GetVisualDescendants().OfType<Grid>()
                .Single(static control => control.Name == "LogListHost");
            var detailsPanel = window.GetVisualDescendants().OfType<Border>()
                .Single(static control => control.Name == "LogDetailsPanel");

            Assert.True(viewModel.ShowDetails);
            Assert.True(list.IsEffectivelyVisible);
            Assert.True(listHost.IsEffectivelyVisible);
            Assert.True(detailsPanel.IsEffectivelyVisible);
            Assert.True(list.Bounds.Width > 0);
            Assert.InRange(detailsPanel.Bounds.Width, 400, 480);

            foreach (var button in window.GetVisualDescendants().OfType<Button>()
                         .Where(static button => button.IsEffectivelyVisible))
            {
                var origin = button.TranslatePoint(default, window);
                Assert.NotNull(origin);
                Assert.InRange(origin.Value.X, 0, window.Bounds.Width);
                Assert.InRange(origin.Value.X + button.Bounds.Width, 0, window.Bounds.Width + 1);
            }
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Log_inspector_hides_empty_technical_sections_for_sparse_entries()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        logService.CreateLogger("ChapterTool.Headless").LogInformation("Sparse entry");
        using var viewModel = new LogToolViewModel(logService, localizer);
        var view = new LogToolView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 760, Height = 460 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var entry = Assert.Single(viewModel.FilteredEntries);
            await viewModel.OpenDetailsCommand.ExecuteAsync(entry);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var rendered = MainWindowHeadlessTestHost.RenderedTexts(window);
            Assert.Contains("Sparse entry", rendered);
            Assert.DoesNotContain(localizer.GetString("Tool.Log.TechnicalDetail"), rendered);
            Assert.DoesNotContain(localizer.GetString("Tool.Log.Exception"), rendered);
            Assert.DoesNotContain(localizer.GetString("Tool.Log.StructuredData"), rendered);
            Assert.Contains(localizer.GetString("Tool.Log.Raw"), rendered);
            Assert.Contains(localizer.GetString("Tool.Log.NoDetails"), rendered);
            Assert.Contains(
                window.GetVisualDescendants().OfType<TabControl>(),
                static control => control is { IsVisible: true, Name: "LogDetailsTabs" });
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Log_inspector_renders_scalar_properties_and_technical_search_indicator()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        var state = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = "Import finished",
            ["code"] = "Import.Partial",
            ["TechnicalDetail"] = "hidden-token"
        };
        logService.CreateLogger("ChapterTool.Headless").Log(
            LogLevel.Warning,
            new EventId(4, "Diagnostic"),
            state,
            null,
            static (_, _) => "Import diagnostic: severity=Warning, code=Import.Partial");
        using var viewModel = new LogToolViewModel(logService, localizer);
        var view = new LogToolView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 760, Height = 460 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            viewModel.SearchText = "hidden-token";
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var entry = Assert.Single(viewModel.FilteredEntries);
            Assert.True(entry.HasAdditionalSearchMatch);
            Assert.Contains(
                window.GetVisualDescendants().OfType<Icon>(),
                static icon => icon.IsEffectivelyVisible
                    && (AutomationProperties.GetName(icon) ?? string.Empty).Contains("details", StringComparison.OrdinalIgnoreCase));

            await viewModel.OpenDetailsCommand.ExecuteAsync(entry);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var detailsTabs = window.GetVisualDescendants().OfType<TabControl>().Single(static control => control.Name == "LogDetailsTabs");
            detailsTabs.SelectedIndex = 0;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var rendered = MainWindowHeadlessTestHost.RenderedTexts(window);
            Assert.Contains(localizer.GetString("Tool.Log.Properties"), rendered);
            Assert.Contains("Code", rendered);
            Assert.Contains("Import.Partial", rendered);
            Assert.DoesNotContain(localizer.GetString("Tool.Log.NoDetails"), rendered);
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    [AvaloniaFact]
    public async Task Log_keyboard_enter_and_space_open_the_selected_entry_inspector()
    {
        var localizer = new AppLocalizationManager("en-US");
        using var localizationAdapter = new AvaloniaLocalizationResourceAdapter(localizer);
        var logService = new ApplicationLogPanelProvider();
        logService.CreateLogger("ChapterTool.Headless").LogInformation("Keyboard entry");
        using var viewModel = new LogToolViewModel(logService, localizer);
        var view = new LogToolView { DataContext = viewModel };
        var window = new Window { Content = view, Width = 760, Height = 460 };

        try
        {
            window.Show();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            var entry = Assert.Single(viewModel.FilteredEntries);
            list.SelectedItem = entry;
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            list.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Enter,
                Source = list
            });
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.True(viewModel.IsDetailsOpen);

            await viewModel.CloseDetailsCommand.ExecuteAsync();
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            list.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Space,
                Source = list
            });
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);
            Assert.True(viewModel.IsDetailsOpen);
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    private static void CaptureIfRequested(Window window, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("CHAPTERTOOL_CAPTURE_LOG_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var width = Math.Max(1, (int)Math.Ceiling(window.Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(window.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        bitmap.Render(window);
        var path = Path.Combine(directory, fileName);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 256, $"Screenshot was empty: {path}");
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
            themeService.Apply(new ThemeSettings("ayu-dark"));
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var expected = ImportedThemeBrushColor("Brush.Contents");
            Assert.Contains(
                window.GetVisualDescendants().OfType<Border>(),
                border => border.Background is SolidColorBrush brush && brush.Color == expected);
        }
        finally
        {
            themeService.Apply(ThemeSettings.Default);
            Dispatcher.UIThread.RunJobs();
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    private static Color BrushColor(IBrush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Color ImportedThemeBrushColor(string key)
    {
        var application = Application.Current!;
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
