using System.Text.Json;
using System.Text.RegularExpressions;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class AppSettingsStore(string settingsDirectory, IReadOnlyList<string>? legacyDirectories = null)
    : ISettingsStore<AppSettings>
{
    private const string CurrentFileName = "appsettings.json";
    private const string LegacyFileName = "chaptertool.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IReadOnlyList<string> legacyDirectories = legacyDirectories ?? [settingsDirectory];
    private readonly Lock syncRoot = new();
    private AppSettings? cachedSettings;
    private SettingsFileState cachedFileState;

    public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        var fileState = GetFileState(currentPath, legacyDirectories);

        lock (syncRoot)
        {
            if (cachedSettings is not null && cachedFileState == fileState)
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
                settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                    ?? new AppSettings();
                Cache(settings, fileState);
                return settings;
            }
            catch (JsonException)
            {
                settings = new AppSettings();
                Cache(settings, fileState);
                return settings;
            }
        }

        foreach (var legacyDirectory in legacyDirectories)
        {
            var legacyPath = Path.Combine(legacyDirectory, LegacyFileName);
            if (!File.Exists(legacyPath))
            {
                continue;
            }

            var migrated = await TryLoadLegacyAsync(legacyPath, cancellationToken);
            if (migrated is not null)
            {
                Cache(migrated, fileState);
                return migrated;
            }
        }

        settings = new AppSettings();
        Cache(settings, fileState);
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
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, currentPath, overwrite: true);
            Cache(settings, GetFileState(currentPath, legacyDirectories));
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

    private static async ValueTask<AppSettings?> TryLoadLegacyAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var values = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
            if (values is null)
            {
                return null;
            }

            return new AppSettings(
                SavingPath: Get(values, @"Software\ChapterTool.SavingPath"),
                Language: Get(values, @"Software\ChapterTool.Language") ?? Get(values, "Language") ?? "",
                MainWindowLocation: ParseLocation(
                    Get(values, @"Software\ChapterTool.Location")
                    ?? Get(values, @"Software\ChapterTool.location")),
                MkvToolnixPath: Get(values, @"Software\ChapterTool.mkvToolnixPath") ?? Get(values, "mkvToolnixPath"),
                Eac3toPath: Get(values, @"Software\ChapterTool.eac3toPath") ?? Get(values, "eac3toPath"));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key);

    private void Cache(AppSettings settings, SettingsFileState fileState)
    {
        lock (syncRoot)
        {
            cachedSettings = settings;
            cachedFileState = fileState;
        }
    }

    private static SettingsFileState GetFileState(string currentPath, IReadOnlyList<string> legacyDirectories)
    {
        var current = GetFileStamp(currentPath);
        FileStamp legacy = default;
        foreach (var legacyDirectory in legacyDirectories)
        {
            legacy = GetFileStamp(Path.Combine(legacyDirectory, LegacyFileName));
            if (legacy.Exists)
            {
                break;
            }
        }

        return new SettingsFileState(current, legacy);
    }

    private static FileStamp GetFileStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? new FileStamp(true, file.LastWriteTimeUtc, file.Length)
            : default;
    }

    private readonly record struct SettingsFileState(FileStamp Current, FileStamp Legacy);

    private readonly record struct FileStamp(bool Exists, DateTime LastWriteTimeUtc, long Length);

    private static WindowLocation? ParseLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = LocationRegex().Match(value);
        if (!match.Success)
        {
            return null;
        }

        return new WindowLocation(
            int.Parse(match.Groups["x"].Value),
            int.Parse(match.Groups["y"].Value));
    }

    [GeneratedRegex(@"\{X=(?<x>-?\d+),Y=(?<y>-?\d+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex LocationRegex();
}
