using ChapterTool.Core.Services;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.Infrastructure.Tools;

public sealed class ExternalToolLocator(
    ISettingsStore<AppSettings> settingsStore,
    IReadOnlyList<string>? searchDirectories = null,
    IMkvToolNixInstallProbe? mkvToolNixInstallProbe = null)
    : IExternalToolLocator
{
    private readonly IMkvToolNixInstallProbe mkvToolNixInstallProbe = mkvToolNixInstallProbe ?? MkvToolNixInstallProbe.CreateDefault();

    public async ValueTask<ExternalToolLocation> LocateAsync(string toolId, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var configuredPath = GetConfiguredPath(toolId, settings);
        var executableName = ExternalToolPathResolver.ExecutableName(toolId);

        foreach (var candidate in ExternalToolPathResolver.ExpandConfiguredCandidates(configuredPath, executableName))
        {
            if (File.Exists(candidate))
            {
                return new ExternalToolLocation(true, candidate);
            }
        }

        foreach (var directory in searchDirectories ?? [])
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return new ExternalToolLocation(true, candidate);
            }
        }

        foreach (var candidate in ExternalToolPathResolver.DefaultCandidates(toolId, executableName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(candidate))
            {
                return new ExternalToolLocation(true, candidate);
            }
        }

        if (toolId.Equals("mkvextract", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidate in mkvToolNixInstallProbe.FindMkvExtractCandidates(executableName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(candidate))
                {
                    return new ExternalToolLocation(true, candidate);
                }
            }
        }

        return new ExternalToolLocation(
            false,
            null,
            "MissingDependency",
            $"External tool '{toolId}' was not found.");
    }

    private static string? GetConfiguredPath(string toolId, AppSettings settings) =>
        toolId.ToLowerInvariant() switch
        {
            "mkvextract" => settings.MkvToolnixPath,
            "eac3to" => settings.Eac3toPath,
            "ffprobe" => !string.IsNullOrWhiteSpace(settings.FfprobePath) ? settings.FfprobePath : settings.FfmpegPath,
            _ => null
        };

}
