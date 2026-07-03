namespace ChapterTool.Core.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    string GetString(string key);

    ValueTask SetLanguageAsync(string language, CancellationToken cancellationToken);
}
