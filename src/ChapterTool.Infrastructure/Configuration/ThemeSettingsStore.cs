using System.Text.Json;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class ThemeSettingsStore(string settingsDirectory) : ISettingsStore<ThemeColorSettings>
{
    private const string CurrentFileName = "theme-colors.json";

    public async ValueTask<ThemeColorSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        if (File.Exists(currentPath))
        {
            try
            {
                await using var stream = File.OpenRead(currentPath);
                return await JsonSerializer.DeserializeAsync(stream, AppJsonSerializerContext.Default.ThemeColorSettings, cancellationToken)
                    ?? ThemeColorSettings.Default;
            }
            catch (JsonException exception)
            {
                throw CorruptSettingsFile.Preserve(currentPath, exception);
            }
        }

        return ThemeColorSettings.Default;
    }

    public async ValueTask SaveAsync(ThemeColorSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settingsDirectory);
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        var tempPath = currentPath + ".tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, AppJsonSerializerContext.Default.ThemeColorSettings, cancellationToken);
            }

            File.Move(tempPath, currentPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
