using System.Text.Json;
using System.Text.RegularExpressions;
using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed partial class ThemeSettingsStore(
    string settingsDirectory,
    IReadOnlyList<string>? legacyDirectories = null)
    : ISettingsStore<ThemeColorSettings>
{
    private const string CurrentFileName = "theme-colors.json";
    private const string LegacyFileName = "color-config.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IReadOnlyList<string> legacyDirectories = legacyDirectories ?? [settingsDirectory];

    public async ValueTask<ThemeColorSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var currentPath = Path.Combine(settingsDirectory, CurrentFileName);
        if (File.Exists(currentPath))
        {
            try
            {
                await using var stream = File.OpenRead(currentPath);
                return await JsonSerializer.DeserializeAsync<ThemeColorSettings>(stream, JsonOptions, cancellationToken)
                    ?? ThemeColorSettings.Default;
            }
            catch (JsonException exception)
            {
                throw CorruptSettingsFile.Preserve(currentPath, exception);
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

    private static async ValueTask<ThemeColorSettings?> TryLoadLegacyAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var matches = HexStringRegex().Matches(json);
            if (matches.Count < 6)
            {
                return null;
            }

            var defaults = ThemeColorSettings.Default.OrderedSlots.Select(static slot => slot.Value).ToList();
            var colors = new string[6];
            for (var index = 0; index < colors.Length; index++)
            {
                var candidate = matches[index].Groups["hex"].Value;
                colors[index] = IsHexColor(candidate) ? candidate.ToUpperInvariant() : defaults[index];
            }

            return new ThemeColorSettings(colors[0], colors[1], colors[2], colors[3], colors[4], colors[5]);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsHexColor(string value) => StrictHexColorRegex().IsMatch(value);

    [GeneratedRegex("\"(?<hex>#[0-9A-Fa-f]{6}|[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex HexStringRegex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex StrictHexColorRegex();
}
