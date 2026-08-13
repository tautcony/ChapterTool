using System.Security;
using System.Text;
using ChapterTool.CommandLine.Cli;

namespace ChapterTool.CommandLine;

public static class ChapterToolCliHost
{
    public static int Run(IReadOnlyList<string> args)
    {
        TryConfigureUtf8Console();
        return ChapterToolCliSupport.Run(args);
    }

    internal static bool TryConfigureUtf8Console()
    {
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.OutputEncoding = utf8;
            return Console.OutputEncoding.CodePage == utf8.CodePage;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or SecurityException)
        {
            return false;
        }
    }
}
