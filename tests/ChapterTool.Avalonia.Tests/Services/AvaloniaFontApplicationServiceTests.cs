using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Tests.Headless;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Tests.Services;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AvaloniaFontApplicationServiceTests
{
    [AvaloniaFact]
    public void Apply_writes_independent_semantic_font_resources_and_replaces_previous_values()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaFontApplicationService(
                new AvaloniaFontFamilyCatalog(["Ui Family", "Mono Family", "Second UI"]));

            try
            {
                service.Apply(new FontSettings("Ui Family", "Mono Family"));
                Assert.Equal("Ui Family", ResourceFamily(application, AvaloniaFontApplicationService.UiFontFamilyKey));
                Assert.Equal("Mono Family", ResourceFamily(application, AvaloniaFontApplicationService.MonospaceFontFamilyKey));

                service.Apply(new FontSettings("Second UI", "Mono Family"));
                Assert.Equal("Second UI", ResourceFamily(application, AvaloniaFontApplicationService.UiFontFamilyKey));
                Assert.Equal("Mono Family", ResourceFamily(application, AvaloniaFontApplicationService.MonospaceFontFamilyKey));
            }
            finally
            {
                new AvaloniaFontApplicationService(new AvaloniaFontFamilyCatalog([])).Apply(FontSettings.Default);
            }
        });
    }

    [AvaloniaFact]
    public void Apply_falls_back_unavailable_categories_independently()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Avalonia application was not initialized.");
            var service = new AvaloniaFontApplicationService(new AvaloniaFontFamilyCatalog(["Available UI", "Available Mono"]));

            try
            {
                service.Apply(new FontSettings("Available UI", "Missing Mono"));
                Assert.Equal("Available UI", ResourceFamily(application, AvaloniaFontApplicationService.UiFontFamilyKey));
                Assert.Equal("Menlo", ResourceFamily(application, AvaloniaFontApplicationService.MonospaceFontFamilyKey));
                Assert.EndsWith(
                    "monospace",
                    AvaloniaFontApplicationService.DefaultMonospaceFontFamily,
                    StringComparison.OrdinalIgnoreCase);

                service.Apply(new FontSettings("Missing UI", "Available Mono"));
                Assert.Equal(FontFamily.Default.Name, ResourceFamily(application, AvaloniaFontApplicationService.UiFontFamilyKey));
                Assert.Equal("Available Mono", ResourceFamily(application, AvaloniaFontApplicationService.MonospaceFontFamilyKey));
            }
            finally
            {
                new AvaloniaFontApplicationService(new AvaloniaFontFamilyCatalog([])).Apply(FontSettings.Default);
            }
        });
    }

    private static string ResourceFamily(Application application, string key) =>
        Assert.IsType<FontFamily>(application.Resources[key]).Name;
}
