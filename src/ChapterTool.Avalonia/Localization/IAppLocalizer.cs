namespace ChapterTool.Avalonia.Localization;

public interface IAppLocalizer
{
    event EventHandler? CultureChanged;

    IReadOnlyList<AppLanguage> SupportedLanguages { get; }

    string CurrentCultureName { get; }

    void SetCulture(string? cultureName);

    string GetString(string key);

    bool TryGetString(string key, out string value);

    string Format(string key, IReadOnlyDictionary<string, object?>? arguments = null);

    string Format(LocalizedMessage message);
}
