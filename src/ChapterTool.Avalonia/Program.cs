using Avalonia;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace ChapterTool.Avalonia;

internal static class Program
{
    internal static IReadOnlyList<string> StartupArgs { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        RegisterIconProviders();

        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = true })
            .LogToTrace();
    }

    public static void RegisterIconProviders()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
    }
}
