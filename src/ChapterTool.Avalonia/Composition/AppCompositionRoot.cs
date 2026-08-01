using Autofac;
using Avalonia.Controls;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views;
using ChapterTool.Avalonia.Views;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace ChapterTool.Avalonia.Composition;

/// <summary>Owns the desktop Autofac container and its application lifetime.</summary>
public sealed class AppCompositionRoot : IDisposable
{
    private static long latestCompositionGeneration;
    private readonly AppCompositionOptions options;
    private readonly string settingsDirectory;
    private readonly string? startupPath;
    private readonly long compositionGeneration;
    private bool mainWindowInitialized;
    private bool disposed;

    public AppCompositionRoot(string? startupPath = null, string? settingsDirectory = null)
        : this(new AppCompositionOptions
        {
            StartupPath = startupPath,
            SettingsDirectory = settingsDirectory
        })
    {
    }

    public AppCompositionRoot(AppCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        compositionGeneration = Interlocked.Increment(ref latestCompositionGeneration);
        this.startupPath = options.StartupPath;
        settingsDirectory = options.SettingsDirectory ?? ChapterToolRuntimeComposition.ResolveSettingsDirectory();
        this.options = options with { SettingsDirectory = settingsDirectory };

        var builder = new ContainerBuilder();
        if (this.options.RegisterProductionModules)
        {
            builder.RegisterModule(new LoggingModule(settingsDirectory));
            builder.RegisterModule(new InfrastructureModule(settingsDirectory));
            builder.RegisterModule(new WorkspaceModule(this.options));
            builder.RegisterModule(new AvaloniaPlatformModule(this.options));
            builder.RegisterModule(new AuxiliaryToolsModule(settingsDirectory));
            builder.RegisterModule(new ApplicationShellModule(this.options));
        }
        this.options.ConfigureOverrides?.Invoke(builder);

        LifetimeScope = builder.Build();
        if (this.options.RegisterProductionModules)
        {
            _ = LifetimeScope.Resolve<AvaloniaLocalizationResourceAdapter>();
            LifetimeScope.Resolve<IThemeApplicationService>().Apply(ThemeSettings.Default);
            LifetimeScope.Resolve<IFontApplicationService>().Apply(FontSettings.Default);
            AppearanceSettingsInitialization = ApplyAppearanceSettingsAsync();
        }
        else
        {
            AppearanceSettingsInitialization = Task.CompletedTask;
        }
    }

    internal AppCompositionRoot(
        string? startupPath,
        string? settingsDirectory,
        IExpressionAuthoringService? expressionAuthoringServiceOverride)
        : this(new AppCompositionOptions
        {
            StartupPath = startupPath,
            SettingsDirectory = settingsDirectory,
            ExpressionAuthoringService = expressionAuthoringServiceOverride
        })
    {
    }

    /// <summary>Gets the application lifetime scope owned by this root.</summary>
    public ILifetimeScope LifetimeScope { get; }

    public IRuntimeCapabilities Capabilities => LifetimeScope.Resolve<IRuntimeCapabilities>();

    internal Task AppearanceSettingsInitialization { get; }

    internal ChapterToolSettingsStore SettingsStore => LifetimeScope.Resolve<ChapterToolSettingsStore>();

    internal IExternalToolLocator ExternalToolLocator => LifetimeScope.Resolve<IExternalToolLocator>();

    internal IChapterTimeFormatter Formatter => LifetimeScope.Resolve<IChapterTimeFormatter>();

    /// <summary>Resolves the shell graph without showing a window or starting initialization.</summary>
    public MainWindow ResolveMainWindow()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return LifetimeScope.Resolve<MainWindow>();
    }

    /// <summary>Resolves the shell and starts its normal asynchronous initialization.</summary>
    public MainWindow CreateMainWindow()
    {
        var mainWindow = ResolveMainWindow();
        if (!mainWindowInitialized && mainWindow.Content is MainView mainView)
        {
            mainWindowInitialized = true;
            _ = mainView.InitializeAsync(startupPath);
        }

        return mainWindow;
    }

    /// <summary>Validates the production root graph before user interaction.</summary>
    public void ValidateProductionComposition()
    {
        _ = ResolveMainWindow();
        _ = LifetimeScope.Resolve<MainWindowViewModel>();
        _ = LifetimeScope.Resolve<IToolCatalog>();
        _ = LifetimeScope.Resolve<IAuxiliaryToolHost>();
        _ = LifetimeScope.Resolve<IChapterImporterRegistry>();
    }

    public MainWindowViewModel CreateMainWindowViewModel() => LifetimeScope.Resolve<MainWindowViewModel>();

    public AvaloniaHostComposition CreateHostComposition() => LifetimeScope.Resolve<AvaloniaHostComposition>();

    public IApplicationLogService CreateApplicationLogService() => LifetimeScope.Resolve<IApplicationLogService>();

    public ILogger<T> CreateLogger<T>() => LifetimeScope.Resolve<ILoggerFactory>().CreateLogger<T>();

    public IChapterLoadService CreateChapterLoadService() => LifetimeScope.Resolve<IChapterLoadService>();

    public IChapterImporterRegistry CreateChapterImporterRegistry() => LifetimeScope.Resolve<IChapterImporterRegistry>();

    public FfprobeMediaChapterReader CreateMediaChapterReader() => LifetimeScope.Resolve<FfprobeMediaChapterReader>();

    public Core.Exporting.ChapterExportService CreateChapterExportService() =>
        LifetimeScope.Resolve<Core.Exporting.ChapterExportService>();

    public IChapterSaveService CreateChapterSaveService() => LifetimeScope.Resolve<IChapterSaveService>();

    public IChapterEditingService CreateChapterEditingService() => LifetimeScope.Resolve<IChapterEditingService>();

    public static ChapterSegmentService CreateChapterSegmentService() => new();

    public AvaloniaWindowService CreateWindowService() => LifetimeScope.Resolve<AvaloniaWindowService>();

    public IAuxiliaryToolHost CreateAuxiliaryToolHost() => LifetimeScope.Resolve<IAuxiliaryToolHost>();

    public IToolCatalog CreateToolCatalog() => LifetimeScope.Resolve<IToolCatalog>();

    public IAppLocalizer CreateLocalizer() => LifetimeScope.Resolve<IAppLocalizer>();

    public IExpressionAuthoringService CreateExpressionAuthoringService() =>
        LifetimeScope.Resolve<IExpressionAuthoringService>();

    public IFilePickerService CreateFilePickerService(Window owner) =>
        LifetimeScope.Resolve<Func<Window, IFilePickerService>>()(owner);

    public IExternalToolLocator CreateExternalToolLocator() => LifetimeScope.Resolve<IExternalToolLocator>();

    public static IProcessRunner CreateProcessRunner() => new ProcessRunner();

    public static INativeDependencyService CreateNativeDependencyService() =>
        new FileSystemNativeDependencyService(PathSearchDirectories().Prepend(AppContext.BaseDirectory).ToList());

    internal static IEnumerable<string> PathSearchDirectoriesForTests() => ChapterToolRuntimeComposition.PathSearchDirectories();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        LifetimeScope.Dispose();
    }

    private async Task ApplyAppearanceSettingsAsync()
    {
        try
        {
            var settings = await SettingsStore.LoadAsync(CancellationToken.None);
            if (compositionGeneration != Volatile.Read(ref latestCompositionGeneration))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LifetimeScope.Resolve<IThemeApplicationService>().Apply(settings.Theme);
                LifetimeScope.Resolve<IFontApplicationService>().Apply(settings.Font);
            });
        }
        catch (IOException)
        {
            ApplyAppearanceDefaults();
        }
        catch (UnauthorizedAccessException)
        {
            ApplyAppearanceDefaults();
        }
        catch (CorruptSettingsFileException)
        {
            ApplyAppearanceDefaults();
        }
    }

    private void ApplyAppearanceDefaults()
    {
        LifetimeScope.Resolve<IThemeApplicationService>().Apply(ThemeSettings.Default);
        LifetimeScope.Resolve<IFontApplicationService>().Apply(FontSettings.Default);
    }

    private static IEnumerable<string> PathSearchDirectories() => ChapterToolRuntimeComposition.PathSearchDirectories();
}
