using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests.Headless;

internal sealed class MainWindowHeadlessTestHost : IDisposable
{
    public MainWindowHeadlessTestHost(ChapterImportResult? loadResult = null)
    {
        LoadService = new FakeLoadService(loadResult ?? ImportResult("movie.txt", Option("OGM", "movie.txt", "Intro")));
        SaveService = new FakeSaveService();
        WindowService = new FakeWindowService();
        FilePickerService = new FakeFilePickerService();
        var logService = new ApplicationLogPanelProvider();
        ViewModel = new MainWindowViewModel(
            LoadService,
            SaveService,
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            WindowService,
            new ChapterTimeFormatter(),
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            shellService: null,
            appSettingsStore: null,
            frameRateService: null,
            new AppLocalizationManager("en-US"));
        Window = new MainWindow(ViewModel, _ => FilePickerService);
    }

    public MainWindow Window { get; }

    public MainWindowViewModel ViewModel { get; }

    public FakeLoadService LoadService { get; }

    public FakeSaveService SaveService { get; }

    public FakeWindowService WindowService { get; }

    public FakeFilePickerService FilePickerService { get; }

    public static ChapterImportResult ImportResult(string path, params ChapterSourceOption[] options) =>
        new(true, [new ChapterInfoGroup(path, options, 0)], Array.Empty<ChapterDiagnostic>());

    public static ChapterSourceOption Option(string sourceType, string sourceName, params string[] chapterNames)
    {
        var chapters = chapterNames
            .Select((name, index) => new Chapter(index + 1, TimeSpan.FromSeconds(index * 10), name))
            .ToArray();
        var duration = chapters.Length == 0 ? TimeSpan.Zero : chapters[^1].Time;
        var info = new ChapterInfo(sourceName, sourceName, 0, sourceType, 24, duration, chapters);
        return new ChapterSourceOption(sourceName, sourceName, info);
    }

    public async ValueTask LoadAsync(string path)
    {
        await LayoutAsync();
        FilePickerService.SourcePath = path;
        await Window.BrowseAndLoadCommand.ExecuteAsync();
        await LayoutAsync();
    }

    public async ValueTask LayoutAsync()
    {
        Window.Show();
        Window.Width = 920;
        Window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        var layoutManager = Window.GetLayoutManager()
            ?? throw new InvalidOperationException("MainWindow layout manager was not available.");
        layoutManager.ExecuteInitialLayoutPass();
        layoutManager.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
    }

    public T RequiredControl<T>(string name)
        where T : Control =>
        Window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    public bool ContainsRenderedText(string text) =>
        ContainsRenderedText(Window, text);

    public bool ContainsRenderedText(Control scope, string text) =>
        RenderedTexts(scope).Any(rendered => string.Equals(rendered, text, StringComparison.Ordinal));

    public IReadOnlyList<string> RenderedTexts(Control scope) =>
        scope
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(static block => block.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray()!;

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChapterTool.Avalonia.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    public string DescribeRenderedTexts(Control scope) =>
        RenderedTexts(scope)
            .Aggregate(string.Empty, static (current, text) => string.IsNullOrEmpty(current) ? text! : current + Environment.NewLine + text);

    public void Dispose() => Window.Close();

    internal sealed class FakeLoadService(ChapterImportResult result) : IChapterLoadService
    {
        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    internal sealed class FakeSaveService : IChapterSaveService
    {
        public ValueTask<ChapterExportResult> SaveAsync(
            ChapterInfo info,
            ChapterExportOptions options,
            string? directory,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", Array.Empty<ChapterDiagnostic>()));
    }

    internal sealed class FakeWindowService : IWindowService
    {
        public List<string> Opened { get; } = [];

        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken)
        {
            Opened.Add(windowId);
            return ValueTask.CompletedTask;
        }

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    internal sealed class FakeFilePickerService : IFilePickerService
    {
        public string? SourcePath { get; set; }

        public ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SourcePath);

        public ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
    }
}
