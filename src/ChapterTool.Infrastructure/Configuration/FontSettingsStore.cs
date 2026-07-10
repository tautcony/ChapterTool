using System.Text.Json;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class FontSettingsStore(string settingsDirectory) : ISettingsStore<FontSettings>
{
    private const string CurrentFileName = "font-settings.json";

    private readonly Lock syncRoot = new();
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private FontSettings? cachedSettings;
    private FileStamp cachedFileStamp;

    public async ValueTask<FontSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        using var corruptLoadScope = CorruptSettingsFile.EnterLoad(currentPath);
        var stamp = GetFileStamp(currentPath);

        lock (syncRoot)
        {
            if (cachedSettings is not null && cachedFileStamp == stamp)
            {
                return cachedSettings;
            }
        }

        if (File.Exists(currentPath))
        {
            try
            {
                await using var stream = File.OpenRead(currentPath);
                var settings = FontSettings.Normalize(
                    await JsonSerializer.DeserializeAsync(stream, AppJsonSerializerContext.Default.FontSettings, cancellationToken));
                Cache(settings, stamp);
                return settings;
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

        Cache(FontSettings.Default, stamp);
        return FontSettings.Default;
    }

    public async ValueTask SaveAsync(FontSettings settings, CancellationToken cancellationToken)
    {
        await saveLock.WaitAsync(cancellationToken);
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(settingsDirectory);
            var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
            tempPath = $"{currentPath}.{Guid.NewGuid():N}.tmp";
            var normalized = FontSettings.Normalize(settings);

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, AppJsonSerializerContext.Default.FontSettings, cancellationToken);
            }

            File.Move(tempPath, currentPath, overwrite: true);
            Cache(normalized, GetFileStamp(currentPath));
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

    private void Cache(FontSettings settings, FileStamp stamp)
    {
        lock (syncRoot)
        {
            cachedSettings = settings;
            cachedFileStamp = stamp;
        }
    }

    private static FileStamp GetFileStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? new FileStamp(true, file.LastWriteTimeUtc, file.Length)
            : default;
    }

    private readonly record struct FileStamp(bool Exists, DateTime LastWriteTimeUtc, long Length);
}
