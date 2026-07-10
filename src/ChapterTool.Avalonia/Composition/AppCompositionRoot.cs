using Avalonia.Controls;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing.Media;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ChapterTool.Avalonia.Composition;

public sealed class AppCompositionRoot : IDisposable
{
    private readonly string? startupPath;
    private readonly ChapterTimeFormatter formatter = new();
    private readonly IChapterExpressionEngine expressionEngine = new LuaExpressionScriptService();
    private readonly FrameRateService frameRateService = new();
    private readonly ApplicationLogPanelProvider logService = new(capacity: 500, minimumLevel: LogLevel.Information);
    private readonly AppLocalizationManager localizationManager = new();
    private readonly AppSettingsStore appSettingsStore;
    private readonly FontSettingsStore fontSettingsStore;
    private readonly ThemeSettingsStore themeSettingsStore;
    private readonly AvaloniaFontFamilyCatalog fontFamilyCatalog = new();
    private readonly AvaloniaFontApplicationService fontApplicationService;
    private readonly AvaloniaThemeApplicationService themeApplicationService = new();
    private readonly ILoggerFactory loggerFactory;
    private bool disposed;

    public AppCompositionRoot(string? startupPath = null, string? settingsDirectory = null)
    {
        this.startupPath = startupPath;
        var resolvedSettingsDirectory = settingsDirectory ?? SettingsDirectory();
        appSettingsStore = new AppSettingsStore(resolvedSettingsDirectory);
        fontSettingsStore = new FontSettingsStore(resolvedSettingsDirectory);
        themeSettingsStore = new ThemeSettingsStore(resolvedSettingsDirectory);
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
        _ = ApplyThemeSettingsAsync();
        _ = ApplyFontSettingsAsync();
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
            CreateShellService(),
            appSettingsStore,
            frameRateService,
            localizationManager,
            expressionEngine);

    public IApplicationLogService CreateApplicationLogService() => logService;

    public ILogger<T> CreateLogger<T>() => loggerFactory.CreateLogger<T>();

    public IChapterLoadService CreateChapterLoadService() => new RuntimeChapterLoadService(CreateChapterImporterRegistry());

    public IChapterImporterRegistry CreateChapterImporterRegistry() =>
        new RuntimeChapterImporterRegistry(
            formatter,
            CreateExternalToolLocator(),
            CreateProcessRunner(),
            CreateMediaChapterReader(),
            CreateMp4ChapterReader());

    public static IMediaChapterReader CreateMp4ChapterReader() => new AtlMp4ChapterReader();

    public FfprobeMediaChapterReader CreateMediaChapterReader() =>
        new(CreateExternalToolLocator(), CreateProcessRunner());

    public IChapterSaveService CreateChapterSaveService() =>
        new RuntimeChapterSaveService(new ChapterExportService(formatter, expressionEngine));

    public IChapterEditingService CreateChapterEditingService() => new ChapterEditingService(formatter);

    public static ChapterSegmentService CreateChapterSegmentService() => new();

    public IWindowService CreateWindowService() =>
        new AvaloniaWindowService(
            appSettingsStore,
            themeSettingsStore,
            themeApplicationService,
            localizationManager,
            owner => new AvaloniaSettingsPickerService(owner, localizationManager),
            CreateExternalToolLocator(),
            new AvaloniaSettingsCloseConfirmationService(localizationManager),
            shellService: CreateShellService(),
            fontSettingsStore: fontSettingsStore,
            fontFamilyCatalog: fontFamilyCatalog,
            fontApplicationService: fontApplicationService);

    public IAppLocalizer CreateLocalizer() => localizationManager;

    public static IShellService CreateShellService() => new ShellService();

    public IFilePickerService CreateFilePickerService(Window owner) => new AvaloniaFilePickerService(owner, localizationManager);

    public IExternalToolLocator CreateExternalToolLocator() =>
        new ExternalToolLocator(appSettingsStore, PathSearchDirectories().ToList());

    public static IProcessRunner CreateProcessRunner() => new ProcessRunner();

    public static INativeDependencyService CreateNativeDependencyService() =>
        new FileSystemNativeDependencyService(PathSearchDirectories().Prepend(AppContext.BaseDirectory).ToList());

    private async Task ApplyThemeSettingsAsync()
    {
        try
        {
            var settings = await themeSettingsStore.LoadAsync(CancellationToken.None);
            themeApplicationService.Apply(settings);
        }
        catch (IOException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
        }
        catch (UnauthorizedAccessException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
        }
        catch (CorruptSettingsFileException)
        {
            themeApplicationService.Apply(ThemeSettings.Default);
        }
    }

    private async Task ApplyFontSettingsAsync()
    {
        try
        {
            var settings = await fontSettingsStore.LoadAsync(CancellationToken.None);
            fontApplicationService.Apply(settings);
        }
        catch (IOException)
        {
            fontApplicationService.Apply(FontSettings.Default);
        }
        catch (UnauthorizedAccessException)
        {
            fontApplicationService.Apply(FontSettings.Default);
        }
        catch (CorruptSettingsFileException)
        {
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
