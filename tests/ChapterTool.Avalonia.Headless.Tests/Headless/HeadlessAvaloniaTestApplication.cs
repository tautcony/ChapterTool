using Avalonia;
using Avalonia.Headless;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

[assembly: AvaloniaTestApplication(typeof(ChapterTool.Avalonia.Headless.Tests.Headless.HeadlessAvaloniaTestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaHeadlessTestCollection
{
    public const string Name = "Avalonia headless tests";
}

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
