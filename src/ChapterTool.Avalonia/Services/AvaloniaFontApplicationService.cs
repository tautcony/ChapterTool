using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Avalonia.Services;

public sealed class AvaloniaFontApplicationService(IFontFamilyCatalog fontFamilyCatalog) : IFontApplicationService
{
    public const string UiFontFamilyKey = "ChapterTool.UiFontFamily";
    public const string MonospaceFontFamilyKey = "ChapterTool.MonospaceFontFamily";
    public const string DefaultMonospaceFontFamily = "Menlo, Consolas, monospace";

    public FontSettings Resolve(FontSettings settings) => FontSettingsResolver.Resolve(settings, fontFamilyCatalog);

    public void Apply(FontSettings settings)
    {
        var application = Application.Current;
        if (application?.Resources is null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Apply(settings));
            return;
        }

        var resolved = Resolve(settings);
        application.Resources[UiFontFamilyKey] = string.IsNullOrEmpty(resolved.UiFontFamily)
            ? FontFamily.Default
            : FontFamily.Parse(resolved.UiFontFamily);
        application.Resources[MonospaceFontFamilyKey] = FontFamily.Parse(
            string.IsNullOrEmpty(resolved.MonospaceFontFamily)
                ? DefaultMonospaceFontFamily
                : resolved.MonospaceFontFamily);
    }
}
