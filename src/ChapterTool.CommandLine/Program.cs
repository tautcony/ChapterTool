using ChapterTool.CommandLine;

try
{
    return ChapterToolCliHost.Run(args);
}
catch (Exception exception)
{
    await Console.Error.WriteLineAsync($"Unhandled CLI exception: {exception.Message}");
    return 2;
}
