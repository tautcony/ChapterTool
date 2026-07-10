using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Infrastructure.Configuration;

public sealed class ChapterToolSettingsStore(string settingsDirectory) : ISettingsStore<ChapterToolSettings>
{
    public const string FileName = "settings.json";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private readonly string settingsPath = Path.Combine(settingsDirectory, FileName);
    private ChapterToolSettings? cachedSettings;
    private FileStamp cachedFileStamp;

    public async ValueTask<ChapterToolSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var pathLock = PathLock();
        await pathLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadCoreAsync(persistMigrations: true, cancellationToken);
        }
        finally
        {
            pathLock.Release();
        }
    }

    public async ValueTask SaveAsync(ChapterToolSettings settings, CancellationToken cancellationToken)
    {
        var pathLock = PathLock();
        await pathLock.WaitAsync(cancellationToken);
        try
        {
            if (GetFileStamp(settingsPath).Exists)
            {
                _ = await LoadCoreAsync(persistMigrations: false, cancellationToken);
            }

            await WriteAsync(ChapterToolSettings.Normalize(settings), cancellationToken);
        }
        finally
        {
            pathLock.Release();
        }
    }

    public async ValueTask UpdateAsync(
        Func<ChapterToolSettings, ChapterToolSettings> update,
        CancellationToken cancellationToken)
    {
        var pathLock = PathLock();
        await pathLock.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadCoreAsync(persistMigrations: false, cancellationToken);
            await WriteAsync(ChapterToolSettings.Normalize(update(current)), cancellationToken);
        }
        finally
        {
            pathLock.Release();
        }
    }

    private async ValueTask<ChapterToolSettings> LoadCoreAsync(
        bool persistMigrations,
        CancellationToken cancellationToken)
    {
        var stamp = GetFileStamp(settingsPath);
        if (cachedSettings is not null && cachedFileStamp == stamp)
        {
            return cachedSettings;
        }

        if (stamp.Exists)
        {
            return await LoadActiveAsync(stamp, persistMigrations, cancellationToken);
        }

        return ChapterToolSettings.Default;
    }

    private async ValueTask<ChapterToolSettings> LoadActiveAsync(
        FileStamp stamp,
        bool persistMigrations,
        CancellationToken cancellationToken)
    {
        using var corruptLoadScope = CorruptSettingsFile.EnterLoad(settingsPath);

        try
        {
            await using var stream = File.OpenRead(settingsPath);
            var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            if (node is not JsonObject root)
            {
                throw new JsonException("The settings document root must be an object.");
            }

            var upgraded = Upgrade(root);
            var settings = upgraded.Root.Deserialize(AppJsonSerializerContext.Default.ChapterToolSettings)
                ?? throw new JsonException("The settings document could not be deserialized.");
            var normalized = ChapterToolSettings.Normalize(settings);

            if (upgraded.WasUpgraded && persistMigrations)
            {
                await WriteAsync(normalized, cancellationToken);
            }
            else if (!upgraded.WasUpgraded)
            {
                Cache(normalized, stamp);
            }

            return normalized;
        }
        catch (UnsupportedSettingsVersionException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw CorruptSettingsFile.Preserve(settingsPath, exception);
        }
        catch (FileNotFoundException exception) when (CorruptSettingsFile.TryGetConcurrentPreservation(settingsPath, exception, out var corruptException))
        {
            throw corruptException;
        }
    }

    private async ValueTask WriteAsync(ChapterToolSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settingsDirectory);
        var tempPath = $"{settingsPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    ChapterToolSettings.Normalize(settings),
                    AppJsonSerializerContext.Default.ChapterToolSettings,
                    cancellationToken);
            }

            File.Move(tempPath, settingsPath, overwrite: true);
            Cache(settings, GetFileStamp(settingsPath));
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private UpgradeResult Upgrade(JsonObject root)
    {
        var version = ReadVersion(root);
        if (version > ChapterToolSettings.CurrentSchemaVersion)
        {
            throw new UnsupportedSettingsVersionException(
                settingsPath,
                version,
                ChapterToolSettings.CurrentSchemaVersion);
        }

        var wasUpgraded = false;
        while (version < ChapterToolSettings.CurrentSchemaVersion)
        {
            root = version switch
            {
                0 => UpgradeVersionZero(root),
                _ => throw new JsonException($"No settings upgrade path exists from schema version {version}."),
            };
            version++;
            wasUpgraded = true;
        }

        return new UpgradeResult(root, wasUpgraded);
    }

    private static int ReadVersion(JsonObject root)
    {
        if (!root.TryGetPropertyValue("schemaVersion", out var versionNode))
        {
            if (root.ContainsKey("application") || root.ContainsKey("theme") || root.ContainsKey("font"))
            {
                return 0;
            }

            throw new JsonException("The unversioned settings document does not contain a recognized section.");
        }

        if (versionNode is not JsonValue versionValue
            || !versionValue.TryGetValue<int>(out var version)
            || version < 0)
        {
            throw new JsonException("The settings schema version must be a non-negative integer.");
        }

        return version;
    }

    private static JsonObject UpgradeVersionZero(JsonObject root)
    {
        root["schemaVersion"] = 1;
        return root;
    }

    private SemaphoreSlim PathLock() => PathLocks.GetOrAdd(Path.GetFullPath(settingsPath), static _ => new SemaphoreSlim(1, 1));

    private void Cache(ChapterToolSettings settings, FileStamp stamp)
    {
        cachedSettings = settings;
        cachedFileStamp = stamp;
    }

    private static FileStamp GetFileStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? new FileStamp(true, file.LastWriteTimeUtc, file.Length)
            : default;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct UpgradeResult(JsonObject Root, bool WasUpgraded);

    private readonly record struct FileStamp(bool Exists, DateTime LastWriteTimeUtc, long Length);
}
