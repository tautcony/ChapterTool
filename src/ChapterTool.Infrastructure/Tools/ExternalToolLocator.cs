using ChapterTool.Core.Diagnostics;
using ChapterTool.Infrastructure.Services;
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
            if (IsExecutableCandidate(candidate, executableName))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        foreach (var directory in searchDirectories ?? [])
        {
            var candidate = Path.Combine(directory, executableName);
            if (IsExecutableCandidate(candidate, executableName))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        foreach (var candidate in defaultCandidateProvider.FindCandidates(toolId, executableName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExecutableCandidate(candidate, executableName))
            {
                return Cache(cacheKey, new ExternalToolLocation(true, candidate));
            }
        }

        if (toolId.Equals("mkvextract", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidate in mkvToolNixInstallProbe.FindMkvExtractCandidates(executableName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsExecutableCandidate(candidate, executableName))
                {
                    return Cache(cacheKey, new ExternalToolLocation(true, candidate));
                }
            }
        }

        return Cache(cacheKey, new ExternalToolLocation(
            false,
            null,
            ChapterDiagnosticCode.MissingDependency,
            $"External tool '{toolId}' was not found."));
    }

    private static string? GetConfiguredPath(string toolId, AppSettings settings) =>
        toolId.ToLowerInvariant() switch
        {
            "mkvextract" => settings.MkvToolnixPath,
            "eac3to" => settings.Eac3toPath,
            "ffprobe" => !string.IsNullOrWhiteSpace(settings.FfprobePath)
                ? settings.FfprobePath
                : Directory.Exists(settings.FfmpegPath) ? settings.FfmpegPath : null,
            _ => null
        };

    private static bool IsExecutableCandidate(string path, string executableName)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.GetFileName(path).Equals(executableName, StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & executeBits) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return true;
        }
    }

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
                var executableName = ExternalToolPathResolver.ExecutableName(key.ToolId);
                if (!string.IsNullOrWhiteSpace(cached.Location.Path) && IsExecutableCandidate(cached.Location.Path, executableName))
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
