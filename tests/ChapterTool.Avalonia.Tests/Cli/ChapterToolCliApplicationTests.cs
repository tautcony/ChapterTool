using System.Text;
using ChapterTool.CommandLine;
using ChapterTool.CommandLine.Cli;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;

namespace ChapterTool.Avalonia.Tests.Cli;

public sealed class ChapterToolCliApplicationTests
{
    [Fact]
    public void Product_facade_keeps_desktop_and_standalone_launch_policies_distinct()
    {
        var existingPath = Path.GetTempFileName();
        try
        {
            var desktopWithoutArguments = ChapterToolCliHost.AnalyzeDesktopLaunch([]);
            var desktopWithPath = ChapterToolCliHost.AnalyzeDesktopLaunch([existingPath]);
            var standaloneWithPath = ChapterToolCliHost.Run([existingPath]);

            Assert.True(desktopWithoutArguments.LaunchGui);
            Assert.False(desktopWithoutArguments.RunCli);
            Assert.True(desktopWithPath.LaunchGui);
            Assert.Equal(existingPath, desktopWithPath.GuiStartupPath);
            Assert.Equal(1, standaloneWithPath);
        }
        finally
        {
            File.Delete(existingPath);
        }
    }

    [Fact]
    public void Standalone_facade_rejects_gui_only_load_command()
    {
        Assert.Equal(1, ChapterToolCliHost.Run(["load", "input.xml"]));
    }

    [Fact]
    public void SharedFactories_are_used_for_default_cli_construction()
    {
        var store = new ChapterToolSettingsStore(Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N")));
        var registry = ChapterToolRuntimeComposition.CreateImporterRegistry(store);
        var export = ChapterToolRuntimeComposition.CreateExportService(expressionEngine: null);

        Assert.NotNull(registry);
        Assert.NotNull(export);

        // CLI injects overrides; defaults share the same factory methods.
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: registry, exporter: export, settingsStore: store);
        Assert.Equal(0, app.ShowFormats());
        Assert.Contains("Output formats", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedExportFactory_without_expression_matches_cli_scope()
    {
        var export = ChapterToolRuntimeComposition.CreateExportService();
        var result = export.Export(
            new ChapterSet(
                "t",
                "s",
                ChapterImportFormat.Ogm,
                24,
                TimeSpan.FromSeconds(1),
                [new Chapter(1, TimeSpan.Zero, "Intro")]),
            new ChapterExportOptions(ChapterExportFormat.Txt, ApplyExpression: false));
        Assert.True(result.Success);
    }

    [Fact]
    public void AnalyzeLaunchRecognizesCliTokens()
    {
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["--help"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["formats"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["convert", "input.txt"]).CliResult);
        Assert.NotNull(ChapterToolCliSupport.AnalyzeLaunch(["movie.xml"]).CliResult);
    }

    [Fact]
    public void AnalyzeLaunchDoesNotBindInvalidCliOptionsBeforeRun()
    {
        var plan = ChapterToolCliSupport.AnalyzeLaunch(["convert", "missing.xml", "--format", "expr"]);

        Assert.False(plan.LaunchGui);
        Assert.NotNull(plan.CliResult);
    }

    [Fact]
    public void UnknownRootTokenReturnsFailureExitCode()
    {
        var plan = ChapterToolCliSupport.AnalyzeLaunch(["nosuchcommand"]);

        Assert.False(plan.LaunchGui);
        Assert.NotNull(plan.CliResult);
        Assert.NotEqual(0, plan.CliResult.Run());
    }

    [Fact]
    public void CommandLevelFormatsReturnsSuccess()
    {
        var plan = ChapterToolCliSupport.AnalyzeLaunch(["formats"]);

        Assert.Equal(0, plan.CliResult!.Run());
    }

    [Fact]
    public void CommandLevelConvertWritesOutputFile()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "chapters.txt");
        try
        {
            var plan = ChapterToolCliSupport.AnalyzeLaunch([
                "convert",
                XmlFixture(),
                "--format",
                "txt",
                "--output",
                outputPath,
                "--group-index",
                "0",
                "--entry-index",
                "0"
            ]);

            Assert.Equal(0, plan.CliResult!.Run());
            Assert.Contains("CHAPTER01=", File.ReadAllText(outputPath), StringComparison.Ordinal);
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
    public void CommandLevelConvertRejectsConflictingOutputOptions()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "chapters.txt");
        var plan = ChapterToolCliSupport.AnalyzeLaunch([
            "convert",
            XmlFixture(),
            "--format",
            "txt",
            "--stdout",
            "--output",
            outputPath,
            "--group-index",
            "0",
            "--entry-index",
            "0"
        ]);

        Assert.Equal(1, plan.CliResult!.Run());
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void AnalyzeLaunchReturnsLoadInputsForGui()
    {
        var path = Path.GetTempFileName();
        try
        {
            var loadByArgument = ChapterToolCliSupport.AnalyzeLaunch(["load", path]);
            var loadByOption = ChapterToolCliSupport.AnalyzeLaunch(["load", "--source", path]);
            var plainPath = ChapterToolCliSupport.AnalyzeLaunch([path]);
            var missingPlainPath = ChapterToolCliSupport.AnalyzeLaunch([Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.xml")]);
            var convert = ChapterToolCliSupport.AnalyzeLaunch(["convert", path]);
            var empty = ChapterToolCliSupport.AnalyzeLaunch([]);

            Assert.True(loadByArgument.LaunchGui);
            Assert.Equal(path, loadByArgument.GuiStartupPath);
            Assert.True(loadByOption.LaunchGui);
            Assert.Equal(path, loadByOption.GuiStartupPath);
            Assert.True(plainPath.LaunchGui);
            Assert.Equal(path, plainPath.GuiStartupPath);
            Assert.False(missingPlainPath.LaunchGui);
            Assert.NotNull(missingPlainPath.CliResult);
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
        Assert.Contains("Convert supports optional Lua expressions and built-in expression presets.", console.Stdout, StringComparison.Ordinal);
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
                EntryIndex: 0,
                EntryId: null,
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
    public async Task ConvertAppliesInlineExpressionBeforeExport()
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
                EntryIndex: 0,
                EntryId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: 24,
                Expression: "t + 1"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01=00:00:01.000", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAppliesBuiltInExpressionPresetBeforeExport()
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
                EntryIndex: 0,
                EntryId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: 24,
                ExpressionPreset: "offset-seconds"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01=00:00:01.000", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertRejectsConflictingExpressionOptions()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(
                Path.Combine(Path.GetTempPath(), "missing-expression-input.xml"),
                "txt",
                OutputPath: null,
                Stdout: true,
                GroupIndex: 0,
                EntryIndex: 0,
                EntryId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null,
                Expression: "t",
                ExpressionPreset: "identity"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("cannot be used together", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertRejectsUnknownExpressionPreset()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(
                Path.Combine(Path.GetTempPath(), "missing-expression-input.xml"),
                "txt",
                OutputPath: null,
                Stdout: true,
                GroupIndex: 0,
                EntryIndex: 0,
                EntryId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null,
                ExpressionPreset: "missing-preset"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown expression preset", console.Stderr, StringComparison.Ordinal);
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
                    EntryIndex: 0,
                    EntryId: null,
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
                EntryIndex: null,
                EntryId: null,
                XmlLanguage: null,
                SourceFileName: null,
                FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Group 0 has multiple entries", console.Stderr, StringComparison.Ordinal);
        Assert.Contains("SelectionGroup.Available", console.Stderr, StringComparison.Ordinal);
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
                EntryIndex: 0,
                EntryId: null,
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
