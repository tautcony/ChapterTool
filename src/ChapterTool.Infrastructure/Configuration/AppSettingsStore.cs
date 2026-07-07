using System.Text.Json;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class AppSettingsStore(string settingsDirectory) : ISettingsStore<AppSettings>
{
    private const string CurrentFileName = "appsettings.json";

    private readonly Lock syncRoot = new();
    private AppSettings? cachedSettings;
    private FileStamp cachedFileStamp;

    public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        var stamp = GetFileStamp(currentPath);

        lock (syncRoot)
        {
            if (cachedSettings is not null && cachedFileStamp == stamp)
            {
                return cachedSettings;
            }
        }

        AppSettings settings;
        if (File.Exists(currentPath))
        {
            try
            {
                await using var stream = File.OpenRead(currentPath);
                settings = await JsonSerializer.DeserializeAsync(stream, AppJsonSerializerContext.Default.AppSettings, cancellationToken)
                    ?? new AppSettings();
                Cache(settings, stamp);
                return settings;
            }
            catch (JsonException exception)
            {
                throw CorruptSettingsFile.Preserve(currentPath, exception);
            }
        }

        settings = new AppSettings();
        Cache(settings, stamp);
        return settings;
    }

    public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settingsDirectory);
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        var tempPath = currentPath + ".tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, AppJsonSerializerContext.Default.AppSettings, cancellationToken);
            }

            File.Move(tempPath, currentPath, overwrite: true);
            Cache(settings, GetFileStamp(currentPath));
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

    private void Cache(AppSettings settings, FileStamp stamp)
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
