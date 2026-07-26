using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ChapterTool.CommandLine.Cli;

public interface ICliLocalizer
{
    string CurrentCultureName { get; }

    void SetCulture(string? cultureName);

    string GetString(string key);

    bool TryGetString(string key, out string value);

    string Format(string key, IReadOnlyDictionary<string, object?>? arguments = null);
}

public sealed class CliLocalizationManager : ICliLocalizer
{
    public const string DefaultCultureName = "en-US";

    public static IReadOnlyList<string> SupportedCultureNames { get; } = ["en-US", "zh-CN", "ja-JP"];

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources;

    public CliLocalizationManager(
        string? initialCultureName = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? resources = null)
    {
        this.resources = resources ?? CliLocalizationResources.All;
        CurrentCultureName = Normalize(initialCultureName);
        ApplyCulture(CurrentCultureName);
    }

    public string CurrentCultureName { get; private set; }

    public void SetCulture(string? cultureName)
    {
        var normalized = Normalize(cultureName);
        if (string.Equals(CurrentCultureName, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentCultureName = normalized;
        ApplyCulture(normalized);
    }

    public string GetString(string key) => TryGetString(key, out var value) ? value : key;

    public bool TryGetString(string key, out string value)
    {
        if (resources.TryGetValue(CurrentCultureName, out var current) && current.TryGetValue(key, out value!))
        {
            return true;
        }

        if (CliLocalizationResources.Fallback.TryGetValue(key, out value!))
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

    private static string Normalize(string? cultureName) =>
        SupportedCultureNames.FirstOrDefault(culture =>
            string.Equals(culture, cultureName?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? DefaultCultureName;

    private static void ApplyCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}

internal static class CliLocalizationResources
{
    private static readonly Assembly ResourceAssembly = typeof(CliLocalizationResources).Assembly;

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } =
        CliLocalizationManager.SupportedCultureNames.ToDictionary(
            static culture => culture,
            static culture => LoadCulture(culture),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Fallback => All[CliLocalizationManager.DefaultCultureName];

    private static IReadOnlyDictionary<string, string> LoadCulture(string cultureName)
    {
        var suffix = $".Resources.Locales.{cultureName}.json";
        var resourceName = ResourceAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"CLI localization resource '{cultureName}' was not found.");

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"CLI localization resource '{cultureName}' could not be opened.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }
}
