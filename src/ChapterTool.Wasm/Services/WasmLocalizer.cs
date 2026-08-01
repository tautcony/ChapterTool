using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ChapterTool.Wasm.Services;

/// <summary>Web-only localizer backed by the Web host's JSON resource catalog.</summary>
public sealed class WasmLocalizer
{
    private static readonly Assembly ResourceAssembly = typeof(WasmLocalizer).Assembly;
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalog =
        LoadCatalog();

    public string Culture { get; private set; } = "en-US";

    public event Action? CultureChanged;

    public static IReadOnlyCollection<string> EnglishKeys =>
        [.. Catalog["en-US"].Keys];

    public void SetCulture(string? culture)
    {
        var normalized = Normalize(culture);
        if (string.Equals(Culture, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Culture = normalized;
        CultureChanged?.Invoke();
    }

    public string T(string key)
    {
        if (Catalog.TryGetValue(Culture, out var table) && table.TryGetValue(key, out var value))
        {
            return value;
        }

        return Catalog["en-US"].GetValueOrDefault(key, key);
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.GetCultureInfo(Culture), T(key), args);

    public IReadOnlyList<string> ChapterNameModes =>
    [
        T("NameMode.AsIs"),
        T("NameMode.Auto"),
        T("NameMode.Template")
    ];

    public IReadOnlyList<string> SettingsTabs =>
    [
        T("Settings.General"),
        T("Settings.Tools"),
        T("Settings.Output"),
        T("Settings.Appearance"),
        T("Settings.About")
    ];

    public static string Normalize(string? culture)
    {
        var normalized = culture?.Trim();
        if (string.Equals(normalized, "zh-CN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }

        return string.Equals(normalized, "ja-JP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ja", StringComparison.OrdinalIgnoreCase)
            ? "ja-JP"
            : "en-US";
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalog()
    {
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        {
            var suffix = $".Resources.Locales.{culture}.json";
            var resourceName = ResourceAssembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Web localization resource '{culture}' was not found.");
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Web localization resource '{culture}' could not be opened.");
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException($"Web localization resource '{culture}' is empty.");
            catalog[culture] = new Dictionary<string, string>(values, StringComparer.Ordinal);
        }

        return catalog;
    }
}
