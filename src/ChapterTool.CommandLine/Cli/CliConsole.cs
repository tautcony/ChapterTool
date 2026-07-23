namespace ChapterTool.CommandLine.Cli;

public interface ICliConsole
{
    void Write(string text);

    void WriteLine(string text = "");

    void WriteError(string text);

    void WriteErrorLine(string text = "");
}

public sealed class SystemCliConsole : ICliConsole
{
    public void Write(string text) => Console.Out.Write(text);

    public void WriteLine(string text = "") => Console.Out.WriteLine(text);

    public void WriteError(string text) => Console.Error.Write(text);

    public void WriteErrorLine(string text = "") => Console.Error.WriteLine(text);
}
