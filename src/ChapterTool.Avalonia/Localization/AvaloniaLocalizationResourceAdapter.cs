using Avalonia;
using ChapterTool.Localization;

namespace ChapterTool.Avalonia.Localization;

/// <summary>Applies shared localization values to Avalonia application resources.</summary>
public sealed class AvaloniaLocalizationResourceAdapter : IDisposable
{
    private readonly IAppLocalizer localizer;
    private readonly HashSet<string> appliedKeys = new(StringComparer.Ordinal);

    public AvaloniaLocalizationResourceAdapter(IAppLocalizer localizer)
    {
        this.localizer = localizer;
        localizer.CultureChanged += OnCultureChanged;
        Apply();
    }

    public void Dispose() => localizer.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var current = AppLocalizationResources.All.TryGetValue(localizer.CurrentCultureName, out var values)
            ? values
            : AppLocalizationResources.Fallback;
        var activeKeys = new HashSet<string>(AppLocalizationResources.Fallback.Keys, StringComparer.Ordinal);
        activeKeys.UnionWith(current.Keys);

        foreach (var key in appliedKeys.Where(key => !activeKeys.Contains(key)).ToArray())
        {
            application.Resources.Remove(key);
        }

        foreach (var (key, value) in AppLocalizationResources.Fallback)
        {
            application.Resources[key] = value;
        }

        foreach (var (key, value) in current)
        {
            application.Resources[key] = value;
        }

        appliedKeys.Clear();
        appliedKeys.UnionWith(activeKeys);
    }
}
