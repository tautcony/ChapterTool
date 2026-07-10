using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Tests.Headless;

internal sealed class MainWindowHeadlessTestHost : IDisposable
{
    private readonly ApplicationLogPanelProvider logService;

    public MainWindowHeadlessTestHost(
        ChapterImportResult? loadResult = null,
        AppLocalizationManager? localizer = null,
        AppSettings? appSettings = null,
        ThemeSettings? themeSettings = null,
        IShellService? shellService = null,
        FontSettings? fontSettings = null)
        : this(
            loadResult is null
                ? [ImportResult("movie.txt", Entry(ChapterImportFormat.Ogm, "movie.txt", "Intro"))]
                : [loadResult],
            localizer,
            appSettings,
            themeSettings,
            shellService,
            fontSettings)
    {
    }

    public MainWindowHeadlessTestHost(
        IReadOnlyList<ChapterImportResult> loadResults,
        AppLocalizationManager? localizer = null,
        AppSettings? appSettings = null,
        ThemeSettings? themeSettings = null,
        IShellService? shellService = null,
        FontSettings? fontSettings = null)
    {
        Localizer = localizer ?? new AppLocalizationManager("en-US");
        LoadService = new FakeLoadService(loadResults.Count == 0
            ? [ImportResult("movie.txt", Entry(ChapterImportFormat.Ogm, "movie.txt", "Intro"))]
            : loadResults);
        SaveService = new FakeSaveService();
        WindowService = new FakeWindowService();
        FilePickerService = new FakeFilePickerService();
        SettingsPickerService = new FakeSettingsPickerService();
        AppSettingsStore = new FakeSettingsStore<AppSettings>(appSettings ?? new AppSettings(Language: "en-US"));
        ThemeSettingsStore = new FakeSettingsStore<ThemeSettings>(themeSettings ?? ThemeSettings.Default);
        FontSettingsStore = new FakeSettingsStore<FontSettings>(fontSettings ?? FontSettings.Default);
        FontFamilyCatalog = new AvaloniaFontFamilyCatalog(["ChapterTool UI Test", "ChapterTool Mono Test"]);
        FontApplicationService = new AvaloniaFontApplicationService(FontFamilyCatalog);
        ShellService = shellService ?? new FakeShellService();
        logService = new ApplicationLogPanelProvider();
        ViewModel = new MainWindowViewModel(
            LoadService,
            SaveService,
            new ChapterEditingService(new ChapterTimeFormatter()),
            new ChapterSegmentService(),
            WindowService,
            new ChapterTimeFormatter(),
            logService,
            TestApplicationLogger.Create<MainWindowViewModel>(logService),
            ShellService,
            AppSettingsStore,
            frameRateService: null,
            Localizer);
        Window = new MainWindow(ViewModel, _ => FilePickerService);
    }

    public MainWindow Window { get; }

    public MainWindowViewModel ViewModel { get; }

    public AppLocalizationManager Localizer { get; }

    public FakeLoadService LoadService { get; }

    public FakeSaveService SaveService { get; }

    public FakeWindowService WindowService { get; }

    public FakeFilePickerService FilePickerService { get; }

    public FakeSettingsPickerService SettingsPickerService { get; }

    public FakeSettingsStore<AppSettings> AppSettingsStore { get; }

    public FakeSettingsStore<ThemeSettings> ThemeSettingsStore { get; }

    public FakeSettingsStore<FontSettings> FontSettingsStore { get; }

    public IFontFamilyCatalog FontFamilyCatalog { get; }

    public IFontApplicationService FontApplicationService { get; }

    public IShellService ShellService { get; }

    public IApplicationLogService LogService => logService;

    public static ChapterImportResult ImportResult(string path, params ChapterImportEntry[] entries) =>
        new(true, [new ChapterImportSource(path, entries)], []);

    public static ChapterImportResult ImportResult(string path, int defaultEntryIndex, params ChapterImportEntry[] entries) =>
        new(true, [new ChapterImportSource(path, entries, defaultEntryIndex)], []);

    public static ChapterImportEntry Entry(ChapterImportFormat sourceType,  string sourceName, params string[] chapterNames)
    {
        var chapters = chapterNames.Length == 0
            ? [new Chapter(1, TimeSpan.Zero, "Intro")]
            : chapterNames
                .Select((name, index) => new Chapter(index + 1, TimeSpan.FromSeconds(index * 10), name))
                .ToArray();
        var duration = chapters.Length == 0 ? TimeSpan.Zero : chapters[^1].StartTime;
        var info = new ChapterSet(sourceName, sourceName, sourceType, 24, duration, chapters);
        return new ChapterImportEntry(sourceName, sourceName, info);
    }

    public static ChapterImportEntry OptionWithMedia(ChapterImportFormat sourceType,  string sourceName, ReferencedMediaFile referencedMedia, params string[] chapterNames)
    {
        var entry = Entry(sourceType, sourceName, chapterNames);
        return entry with { ReferencedMediaFiles = [referencedMedia] };
    }

    public async ValueTask LoadAsync(string path)
    {
        await LayoutAsync();
        FilePickerService.SourcePath = path;
        await Window.BrowseAndLoadCommand.ExecuteAsync();
        await LayoutAsync();
    }

    public async ValueTask LayoutAsync(double width = 736, double height = 576)
    {
        Window.Show();
        Window.Width = width;
        Window.Height = height;
        await ExecuteLayoutAsync(Window);
    }

    public static async ValueTask ExecuteLayoutAsync(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var layoutManager = window.GetLayoutManager()
            ?? throw new InvalidOperationException("Window layout manager was not available.");
        layoutManager.ExecuteInitialLayoutPass();
        layoutManager.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
    }

    public static async ValueTask<Window> RenderToolAsync(Control view, object viewModel, double width = 760, double height = 520)
    {
        var window = new Window
        {
            Content = view,
            DataContext = viewModel,
            Width = width,
            Height = height
        };
        window.Show();
        await ExecuteLayoutAsync(window);
        return window;
    }

    public T RequiredControl<T>(string name)
        where T : Control =>
        Window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    public static T RequiredDescendant<T>(Control scope, Func<T, bool> predicate, string description)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().FirstOrDefault(predicate)
        ?? throw new InvalidOperationException($"Expected descendant control was not found: {description}.");

    public static IReadOnlyList<T> Descendants<T>(Control scope)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().ToArray();

    public bool ContainsRenderedText(string text) =>
        ContainsRenderedText(Window, text);

    public bool ContainsRenderedText(Control scope, string text) =>
        RenderedTexts(scope).Any(rendered => string.Equals(rendered, text, StringComparison.Ordinal));

    public static bool ContainsRenderedTextStatic(Control scope, string text) =>
        RenderedTextsStatic(scope).Any(rendered => string.Equals(rendered, text, StringComparison.Ordinal));

    public static IReadOnlyList<string> RenderedTexts(Control scope) => RenderedTextsStatic(scope);

    public static IReadOnlyList<string> RenderedTextsStatic(Control scope) =>
        scope
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(static block => block.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray()!;

    public string DescribeRenderedTexts(Control scope) =>
        RenderedTexts(scope)
            .Aggregate(string.Empty, static (current, text) => string.IsNullOrEmpty(current) ? text : current + Environment.NewLine + text);

    public async ValueTask FocusAndPressAsync(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Window.Focus();
        Window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = Window
        });
        await LayoutAsync(Window.Width, Window.Height);
    }

    public IReadOnlyList<MenuItem> OpenContextMenu(Control control)
    {
        var menu = control.ContextMenu ?? throw new InvalidOperationException($"Control '{control.Name}' does not have a context menu.");
        menu.PlacementTarget = control;
        menu.Open();
        Dispatcher.UIThread.RunJobs();
        return menu.Items.OfType<MenuItem>().ToArray();
    }

    public MenuItem RequiredMenuItem(Control control, string name) =>
        OpenContextMenu(control).FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Menu item '{name}' was not found on '{control.Name}'.");

    public void SelectRows(params int[] indexes)
    {
        var grid = RequiredControl<DataGrid>("ChapterGrid");
        grid.SelectedItems.Clear();
        foreach (var index in indexes)
        {
            grid.SelectedItems.Add(ViewModel.Rows[index]);
        }

        ViewModel.UpdateSelectedRows(indexes.ToHashSet());
    }

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

    public void Dispose() => Window.Close();

    internal sealed class FakeLoadService(IReadOnlyList<ChapterImportResult> results) : IChapterLoadService
    {
        private readonly Queue<ChapterImportResult> results = new(results);

        public List<string> Paths { get; } = [];

        public ValueTask<ChapterImportResult> LoadAsync(string path, CancellationToken cancellationToken)
        {
            Paths.Add(path);
            if (results.Count == 0)
            {
                throw new InvalidOperationException("FakeLoadService has no more results queued.");
            }

            var result = results.Count == 1 ? results.Peek() : results.Dequeue();
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class FakeSaveService : IChapterSaveService
    {
        public ChapterSet? LastInfo { get; private set; }
        public ChapterExportOptions? LastOptions { get; private set; }
        public string? LastDirectory { get; private set; }
        public int Calls { get; private set; }

        public ValueTask<ChapterExportResult> SaveAsync(
            ChapterSet info,
            ChapterExportOptions options,
            string? directory,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastInfo = info;
            LastOptions = options;
            LastDirectory = directory;
            return ValueTask.FromResult(new ChapterExportResult(true, "ok", ".txt", []));
        }
    }

    internal sealed class FakeWindowService : IWindowService
    {
        public List<string> Opened { get; } = [];
        public List<object?> Parameters { get; } = [];

        public ValueTask ShowAsync(string windowId, object? parameter, CancellationToken cancellationToken)
        {
            Opened.Add(windowId);
            Parameters.Add(parameter);
            return ValueTask.CompletedTask;
        }

        public ValueTask HideAsync(string windowId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    internal sealed class FakeFilePickerService : IFilePickerService
    {
        public string? SourcePath { get; set; }
        public string? MplsPath { get; set; }
        public string? ChapterNameTemplatePath { get; set; }
        public string? SaveDirectoryPath { get; set; }
        public string? LuaExpressionScriptPath { get; set; }

        public int SourcePickCount { get; private set; }
        public int SaveDirectoryPickCount { get; private set; }

        public ValueTask<string?> PickSourceAsync(CancellationToken cancellationToken)
        {
            SourcePickCount++;
            return ValueTask.FromResult(SourcePath);
        }

        public ValueTask<string?> PickMplsAsync(CancellationToken cancellationToken) => ValueTask.FromResult(MplsPath);

        public ValueTask<string?> PickChapterNameTemplateAsync(CancellationToken cancellationToken) => ValueTask.FromResult(ChapterNameTemplatePath);

        public ValueTask<string?> PickLuaExpressionScriptAsync(CancellationToken cancellationToken) => ValueTask.FromResult(LuaExpressionScriptPath);

        public ValueTask<string?> PickSaveDirectoryAsync(CancellationToken cancellationToken)
        {
            SaveDirectoryPickCount++;
            return ValueTask.FromResult(SaveDirectoryPath);
        }
    }

    internal sealed class FakeSettingsPickerService : ISettingsPickerService
    {
        public string? DirectoryPath { get; set; }
        public string? ExecutablePath { get; set; }

        public ValueTask<string?> PickDirectoryAsync(string title, CancellationToken cancellationToken) => ValueTask.FromResult(DirectoryPath);

        public ValueTask<string?> PickExecutableAsync(string title, CancellationToken cancellationToken) => ValueTask.FromResult(ExecutablePath);
    }

    internal sealed class FakeSettingsStore<TSettings>(TSettings current) : ISettingsStore<TSettings>
    {
        public TSettings Current { get; private set; } = current;
        public int Saves { get; private set; }

        public ValueTask<TSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

        public ValueTask SaveAsync(TSettings settings, CancellationToken cancellationToken)
        {
            Saves++;
            Current = settings;
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class FakeShellService : IShellService
    {
        public List<string> Opened { get; } = [];
        public List<string> RevealedInFolder { get; } = [];
        public List<string> TerminalsOpened { get; } = [];

        public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
        {
            Opened.Add(target);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevealInFolderAsync(string filePath, CancellationToken cancellationToken)
        {
            RevealedInFolder.Add(filePath);
            return ValueTask.CompletedTask;
        }

        public ValueTask OpenTerminalAsync(string directoryPath, CancellationToken cancellationToken)
        {
            TerminalsOpened.Add(directoryPath);
            return ValueTask.CompletedTask;
        }
    }
}

internal enum UiTestSize
{
    Default,
    Wide,
    Narrow
}
