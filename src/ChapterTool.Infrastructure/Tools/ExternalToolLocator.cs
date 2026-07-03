using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tools;

public sealed class ExternalToolLocator(
    ISettingsStore<AppSettings> settingsStore,
    IReadOnlyList<string>? searchDirectories = null,
    IMkvToolNixInstallProbe? mkvToolNixInstallProbe = null,
    IExternalToolDefaultCandidateProvider? defaultCandidateProvider = null)
    : IExternalToolLocator
{
    private static readonly TimeSpan MissingResultCacheDuration = TimeSpan.FromSeconds(2);

    private readonly IMkvToolNixInstallProbe mkvToolNixInstallProbe = mkvToolNixInstallProbe ?? MkvToolNixInstallProbe.CreateDefault();
    private readonly IExternalToolDefaultCandidateProvider defaultCandidateProvider =
        defaultCandidateProvider ?? ExternalToolDefaultCandidateProvider.Instance;
    private readonly Lock cacheSyncRoot = new();
    private readonly Dictionary<ToolCacheKey, CachedToolLocation> locationCache = [];

    public async ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = await settingsStore.LoadAsync(cancellationToken);
        var configuredPath = GetConfiguredPath(toolId, settings);
        var cacheKey = new ToolCacheKey(toolId.ToLowerInvariant(), configuredPath);
        if (TryGetCachedLocation(cacheKey) is { } cached)
        {
            return cached;
        }

        var executableName = ExternalToolPathResolver.ExecutableName(toolId);

        foreach (var candidate in ExternalToolPathResolver.ExpandConfiguredCandidates(configuredPath, executableName))
        {
            if (File.Exists(candidate))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        foreach (var directory in searchDirectories ?? [])
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        foreach (var candidate in defaultCandidateProvider.FindCandidates(toolId, executableName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(candidate))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        if (toolId.Equals("mkvextract", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidate in mkvToolNixInstallProbe.FindMkvExtractCandidates(executableName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(candidate))
                {
                    return Cache(cacheKey, new ExternalToolLocation(true, candidate));
                }
            }
        }

        return Cache(cacheKey, new ExternalToolLocation(
            false,
            null,
            "MissingDependency",
            $"External tool '{toolId}' was not found."));
    }

    private static string? GetConfiguredPath(string toolId, AppSettings settings) =>
        toolId.ToLowerInvariant() switch
        {
            "mkvextract" => settings.MkvToolnixPath,
            "eac3to" => settings.Eac3toPath,
            "ffprobe" => !string.IsNullOrWhiteSpace(settings.FfprobePath) ? settings.FfprobePath : settings.FfmpegPath,
            _ => null
        };

    private ExternalToolLocation? TryGetCachedLocation(ToolCacheKey key)
    {
        lock (cacheSyncRoot)
        {
            if (!locationCache.TryGetValue(key, out var cached))
            {
                return null;
            }

            if (cached.Location.Found)
            {
                if (!string.IsNullOrWhiteSpace(cached.Location.Path) && File.Exists(cached.Location.Path))
                {
                    return cached.Location;
                }

                locationCache.Remove(key);
                return null;
            }

            if (DateTime.UtcNow - cached.CachedAtUtc <= MissingResultCacheDuration)
            {
                return cached.Location;
            }

            locationCache.Remove(key);
            return null;
        }
    }

    private ExternalToolLocation Cache(ToolCacheKey key, ExternalToolLocation location)
    {
        lock (cacheSyncRoot)
        {
            locationCache[key] = new CachedToolLocation(location, DateTime.UtcNow);
        }

        return location;
    }

    private readonly record struct ToolCacheKey(string ToolId, string? ConfiguredPath);

    private readonly record struct CachedToolLocation(ExternalToolLocation Location, DateTime CachedAtUtc);
}
