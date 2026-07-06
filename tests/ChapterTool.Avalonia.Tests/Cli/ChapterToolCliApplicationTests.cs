using System.Text;
using ChapterTool.Avalonia.Cli;

namespace ChapterTool.Avalonia.Tests.Cli;

public sealed class ChapterToolCliApplicationTests
{
    [Fact]
    public void AnalyzeLaunchRecognizesCliTokens()
    {
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["--help"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["formats"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["convert", "input.txt"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["movie.xml"]).CliResult);
    }

    [Fact]
    public void AnalyzeLaunchReturnsOnlyExplicitLoadInputsForGui()
    {
        var path = Path.GetTempFileName();
        try
        {
            var loadByArgument = ChapterToolCliSupport.AnalyzeLaunch(["load", path]);
            var loadByOption = ChapterToolCliSupport.AnalyzeLaunch(["load", "--source", path]);
            var plainPath = ChapterToolCliSupport.AnalyzeLaunch([path]);
            var convert = ChapterToolCliSupport.AnalyzeLaunch(["convert", path]);
            var empty = ChapterToolCliSupport.AnalyzeLaunch([]);

            Assert.True(loadByArgument.LaunchGui);
            Assert.Equal(path, loadByArgument.GuiStartupPath);
            Assert.True(loadByOption.LaunchGui);
            Assert.Equal(path, loadByOption.GuiStartupPath);
            Assert.False(plainPath.LaunchGui);
            Assert.Null(plainPath.GuiStartupPath);
            Assert.False(convert.LaunchGui);
            Assert.Null(convert.GuiStartupPath);
            Assert.False(empty.LaunchGui);
            Assert.Null(empty.GuiStartupPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShowFormatsListsStableScope()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = app.ShowFormats();

        Assert.Equal(0, exitCode);
        Assert.Contains("Input formats", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("Output formats", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("txt", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("xml", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("Expression and other advanced transforms are intentionally disabled in CLI.", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectShowsAvailableGroupsAndOptions()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.InspectAsync(new CliInspectRequest(XmlFixture()), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Groups: 1", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("id=edition-0", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("name=\"Edition 01\"", console.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Import failed.", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWritesStdoutForBasicTxtExport()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(
                XmlFixture(),
                "txt",
                OutputPath: null,
                Stdout: true,
                GroupIndex: 0,
                OptionIndex: 0,
                OptionId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01=", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("CHAPTER01NAME=", console.Stdout, StringComparison.Ordinal);
        Assert.Equal(string.Empty, console.Stderr);
    }

    [Fact]
    public async Task ConvertWritesOutputFileWhenRequested()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "chapters.xml");

        try
        {
            var exitCode = await app.ConvertAsync(
                new CliConvertRequest(
                    XmlFixture(),
                    "xml",
                    outputPath,
                    Stdout: false,
                    GroupIndex: 0,
                    OptionIndex: 0,
                    OptionId: null,
                    XmlLanguage: "eng",
                    SourceFileName: null,
                    FrameRate: null),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Contains("<Chapters>", content, StringComparison.Ordinal);
            Assert.Contains("<ChapterLanguage>eng</ChapterLanguage>", content, StringComparison.Ordinal);
            Assert.Contains(outputPath, console.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ConvertFailsWhenSelectionIsAmbiguous()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(
                XmlFixture(),
                "txt",
                OutputPath: null,
                Stdout: true,
                GroupIndex: null,
                OptionIndex: null,
                OptionId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Group 0 has multiple options", console.Stderr, StringComparison.Ordinal);
        Assert.Contains("AvailableGroup", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertFailsForUnsupportedFormat()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(
                XmlFixture(),
                "expr",
                OutputPath: null,
                Stdout: true,
                GroupIndex: 0,
                OptionIndex: 0,
                OptionId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unsupported output format 'expr'.", console.Stderr, StringComparison.Ordinal);
    }

    private static string XmlFixture() => Path.Combine(
        RepositoryRoot(),
        "tests",
        "ChapterTool.Core.Tests",
        "Fixtures",
        "Importing",
        "Text",
        "Xml",
        "xml (T2 - 4 Editions).xml");

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "openspec")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located from test output directory.");
    }

    private sealed class RecordingCliConsole : ICliConsole
    {
        private readonly StringBuilder stdout = new();
        private readonly StringBuilder stderr = new();

        public string Stdout => stdout.ToString();

        public string Stderr => stderr.ToString();

        public void Write(string text) => stdout.Append(text);

        public void WriteLine(string text = "") => stdout.AppendLine(text);

        public void WriteError(string text) => stderr.Append(text);

        public void WriteErrorLine(string text = "") => stderr.AppendLine(text);
    }
}
