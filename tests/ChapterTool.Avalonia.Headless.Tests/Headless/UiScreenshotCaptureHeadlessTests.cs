using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class UiScreenshotCaptureHeadlessTests
{
    [AvaloniaFact]
    public async Task Capture_main_window_and_tool_views_when_requested()
    {
        var set = Environment.GetEnvironmentVariable("CHAPTERTOOL_UI_SCREENSHOT_SET");
        if (string.IsNullOrWhiteSpace(set))
        {
            return;
        }

        var sizes = new (string Name, double Width, double Height)[]
        {
            ("default", 800, 600),
            ("wide", 1280, 720),
            ("narrow", 760, 520)
        };

        var themeService = new AvaloniaThemeApplicationService();
        try
        {
        if (set.Contains("dark", StringComparison.OrdinalIgnoreCase))
        {
            themeService.Apply(new ThemeSettings("ayu-dark"));
        }
        else
        {
            themeService.Apply(ThemeSettings.Default);
        }

        using var host = new MainWindowHeadlessTestHost();
        foreach (var (name, width, height) in sizes)
        {
            await host.LayoutAsync(width, height);
            MainWindowHeadlessTestHost.CaptureRenderedFrame(
                host.Window,
                Path.Combine("artifacts", "unify-sourcegit-design-system", set, $"main-{name}.png"));
        }

        await CaptureToolAsync(
            set,
            "language",
            new LanguageToolView { DataContext = new LanguageToolViewModel(host.ViewModel.PortAdapters.Preferences) },
            sizes);
        await CaptureToolAsync(
            set,
            "forward-shift",
            new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(host.ViewModel.PortAdapters.ChapterEdit) },
            sizes);
        await CaptureToolAsync(
            set,
            "template-names",
            new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(host.ViewModel.PortAdapters.NamingPreferences) },
            sizes);
        await CaptureToolAsync(
            set,
            "expression",
            new ExpressionToolView { DataContext = new ExpressionToolViewModel(host.ViewModel.PortAdapters.Expression) },
            sizes);
        await CaptureToolAsync(
            set,
            "text",
            new TextToolView { DataContext = new TextToolViewModel(() => "00:00:00.000 Intro") },
            sizes);

        var logService = new ApplicationLogPanelProvider();
        logService.CreateLogger("ChapterTool.Headless").LogInformation("Screenshot capture");
        using var logViewModel = new LogToolViewModel(logService, host.Localizer);
        await CaptureToolAsync(set, "log", new LogToolView { DataContext = logViewModel }, sizes);

        using var settingsViewModel = new SettingsToolViewModel(
            host.ViewModel.PortAdapters.Preferences,
            host.SettingsStore,
            host.Localizer,
            autoLoad: false);
        await settingsViewModel.LoadAsync(TestContext.Current.CancellationToken);
        await CaptureToolAsync(set, "settings", new SettingsToolView { DataContext = settingsViewModel }, sizes);
        }
        finally
        {
            themeService.Apply(ThemeSettings.Default);
        }
    }

    private static async ValueTask CaptureToolAsync(
        string set,
        string viewName,
        Control view,
        IReadOnlyList<(string Name, double Width, double Height)> sizes)
    {
        foreach (var (name, width, height) in sizes)
        {
            var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, view.DataContext ?? new object(), width, height);
            try
            {
                MainWindowHeadlessTestHost.CaptureRenderedFrame(
                    window,
                    Path.Combine("artifacts", "unify-sourcegit-design-system", set, $"{viewName}-{name}.png"));
            }
            finally
            {
                await MainWindowHeadlessTestHost.CloseWindowAsync(window);
            }
        }
    }
}
