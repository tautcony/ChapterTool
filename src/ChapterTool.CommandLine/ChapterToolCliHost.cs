using ChapterTool.CommandLine.Cli;

namespace ChapterTool.CommandLine;

public static class ChapterToolCliHost
{
    public static int Run(IReadOnlyList<string> args) => ChapterToolCliSupport.Run(args);
}
