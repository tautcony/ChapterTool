using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Headless.Tests.Headless;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Importing.Runtime;

namespace ChapterTool.Avalonia.Headless.Tests.Composition;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AppCompositionRootIdentityHeadlessTests
{
    [AvaloniaFact]
    public async Task Production_editor_hosts_use_the_same_composed_authoring_service()
    {
        var sentinel = new SentinelAuthoringService();
        var settingsDirectory = CreateTempDirectory();
        using var composition = new AppCompositionRoot(
            startupPath: null,
            settingsDirectory: settingsDirectory,
            expressionAuthoringServiceOverride: sentinel);
        var mainWindow = composition.CreateMainWindow();
        try
        {
            mainWindow.Show();
            await LayoutAsync(mainWindow);

            var mainEditor = mainWindow.FindControl<ExpressionEditor>("ExpressionBox")
                ?? throw new InvalidOperationException("Main expression editor was not created.");
            mainEditor.Text = "sentinel";
            await LayoutAsync(mainWindow);
            Assert.Contains(mainEditor.CurrentCompletions, completion => completion.Text == "sentinel");

            var viewModel = Assert.IsType<MainWindowViewModel>(mainWindow.DataContext);
            await viewModel.ExpressionCommand.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);
            var toolWindow = WindowsFor(viewModel)
                .Single(window => window.Content is ExpressionToolView);
            await LayoutAsync(toolWindow);

            var toolEditor = toolWindow.GetVisualDescendants().OfType<ExpressionEditor>().Single();
            toolEditor.Text = "sentinel";
            await LayoutAsync(toolWindow);

            Assert.Contains(toolEditor.CurrentCompletions, completion => completion.Text == "sentinel");
            Assert.Same(sentinel, viewModel.ExpressionAuthoringService);
            Assert.True(sentinel.AnalyzeCalls >= 4);
            Assert.Contains(sentinel.Expressions, expression => expression == "sentinel");
        }
        finally
        {
            foreach (var window in WindowsFor(Assert.IsType<MainWindowViewModel>(mainWindow.DataContext)).ToArray())
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task Composition_reuses_shared_services_across_runtime_factories_and_tool_windows()
    {
        var settingsDirectory = CreateTempDirectory();
        using var composition = new AppCompositionRoot(settingsDirectory: settingsDirectory);
        var registry = Assert.IsType<RuntimeChapterImporterRegistry>(composition.CreateChapterImporterRegistry());
        var save = Assert.IsType<RuntimeChapterSaveService>(composition.CreateChapterSaveService());

        Assert.Same(composition.Formatter, registry.Formatter);
        Assert.Same(composition.ExternalToolLocator, registry.ToolLocator);
        Assert.Same(composition.CreateChapterExportService(), save.Exporter);
        Assert.Same(composition.CreateChapterExportService(), composition.CreateChapterExportService());
        Assert.Same(composition.CreateExpressionAuthoringService(), composition.CreateExpressionAuthoringService());
        Assert.Same(composition.ExternalToolLocator, composition.CreateExternalToolLocator());

        var mainWindow = composition.CreateMainWindow();
        try
        {
            mainWindow.Show();
            await LayoutAsync(mainWindow);
            var viewModel = Assert.IsType<MainWindowViewModel>(mainWindow.DataContext);
            Assert.Same(composition.CreateExpressionAuthoringService(), viewModel.ExpressionAuthoringService);

            await viewModel.SettingsCommand.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);
            var settingsWindow = WindowsFor(viewModel)
                .Single(window => window.Content is SettingsToolView);
            await LayoutAsync(settingsWindow);
            var settings = settingsWindow.Content is SettingsToolView { DataContext: SettingsToolViewModel value }
                ? value
                : throw new InvalidOperationException("Settings ViewModel was not rendered.");
            Assert.Same(composition.SettingsStore, settings.SettingsStoreForTesting);
            Assert.Same(composition.ExternalToolLocator, settings.ExternalToolLocatorForTesting);
        }
        finally
        {
            foreach (var window in WindowsFor(Assert.IsType<MainWindowViewModel>(mainWindow.DataContext)).ToArray())
            {
                window.Close();
            }
        }
    }

    private static async Task LayoutAsync(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.GetLayoutManager()?.ExecuteInitialLayoutPass();
        window.GetLayoutManager()?.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
    }

    private static IReadOnlyList<Window> WindowsFor(MainWindowViewModel viewModel)
    {
        var serviceField = typeof(MainWindowViewModel).GetField(
            "windowService",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Main window service field was not found.");
        var service = serviceField.GetValue(viewModel)
            ?? throw new InvalidOperationException("Main window service was not created.");
        var windowsField = service.GetType().GetField(
            "windows",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Window registry was not found.");
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, Window>>(windowsField.GetValue(service)!).Values.ToArray();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class SentinelAuthoringService : IExpressionAuthoringService
    {
        public int AnalyzeCalls { get; private set; }

        public List<string> Expressions { get; } = [];

        public IReadOnlyList<ExpressionSymbol> Symbols { get; } =
            [new("sentinel", ExpressionTokenKind.Function, "Sentinel", 0, "sentinel()")];

        public ExpressionAnalysisResult Analyze(string expression, int caretIndex, decimal timeSeconds = 0, decimal framesPerSecond = 24)
        {
            AnalyzeCalls++;
            Expressions.Add(expression);
            var start = Math.Clamp(caretIndex - expression.Length, 0, expression.Length);
            return new ExpressionAnalysisResult(
                [],
                [new ExpressionCompletion("sentinel", ExpressionTokenKind.Function, "Sentinel", start, expression.Length, "sentinel()")],
                []);
        }
    }
}
