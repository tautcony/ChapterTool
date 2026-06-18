namespace ChapterTool.Infrastructure.Tools;

public static class ExternalToolPathResolver
{
    public static string ExecutableName(string toolId) =>
        OperatingSystem.IsWindows() && !toolId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? $"{toolId}.exe"
            : toolId;

    public static IEnumerable<string> ExpandConfiguredCandidates(string? configuredPath, string executableName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            yield break;
        }

        if (Directory.Exists(configuredPath))
        {
            yield return Path.Combine(configuredPath, executableName);
            yield break;
        }

        yield return configuredPath;
    }

    public static IEnumerable<string> DefaultCandidates(string toolId, string executableName)
    {
        return DefaultCandidateDirectories(toolId).Select(directory => Path.Combine(directory, executableName));
    }

    private static IEnumerable<string> DefaultCandidateDirectories(string toolId)
    {
        var appBase = AppContext.BaseDirectory;
        yield return appBase;
        yield return Path.Combine(appBase, "tools");
        yield return Path.Combine(appBase, toolId);

        if (OperatingSystem.IsMacOS())
        {
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return "/usr/bin";
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return "/usr/local/bin";
            yield return "/usr/bin";
            yield return "/bin";
            yield return "/snap/bin";
        }
        else if (OperatingSystem.IsWindows())
        {
            foreach (var root in WindowsProgramRoots())
            {
                yield return Path.Combine(root, "MKVToolNix");
                yield return Path.Combine(root, "eac3to");
                yield return Path.Combine(root, "ffmpeg", "bin");
            }
        }
    }

    private static IEnumerable<string> WindowsProgramRoots()
    {
        var names = new[]
        {
            "ProgramFiles",
            "ProgramFiles(x86)",
            "LOCALAPPDATA"
        };

        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }
}
