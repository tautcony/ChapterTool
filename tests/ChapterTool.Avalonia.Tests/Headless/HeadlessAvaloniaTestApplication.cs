using Avalonia;
using Avalonia.Headless;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

[assembly: AvaloniaTestApplication(typeof(ChapterTool.Avalonia.Tests.Headless.HeadlessAvaloniaTestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ChapterTool.Avalonia.Tests.Headless;

public static class HeadlessAvaloniaTestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        return AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .LogToTrace();
    }
}
