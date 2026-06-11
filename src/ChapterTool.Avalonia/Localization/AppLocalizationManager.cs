using System.Globalization;
using Avalonia;

namespace ChapterTool.Avalonia.Localization;

public sealed class AppLocalizationManager : IAppLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources;
    private readonly HashSet<string> appliedKeys = new(StringComparer.Ordinal);

    public AppLocalizationManager(
        string? initialCultureName = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? resources = null)
    {
        this.resources = resources ?? AppLocalizationResources.All;
        CurrentCultureName = AppLanguage.Normalize(initialCultureName);
        ApplyCulture(CurrentCultureName, raiseEvent: false);
    }

    public event EventHandler? CultureChanged;

    public IReadOnlyList<AppLanguage> SupportedLanguages => AppLanguage.Supported;

    public string CurrentCultureName { get; private set; }

    public void SetCulture(string? cultureName)
    {
        var normalized = AppLanguage.Normalize(cultureName);
        if (string.Equals(CurrentCultureName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentCultureName = normalized;
        ApplyCulture(normalized, raiseEvent: true);
    }

    public string GetString(string key) =>
        TryGetString(key, out var value) ? value : key;

    public bool TryGetString(string key, out string value)
    {
        if (resources.TryGetValue(CurrentCultureName, out var current) && current.TryGetValue(key, out value!))
        {
            return true;
        }

        if (AppLocalizationResources.Fallback.TryGetValue(key, out value!))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string Format(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var format = GetString(key);
        if (arguments is null || arguments.Count == 0)
        {
            return format;
        }

        foreach (var (name, value) in arguments)
        {
            format = format.Replace(
                "{" + name + "}",
                Convert.ToString(value, CultureInfo.CurrentUICulture) ?? string.Empty,
                StringComparison.Ordinal);
        }

        return format;
    }

    public string Format(LocalizedMessage message) => Format(message.Key, message.Arguments);

    private void ApplyCulture(string cultureName, bool raiseEvent)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        ApplyApplicationResources(cultureName);
        if (raiseEvent)
        {
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyApplicationResources(string cultureName)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        if (!resources.TryGetValue(cultureName, out var current))
        {
            current = AppLocalizationResources.Fallback;
        }

        foreach (var key in appliedKeys)
        {
            application.Resources.Remove(key);
        }

        appliedKeys.Clear();

        foreach (var (key, value) in AppLocalizationResources.Fallback)
        {
            application.Resources[key] = value;
            appliedKeys.Add(key);
        }

        foreach (var (key, value) in current)
        {
            application.Resources[key] = value;
            appliedKeys.Add(key);
        }
    }
}
