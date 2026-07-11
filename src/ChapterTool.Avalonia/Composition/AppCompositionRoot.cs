using Avalonia.Controls;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ChapterTool.Avalonia.Composition;

public sealed class AppCompositionRoot : IDisposable
{
    private readonly string? startupPath;
    private readonly string settingsDirectory;
    private readonly ChapterTimeFormatter formatter = new();
    private readonly IChapterExpressionEngine expressionEngine = new LuaExpressionScriptService();
    private readonly FrameRateService frameRateService = new();
    private readonly ApplicationLogPanelProvider logService = new(capacity: 500, minimumLevel: LogLevel.Information);
    private readonly AppLocalizationManager localizationManager = new();
    private readonly ChapterToolSettingsStore settingsStore;
    private readonly AvaloniaFontFamilyCatalog fontFamilyCatalog = new();
    private readonly AvaloniaFontApplicationService fontApplicationService;
    private readonly AvaloniaThemeApplicationService themeApplicationService = new();
    private readonly ILoggerFactory loggerFactory;
    private bool disposed;

    public AppCompositionRoot(string? startupPath = null, string? settingsDirectory = null)
    {
        this.startupPath = startupPath;
        var resolvedSettingsDirectory = settingsDirectory ?? SettingsDirectory();
        this.settingsDirectory = resolvedSettingsDirectory;
        settingsStore = new ChapterToolSettingsStore(resolvedSettingsDirectory);
        fontApplicationService = new AvaloniaFontApplicationService(fontFamilyCatalog);
        var serilogLogger = CreateSerilogLogger(resolvedSettingsDirectory);
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(serilogLogger, dispose: true);
            builder.AddProvider(logService);
        });

        // Settings are loaded asynchronously from MainWindow.Opened. Blocking here can deadlock
        // macOS single-file startup before Avalonia has shown the first window.
        themeApplicationService.Apply(ThemeSettings.Default);
        fontApplicationService.Apply(FontSettings.Default);
        _ = ApplyAppearanceSettingsAsync();
    }

    public MainWindow CreateMainWindow()
    {
        var viewModel = CreateMainWindowViewModel();
        return new MainWindow(viewModel, CreateFilePickerService, startupPath);
    }

    public MainWindowViewModel CreateMainWindowViewModel() =>
        new(
            CreateChapterLoadService(),
            CreateChapterSaveService(),
            CreateChapterEditingService(),
            CreateChapterSegmentService(),
            CreateWindowService(),
            formatter,
            logService,
            loggerFactory.CreateLogger<MainWindowViewModel>(),
            frameRateService,
            localizationManager,
            expressionEngine,
            CreateChapterExportService(),
            CreateShellService(),
            settingsStore);

    public IApplicationLogService CreateApplicationLogService() => logService;

    public ILogger<T> CreateLogger<T>() => loggerFactory.CreateLogger<T>();

    public IChapterLoadService CreateChapterLoadService() => new RuntimeChapterLoadService(CreateChapterImporterRegistry());

    public IChapterImporterRegistry CreateChapterImporterRegistry() =>
        CreateSharedImporterRegistry(settingsStore);

    /// <summary>
    /// Shared importer-registry factory used by GUI composition and CLI defaults.
    /// </summary>
    public static RuntimeChapterImporterRegistry CreateSharedImporterRegistry(
        ISettingsStore<ChapterToolSettings> settingsStore)
    {
        var sharedFormatter = new ChapterTimeFormatter();
        var toolLocator = new ExternalToolLocator(settingsStore, PathSearchDirectories().ToList());
        var processRunner = CreateProcessRunner();
        return new RuntimeChapterImporterRegistry(
            sharedFormatter,
            toolLocator,
            processRunner,
            new FfprobeMediaChapterReader(toolLocator, processRunner),
            CreateMp4ChapterReader());
    }

    public static IMediaChapterReader CreateMp4ChapterReader() => new AtlMp4ChapterReader();

    public FfprobeMediaChapterReader CreateMediaChapterReader() =>
        new(CreateExternalToolLocator(), CreateProcessRunner());

    public ChapterExportService CreateChapterExportService() =>
        CreateSharedExportService(expressionEngine);

    /// <summary>
    /// Shared export construction for GUI and CLI. CLI omits expression engine (product scope).
    /// </summary>
    public static ChapterExportService CreateSharedExportService(
        IChapterExpressionEngine? expressionEngine = null) =>
        new(new ChapterTimeFormatter(), expressionEngine);

    public IChapterSaveService CreateChapterSaveService() =>
        new RuntimeChapterSaveService(CreateChapterExportService());

    public IChapterEditingService CreateChapterEditingService() => new ChapterEditingService(formatter);

    public static ChapterSegmentService CreateChapterSegmentService() => new();

    public IWindowService CreateWindowService() =>
        new AvaloniaWindowService(
            localizationManager,
            settingsStore,
            themeApplicationService,
            owner => new AvaloniaSettingsPickerService(owner, localizationManager),
            CreateExternalToolLocator(),
            new AvaloniaSettingsCloseConfirmationService(localizationManager),
            shellService: CreateShellService(),
            fontFamilyCatalog: fontFamilyCatalog,
            fontApplicationService: fontApplicationService,
            settingsDirectory: settingsDirectory);

    public IAppLocalizer CreateLocalizer() => localizationManager;

    public static IShellService CreateShellService() => new ShellService();

    public IFilePickerService CreateFilePickerService(Window owner) => new AvaloniaFilePickerService(owner, localizationManager);

    public IExternalToolLocator CreateExternalToolLocator() =>
        new ExternalToolLocator(settingsStore, PathSearchDirectories().ToList());

    public static IProcessRunner CreateProcessRunner() => new ProcessRunner();

    public static INativeDependencyService CreateNativeDependencyService() =>
        new FileSystemNativeDependencyService(PathSearchDirectories().Prepend(AppContext.BaseDirectory).ToList());

    private async Task ApplyAppearanceSettingsAsync()
    {
        try
        {
            var settings = await settingsStore.LoadAsync(CancellationToken.None);
            themeApplicationService.Apply(settings.Theme);
            fontApplicationService.Apply(settings.Font);
        }
        catch (IOException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
            fontApplicationService.Apply(FontSettings.Default);
        }
        catch (UnauthorizedAccessException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
            fontApplicationService.Apply(FontSettings.Default);
        }
        catch (CorruptSettingsFileException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
            fontApplicationService.Apply(FontSettings.Default);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loggerFactory.Dispose();
    }

    private static Logger CreateSerilogLogger(string settingsDirectory)
    {
        var logDirectory = Path.Combine(settingsDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "chaptertool-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    private static string SettingsDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.CurrentDirectory, "settings")
            : Path.Combine(root, "ChapterTool");
    }

    private static IEnumerable<string> PathSearchDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var part in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    internal static IEnumerable<string> PathSearchDirectoriesForTests() => PathSearchDirectories();
}
