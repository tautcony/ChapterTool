using System.Globalization;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

public sealed class LocalizationService(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? resources = null)
    : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resources = resources ?? new Dictionary<string, IReadOnlyDictionary<string, string>>();

    public string CurrentLanguage { get; private set; } = "";

    public string GetString(string key)
    {
        if (resources.TryGetValue(CurrentLanguage, out var current) && current.TryGetValue(key, out var value))
        {
            return value;
        }

        if (resources.TryGetValue("", out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    public ValueTask SetLanguageAsync(string language, CancellationToken cancellationToken)
    {
        CurrentLanguage = language;
        CultureInfo.CurrentUICulture = string.IsNullOrWhiteSpace(language)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(language);
        return ValueTask.CompletedTask;
    }
}
