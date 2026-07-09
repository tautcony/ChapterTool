using System.Text.Json;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class ThemeSettingsStore(string settingsDirectory) : ISettingsStore<ThemeColorSettings>
{
    private const string CurrentFileName = "theme-colors.json";
    private readonly SemaphoreSlim saveLock = new(1, 1);

    public async ValueTask<ThemeColorSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        using var corruptLoadScope = CorruptSettingsFile.EnterLoad(currentPath);
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
            catch (FileNotFoundException exception) when (CorruptSettingsFile.TryGetConcurrentPreservation(currentPath, exception, out var corruptException))
            {
                throw corruptException;
            }
        }

        if (CorruptSettingsFile.TryGetConcurrentPreservation(
            currentPath,
            new FileNotFoundException("The settings file was preserved by another concurrent load.", currentPath),
            out var concurrentCorruptException))
        {
            throw concurrentCorruptException;
        }

        return ThemeColorSettings.Default;
    }

    public async ValueTask SaveAsync(ThemeColorSettings settings, CancellationToken cancellationToken)
    {
        await saveLock.WaitAsync(cancellationToken);
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(settingsDirectory);
            var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
            tempPath = $"{currentPath}.{Guid.NewGuid():N}.tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, AppJsonSerializerContext.Default.ThemeColorSettings, cancellationToken);
            }

            File.Move(tempPath, currentPath, overwrite: true);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            throw;
        }
        finally
        {
            saveLock.Release();
        }
    }
}
