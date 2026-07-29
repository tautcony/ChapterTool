using ChapterTool.CommandLine;

try
{
    return ChapterToolCliHost.Run(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Unhandled CLI exception: {exception.Message}");
    return 2;
}
