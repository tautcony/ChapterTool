namespace ChapterTool.Avalonia.UI.PlatformPorts;

public static class ExternalToolExecutableNames
{
    public static string ExecutableName(string toolId) =>
        OperatingSystem.IsWindows() && !toolId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? $"{toolId}.exe"
            : toolId;
}
