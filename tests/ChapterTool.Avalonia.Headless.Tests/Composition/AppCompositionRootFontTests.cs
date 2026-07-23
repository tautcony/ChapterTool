using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Headless.Tests.Headless;
using ChapterTool.Avalonia.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Headless.Tests.Composition;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AppCompositionRootFontTests
{
    [AvaloniaFact]
    public async Task Startup_defaults_exist_and_persisted_fonts_replace_them_asynchronously()
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia application was not initialized.");
        Assert.IsType<FontFamily>(application.Resources[AvaloniaFontApplicationService.UiFontFamilyKey]);
        Assert.IsType<FontFamily>(application.Resources[AvaloniaFontApplicationService.MonospaceFontFamilyKey]);

        var families = FontManager.Current.SystemFonts
            .Select(static family => family.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        Assert.NotEmpty(families);
        var ui = families[0];
        var mono = families.Length > 1 ? families[1] : families[0];
        var root = CreateTempDirectory();
        await new ChapterToolSettingsStore(root).SaveAsync(
            new ChapterToolSettings { Font = new FontSettings(ui, mono) },
            TestContext.Current.CancellationToken);

        using var composition = new AppCompositionRoot(settingsDirectory: root);
        await composition.AppearanceSettingsInitialization;

        Assert.Equal(ui, ResourceFont(AvaloniaFontApplicationService.UiFontFamilyKey));
        Assert.Equal(mono, ResourceFont(AvaloniaFontApplicationService.MonospaceFontFamilyKey));
    }

    [AvaloniaFact]
    public async Task Corrupt_font_settings_keep_defaults_and_main_window_usable()
    {
        var root = CreateTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, ChapterToolSettingsStore.FileName), "{");

        using var composition = new AppCompositionRoot(settingsDirectory: root);
        var window = composition.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();

            Assert.NotNull(window.Content);
            Assert.Equal(FontFamily.Default.Name, ResourceFont(AvaloniaFontApplicationService.UiFontFamilyKey));
            Assert.Equal("Menlo", ResourceFont(AvaloniaFontApplicationService.MonospaceFontFamilyKey));
            Assert.EndsWith(
                "monospace",
                AvaloniaFontApplicationService.DefaultMonospaceFontFamily,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            window.Close();
        }
    }

    private static string ResourceFont(string key) =>
        Assert.IsType<FontFamily>(Application.Current!.Resources[key]).Name;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
