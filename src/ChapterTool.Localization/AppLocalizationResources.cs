using System.Reflection;
using System.Xml.Linq;

namespace ChapterTool.Localization;

public static class AppLocalizationResources
{
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Assembly ResourceAssembly = typeof(AppLocalizationResources).Assembly;

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } =
        AppLanguage.Supported.ToDictionary(
            static language => language.CultureName,
            static language => LoadCulture(language.CultureName, []),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Fallback { get; } = All[AppLanguage.DefaultCultureName];

    private static IReadOnlyDictionary<string, string> LoadCulture(string cultureName, HashSet<string> loading)
    {
        if (!loading.Add(cultureName))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var document = LoadDocument(cultureName);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var include in document.Descendants(XName.Get("ResourceInclude", AvaloniaNamespace)))
        {
            var includedCulture = CultureFromSource(include.Attribute("Source")?.Value);
            if (includedCulture is not null)
            {
                foreach (var (key, value) in LoadCulture(includedCulture, loading))
                {
                    values[key] = value;
                }
            }
        }

        foreach (var element in document.Descendants(XName.Get("String", XamlNamespace)))
        {
            var key = element.Attribute(XName.Get("Key", XamlNamespace))?.Value
                ?? element.Attribute("Key")?.Value;
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = element.Value;
            }
        }

        loading.Remove(cultureName);
        return values;
    }

    private static XDocument LoadDocument(string cultureName)
    {
        var suffix = $".Resources.Locales.{cultureName}.axaml";
        var resourceName = ResourceAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Localization resource '{cultureName}' was not found.");
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Localization resource '{cultureName}' could not be opened.");
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string? CultureFromSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(source);
        return AppLanguage.Supported.FirstOrDefault(language =>
            string.Equals(language.CultureName, fileName, StringComparison.OrdinalIgnoreCase))?.CultureName;
    }
}
