namespace ChapterTool.CommandLine;

public static class ChapterToolCliHost
{
    public static int Run(IReadOnlyList<string> args) => ChapterToolCliSupport.Run(args);

    public static DesktopLaunchDecision AnalyzeDesktopLaunch(IReadOnlyList<string> args)
    {
        var plan = ChapterToolCliSupport.AnalyzeDesktopLaunch(args);
        return new DesktopLaunchDecision(
            plan.LaunchGui || plan.CliResult is null,
            plan.GuiStartupPath,
            plan.CliResult is not null);
    }
}

public sealed record DesktopLaunchDecision(bool LaunchGui, string? GuiStartupPath, bool RunCli);
