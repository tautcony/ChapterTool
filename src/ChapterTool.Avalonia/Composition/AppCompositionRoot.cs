using Avalonia.Controls;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Services;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ChapterTool.Avalonia.Composition;

public sealed class AppCompositionRoot : IDisposable
{
    private readonly string? startupPath;
    private readonly ChapterTimeFormatter formatter = new();
    private readonly ExpressionService expressionService = new();
    private readonly FrameRateService frameRateService = new();
    private readonly ApplicationLogPanelProvider logService = new(capacity: 500, minimumLevel: LogLevel.Information);
    private readonly AppLocalizationManager localizationManager = new();
    private readonly AppSettingsStore appSettingsStore;
    private readonly ThemeSettingsStore themeSettingsStore;
    private readonly ILoggerFactory loggerFactory;
    private readonly Serilog.ILogger serilogLogger;
    private bool disposed;

    public AppCompositionRoot(string? startupPath = null, string? settingsDirectory = null)
    {
        this.startupPath = startupPath;
        var resolvedSettingsDirectory = settingsDirectory ?? SettingsDirectory();
        appSettingsStore = new AppSettingsStore(resolvedSettingsDirectory);
        themeSettingsStore = new ThemeSettingsStore(resolvedSettingsDirectory);
        serilogLogger = CreateSerilogLogger(resolvedSettingsDirectory);
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(serilogLogger, dispose: true);
            builder.AddProvider(logService);
        });

        var settings = appSettingsStore.LoadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        localizationManager.SetCulture(settings.Language);
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
            localizationManager);

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

    public AtlMp4ChapterReader CreateMp4ChapterReader() => new();

    public FfprobeMediaChapterReader CreateMediaChapterReader() =>
        new(CreateExternalToolLocator(), CreateProcessRunner());

    public IChapterSaveService CreateChapterSaveService() =>
        new RuntimeChapterSaveService(new ChapterExportService(formatter, expressionService));

    public IChapterEditingService CreateChapterEditingService() => new ChapterEditingService(formatter);

    public ChapterSegmentService CreateChapterSegmentService() => new();

    public IWindowService CreateWindowService() =>
        new AvaloniaWindowService(
            appSettingsStore,
            themeSettingsStore,
            localizationManager,
            owner => new AvaloniaSettingsPickerService(owner),
            CreateExternalToolLocator());

    public IAppLocalizer CreateLocalizer() => localizationManager;

    public IShellService CreateShellService() => new ShellService();

    public IFilePickerService CreateFilePickerService(Window owner) => new AvaloniaFilePickerService(owner);

    public IExternalToolLocator CreateExternalToolLocator() =>
        new ExternalToolLocator(appSettingsStore, PathSearchDirectories().ToArray());

    public IProcessRunner CreateProcessRunner() => new ProcessRunner();

    public INativeDependencyService CreateNativeDependencyService() =>
        new FileSystemNativeDependencyService(PathSearchDirectories().Prepend(AppContext.BaseDirectory).ToArray());

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loggerFactory.Dispose();
    }

    private static Serilog.ILogger CreateSerilogLogger(string settingsDirectory)
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
}
