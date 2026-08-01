using Avalonia.Controls;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views;
using ChapterTool.Avalonia.Views;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Importing.Runtime;
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
    private readonly IExpressionAuthoringService expressionAuthoringService;
    private readonly ChapterExportService exportService;
    private readonly IProcessRunner processRunner;
    private readonly FrameRateService frameRateService = new();
    private readonly ApplicationLogPanelProvider logService = new(capacity: 500, minimumLevel: LogLevel.Information);
    private readonly AppLocalizationManager localizationManager = new();
    private readonly AvaloniaLocalizationResourceAdapter localizationResourceAdapter;
    private readonly AvaloniaFontFamilyCatalog fontFamilyCatalog = new();
    private readonly AvaloniaFontApplicationService fontApplicationService;
    private readonly AvaloniaThemeApplicationService themeApplicationService = new();
    private readonly IToolCatalog toolCatalog;
    private readonly ILoggerFactory loggerFactory;
    private AvaloniaWindowService? windowService;
    private bool disposed;

    public RuntimeCapabilities Capabilities { get; } = new(
        RuntimeSourceMode.LocalPath,
        RuntimeOutputMode.Directory,
        RuntimeSecondarySurfaceMode.NativeWindow,
        CanReadClipboard: true,
        CanWriteClipboard: true,
        CanConfigureExternalTools: true,
        CanRunExternalProcesses: true,
        CanOpenLocalPaths: true);

    public AppCompositionRoot(string? startupPath = null, string? settingsDirectory = null)
        : this(startupPath, settingsDirectory, expressionAuthoringServiceOverride: null)
    {
    }

    internal AppCompositionRoot(
        string? startupPath,
        string? settingsDirectory,
        IExpressionAuthoringService? expressionAuthoringServiceOverride)
    {
        this.startupPath = startupPath;
        var resolvedSettingsDirectory = settingsDirectory ?? SettingsDirectory();
        this.settingsDirectory = resolvedSettingsDirectory;
        toolCatalog = StandardToolCatalogFactory.Create();
        localizationResourceAdapter = new AvaloniaLocalizationResourceAdapter(localizationManager);
        SettingsStore = new ChapterToolSettingsStore(resolvedSettingsDirectory);
        expressionAuthoringService = expressionAuthoringServiceOverride ?? new ExpressionAuthoringService(ExpressionEngine);
        exportService = new ChapterExportService(formatter, ExpressionEngine);
        ExternalToolLocator = new ExternalToolLocator(SettingsStore, PathSearchDirectories().ToList());
        processRunner = CreateProcessRunner();
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
        AppearanceSettingsInitialization = ApplyAppearanceSettingsAsync();
    }

    internal Task AppearanceSettingsInitialization { get; }

    public MainWindow CreateMainWindow()
    {
        var viewModel = CreateMainWindowViewModel();
        var mainView = new MainView(
            viewModel,
            control => CreateFilePickerService(TopLevel.GetTopLevel(control) as Window
                ?? throw new InvalidOperationException("The shared main view must be attached to a desktop window.")),
            new NoContentEmbeddedToolPresenter());
        var title = $"{localizationManager.GetString("App.Title")} v{typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";
        var mainWindow = new MainWindow(mainView, title);
        _ = mainView.InitializeAsync(startupPath);
        return mainWindow;
    }

    public MainWindowViewModel CreateMainWindowViewModel() => new(CreateHostComposition());

    public AvaloniaHostComposition CreateHostComposition() => CreateHostDependencies().Compose();

    private AvaloniaHostDependencies CreateHostDependencies() =>
        new(
            new WorkspaceHostServices(
                CreateChapterLoadService(),
                CreateChapterSaveService(),
                CreateChapterEditingService(),
                CreateChapterSegmentService(),
                formatter,
                frameRateService,
                ExpressionEngine,
                CreateChapterExportService(),
                expressionAuthoringService),
            new HostEffectServices(
                logService,
                loggerFactory.CreateLogger<MainWindowViewModel>(),
                CreateShellService()),
            new SettingsAppearanceServices(
                SettingsStore,
                themeApplicationService,
                fontFamilyCatalog,
                fontApplicationService,
                ExternalToolLocator,
                settingsDirectory),
            new LocalizationServices(localizationManager),
            new RuntimeHostServices(Capabilities),
            new AuxiliaryToolHostServices(CreateAuxiliaryToolHost(), new NoContentEmbeddedToolPresenter()));

    public IApplicationLogService CreateApplicationLogService() => logService;

    public ILogger<T> CreateLogger<T>() => loggerFactory.CreateLogger<T>();

    public IChapterLoadService CreateChapterLoadService() => new RuntimeChapterLoadService(CreateChapterImporterRegistry());

    public IChapterImporterRegistry CreateChapterImporterRegistry() =>
        ChapterToolRuntimeComposition.CreateImporterRegistry(
            SettingsStore,
            formatter,
            ExternalToolLocator,
            processRunner,
            new FfprobeMediaChapterReader(ExternalToolLocator, processRunner),
            new AtlMp4ChapterReader());

    public FfprobeMediaChapterReader CreateMediaChapterReader() =>
        new(ExternalToolLocator, processRunner);

    public ChapterExportService CreateChapterExportService() => exportService;

    public IChapterSaveService CreateChapterSaveService() =>
        new RuntimeChapterSaveService(CreateChapterExportService());

    public IChapterEditingService CreateChapterEditingService() => new ChapterEditingService(formatter);

    public static ChapterSegmentService CreateChapterSegmentService() => new();

    public AvaloniaWindowService CreateWindowService() =>
        windowService ??= new AvaloniaWindowService(
            localizationManager,
            SettingsStore,
            themeApplicationService,
            owner => new AvaloniaSettingsPickerService(owner, localizationManager),
            ExternalToolLocator,
            new AvaloniaSettingsCloseConfirmationService(localizationManager),
            shellService: CreateShellService(),
            fontFamilyCatalog: fontFamilyCatalog,
            fontApplicationService: fontApplicationService,
            settingsDirectory: settingsDirectory,
            expressionAuthoringService: expressionAuthoringService,
            clipboardServiceFactory: owner => new AvaloniaClipboardService(owner),
            toolCatalog: toolCatalog);

    public IAuxiliaryToolHost CreateAuxiliaryToolHost() =>
        windowService ?? (AvaloniaWindowService)CreateWindowService();

    public IToolCatalog CreateToolCatalog() => toolCatalog;

    public IAppLocalizer CreateLocalizer() => localizationManager;

    public IExpressionAuthoringService CreateExpressionAuthoringService() => expressionAuthoringService;

    internal IChapterTimeFormatter Formatter => formatter;

    private IChapterExpressionEngine ExpressionEngine { get; } = new LuaExpressionScriptService();

    internal ChapterToolSettingsStore SettingsStore { get; }

    internal IExternalToolLocator ExternalToolLocator { get; }

    public static IShellService CreateShellService() => new ShellService();

    public IFilePickerService CreateFilePickerService(Window owner) => new AvaloniaFilePickerService(owner, localizationManager);

    public IExternalToolLocator CreateExternalToolLocator() => ExternalToolLocator;

    public static IProcessRunner CreateProcessRunner() => new ProcessRunner();

    public static INativeDependencyService CreateNativeDependencyService() =>
        new FileSystemNativeDependencyService(PathSearchDirectories().Prepend(AppContext.BaseDirectory).ToList());

    private async Task ApplyAppearanceSettingsAsync()
    {
        try
        {
            var settings = await SettingsStore.LoadAsync(CancellationToken.None);
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
        windowService?.Dispose();
        localizationResourceAdapter.Dispose();
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
        return ChapterToolRuntimeComposition.ResolveSettingsDirectory();
    }

    private static IEnumerable<string> PathSearchDirectories()
    {
        return ChapterToolRuntimeComposition.PathSearchDirectories();
    }

    internal static IEnumerable<string> PathSearchDirectoriesForTests() => PathSearchDirectories();
}
