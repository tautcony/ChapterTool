using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class UiResourceResolutionHeadlessTests
{
    public static TheoryData<string> PresetIds { get; } =
    [
        ThemePresetCatalog.DefaultPresetId,
        "ayu-dark"
    ];

    [AvaloniaTheory]
    [MemberData(nameof(PresetIds))]
    public async Task Main_view_and_tool_views_resolve_theme_brushes(string presetId)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia application was not initialized.");
        var themeService = new AvaloniaThemeApplicationService();

        try
        {
            themeService.Apply(new ThemeSettings(presetId));
            Dispatcher.UIThread.RunJobs();

            using var host = new MainWindowHeadlessTestHost(
                themeSettings: new ThemeSettings(presetId));
            await host.LayoutAsync(width: 800, height: 600);

            AssertResolvedBrush(host.MainView.Background, "MainView.Background");
            AssertResolvedBrush(host.RequiredControl<SplitButton>("LoadButton").Foreground, "LoadButton.Foreground");
            AssertResolvedBrush(host.RequiredControl<Button>("SaveButton").Background, "SaveButton.Background");
            AssertResolvedBrush(host.RequiredControl<DataGrid>("ChapterGrid").HorizontalGridLinesBrush, "ChapterGrid.HorizontalGridLinesBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.FrameAccurateBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.FrameInexactBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.FrameNeutralBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.LogInformationBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.LogWarningBrush");
            AssertResolvedApplicationBrush(application, "ChapterTool.LogErrorBrush");

            await AssertToolResolvesAsync(
                new LanguageToolView { DataContext = new LanguageToolViewModel(host.ViewModel.ToolSession.Preferences) });
            await AssertToolResolvesAsync(
                new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(host.ViewModel.ToolSession.ChapterEdit) });
            await AssertToolResolvesAsync(
                new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(host.ViewModel.ToolSession.NamingPreferences) });
            await AssertToolResolvesAsync(
                new ExpressionToolView { DataContext = new ExpressionToolViewModel(host.ViewModel.ToolSession.Expression) });
            await AssertToolResolvesAsync(
                new TextToolView { DataContext = new TextToolViewModel(() => "00:00:00.000 Intro") });

            var logService = new ApplicationLogPanelProvider();
            logService.CreateLogger("ChapterTool.Headless").LogInformation("Resource resolution");
            using var logViewModel = new LogToolViewModel(logService, host.Localizer);
            await AssertToolResolvesAsync(new LogToolView { DataContext = logViewModel });

            using var settingsViewModel = new SettingsToolViewModel(
                host.ViewModel.ToolSession.Preferences,
                host.SettingsStore,
                host.Localizer,
                autoLoad: false);
            await settingsViewModel.LoadAsync(TestContext.Current.CancellationToken);
            await AssertToolResolvesAsync(new SettingsToolView { DataContext = settingsViewModel });
        }
        finally
        {
            themeService.Apply(ThemeSettings.Default);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async ValueTask AssertToolResolvesAsync(Control view)
    {
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(view, view.DataContext ?? new object());
        try
        {
            AssertResolvedBrush(
                (view as TemplatedControl)?.Background ?? window.Background,
                $"{view.GetType().Name}.Background");
            var sample = window.GetVisualDescendants()
                .Select(visual => visual switch
                {
                    TemplatedControl templated => templated.Background,
                    TextBlock textBlock => textBlock.Foreground,
                    Border border => border.Background,
                    _ => null
                })
                .FirstOrDefault(brush => brush is not null);
            AssertResolvedBrush(sample, $"{view.GetType().Name}.sample brush");
        }
        finally
        {
            await MainWindowHeadlessTestHost.CloseWindowAsync(window);
        }
    }

    private static void AssertResolvedApplicationBrush(Application application, string key)
    {
        Assert.True(application.TryGetResource(key, out var resource), $"Missing application resource '{key}'.");
        AssertResolvedBrush(resource as IBrush, key);
    }

    private static void AssertResolvedBrush(IBrush? brush, string description)
    {
        Assert.NotNull(brush);
        if (brush is SolidColorBrush solid)
        {
            Assert.NotEqual(default, solid.Color);
            return;
        }

        Assert.False(brush is IImmutableSolidColorBrush immutable && immutable.Color == default, description);
    }
}
