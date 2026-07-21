using Avalonia;
using ChapterTool.CommandLine;
using ChapterTool.Avalonia.Diagnostics;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace ChapterTool.Avalonia;

internal static class Program
{
    internal static string? GuiStartupPath { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var launchPlan = ChapterToolCliHost.AnalyzeDesktopLaunch(args);
        GuiStartupPath = launchPlan.GuiStartupPath;
        if (launchPlan.LaunchGui)
        {
            SetupSentry();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }

        if (launchPlan.RunCli)
        {
            try
            {
                var exitCode = ChapterToolCliHost.Run(args);
                Environment.ExitCode = exitCode;
                return;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Unhandled CLI exception: {exception.Message}");
                Environment.ExitCode = 2;
                return;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        RegisterIconProviders();

        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = true })
            .LogToTrace();
    }

    private static void RegisterIconProviders()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
    }

    private static void SetupSentry()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var startupOptions = SentryStartupConfiguration.FromEnvironment(
            Environment.GetEnvironmentVariable,
            typeof(Program).Assembly,
            localApplicationData,
#if DEBUG
            debugBuild: true
#else
            debugBuild: false
#endif
        );

        if (!startupOptions.Enabled || string.IsNullOrWhiteSpace(startupOptions.Dsn))
        {
            return;
        }

        SentrySdk.Init(options => ApplySentryOptions(options, startupOptions));
    }

    private static void ApplySentryOptions(SentryOptions options, SentryStartupOptions startupOptions)
    {
        options.Dsn = startupOptions.Dsn;
        options.Environment = startupOptions.Environment;
        options.Release = startupOptions.Release;
        options.Distribution = startupOptions.Distribution;
        options.Debug = startupOptions.Debug;
        options.DiagnosticLevel = startupOptions.DiagnosticLevel;
        options.SendDefaultPii = startupOptions.SendDefaultPii;
        options.TracesSampleRate = startupOptions.TracesSampleRate;
        options.ProfilesSampleRate = startupOptions.ProfilesSampleRate;
        options.CacheDirectoryPath = startupOptions.CacheDirectoryPath;
        options.AutoSessionTracking = true;
        options.IsGlobalModeEnabled = true;
        options.AttachStacktrace = true;
        options.MaxBreadcrumbs = 100;
        options.SendClientReports = true;
        options.InitCacheFlushTimeout = TimeSpan.Zero;
    }
}
