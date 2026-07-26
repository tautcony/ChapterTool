using System.Globalization;

namespace ChapterTool.Avalonia.Localization;

public sealed class AppLocalizationManager : IAppLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources;

    public AppLocalizationManager(
        string? initialCultureName = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? resources = null)
    {
        this.resources = resources ?? AppLocalizationResources.All;
        CurrentCultureName = Normalize(initialCultureName);
        ApplyCulture(CurrentCultureName, raiseEvent: false);
    }

    public event EventHandler? CultureChanged;

    public IReadOnlyList<AppLanguage> SupportedLanguages => AppLanguage.Supported;

    public string CurrentCultureName { get; private set; }

    public void SetCulture(string? cultureName)
    {
        var normalized = Normalize(cultureName);
        if (string.Equals(CurrentCultureName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentCultureName = normalized;
        ApplyCulture(normalized, raiseEvent: true);
    }

    public string GetString(string key) => TryGetString(key, out var value) ? value : key;

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

    private static string Normalize(string? cultureName) => AppLanguage.Normalize(cultureName);

    private void ApplyCulture(string cultureName, bool raiseEvent)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (raiseEvent)
        {
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
