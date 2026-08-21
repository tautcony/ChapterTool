using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChapterTool.Core.Localization;

namespace ChapterTool.Wasm.Services;

/// <summary>Web-only localizer backed by the Web host's JSON resource catalog.</summary>
public sealed partial class WasmLocalizer
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

    public string Format(string key, params object[] args)
    {
        var format = T(key);
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        format = NamedFormatItemRegex().Replace(format, match =>
        {
            var name = match.Groups["name"].Value;
            if (!indexes.TryGetValue(name, out var index))
            {
                index = indexes.Count;
                indexes.Add(name, index);
            }

            return "{" + index + match.Groups["format"].Value + "}";
        });
        return string.Format(CultureInfo.GetCultureInfo(Culture), format, args);
    }

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
        T("Settings.OutputPreferences"),
        T("Settings.Editing"),
        T("Settings.Appearance"),
        T("Settings.About")
    ];

    public static string Normalize(string? culture) => UiLanguageCode.Normalize(culture);

    [GeneratedRegex("\\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<format>:[^}]*)?\\}")]
    private static partial Regex NamedFormatItemRegex();

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
