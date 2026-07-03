namespace ChapterTool.Infrastructure.Processes;

public static class DotNetHost
{
    public static string FileName => OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
}
