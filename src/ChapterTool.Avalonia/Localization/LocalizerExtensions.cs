namespace ChapterTool.Avalonia.Localization;

public static class LocalizerExtensions
{
    public static string FormatPositional(this IAppLocalizer localizer, string key, params object?[] arguments)
    {
        var template = localizer.GetString(key);
        var names = template
            .Split('{', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Split('}', 2)[0])
            .Where(static part => part.Length > 0 && part.All(char.IsLetter))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var values = names
            .Select((name, index) => (Name: name, Value: index < arguments.Length ? arguments[index] : null))
            .ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal);
        return localizer.Format(key, values);
    }
}
