using System.Text.Json;
using System.Text.RegularExpressions;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class AppSettingsStore : ISettingsStore<AppSettings>
{
    private const string CurrentFileName = "appsettings.json";
    private const string LegacyFileName = "chaptertool.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string settingsDirectory;
    private readonly IReadOnlyList<string> legacyDirectories;

    public AppSettingsStore(string settingsDirectory, IReadOnlyList<string>? legacyDirectories = null)
    {
        this.settingsDirectory = settingsDirectory;
        this.legacyDirectories = legacyDirectories ?? [settingsDirectory];
    }

    public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        if (File.Exists(currentPath))
        {
            try
            {
                await using var stream = File.OpenRead(currentPath);
                return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                    ?? new AppSettings();
            }
            catch (JsonException)
            {
                return new AppSettings();
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
                return migrated;
            }
        }

        return new AppSettings();
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
