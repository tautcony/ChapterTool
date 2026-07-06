using Avalonia;
using ChapterTool.Avalonia.Cli;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace ChapterTool.Avalonia;

internal static class Program
{
    internal static string? GuiStartupPath { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var launchPlan = ChapterToolCliSupport.AnalyzeLaunch(args);
        GuiStartupPath = launchPlan.GuiStartupPath;
        if (launchPlan.LaunchGui)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }

        if (launchPlan.CliResult is not null)
        {
            try
            {
                var exitCode = launchPlan.CliResult.Run();
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
}
