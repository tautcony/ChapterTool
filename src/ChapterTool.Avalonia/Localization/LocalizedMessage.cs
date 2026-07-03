namespace ChapterTool.Avalonia.Localization;

public sealed record LocalizedMessage(
    string Key,
    IReadOnlyDictionary<string, object?>? Arguments = null,
    string? TechnicalDetail = null)
{
    public static LocalizedMessage Create(string key, params (string Name, object? Value)[] arguments) =>
        new(key, arguments.ToDictionary(static item => item.Name, static item => item.Value, StringComparer.Ordinal));
}
