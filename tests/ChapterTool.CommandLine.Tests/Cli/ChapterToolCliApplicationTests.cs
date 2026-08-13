using System.Globalization;
using System.Text;
using ChapterTool.CommandLine;
using ChapterTool.CommandLine.Cli;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Localization;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.TestSupport;

namespace ChapterTool.CommandLine.Tests.Cli;

public sealed class ChapterToolCliApplicationTests
{
    [Fact]
    public void CliLocalizationSupportsIndependentCultures()
    {
        var localizer = new CliLocalizationManager("zh-CN");

        Assert.Equal("输入格式", localizer.GetString("Cli.Header.InputFormats"));
        Assert.Equal(
            "找不到输入路径“missing”。",
            localizer.Format("Cli.Error.InputNotFound", new Dictionary<string, object?> { ["path"] = "missing" }));
        Assert.True(localizer.TryGetString("Diagnostic.Xml.Invalid", out var diagnostic));
        Assert.Contains("XML", diagnostic, StringComparison.OrdinalIgnoreCase);

        localizer.SetCulture("ja-JP");

        Assert.Equal("入力形式", localizer.GetString("Cli.Header.InputFormats"));
    }

    [Fact]
    public void Standalone_facade_returns_usage_failure_for_plain_path()
    {
        var existingPath = Path.GetTempFileName();
        try
        {
            var standaloneWithPath = ChapterToolCliHost.Run([existingPath]);

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
    public void Standalone_facade_shows_help_without_arguments()
    {
        Assert.Equal(0, ChapterToolCliHost.Run([]));
    }

    [Fact]
    public void HostConfiguresUtf8ConsoleOutputEncoding()
    {
        var previous = Console.OutputEncoding;
        try
        {
            var configured = ChapterToolCliHost.TryConfigureUtf8Console();

            Assert.True(configured);
            Assert.Equal(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).CodePage, Console.OutputEncoding.CodePage);
            Assert.Empty(Console.OutputEncoding.GetPreamble());
        }
        finally
        {
            try
            {
                Console.OutputEncoding = previous;
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void Standalone_facade_returns_failure_for_invalid_cli_options()
    {
        Assert.NotEqual(0, ChapterToolCliHost.Run(["convert", "missing.xml", "--format", "expr"]));
    }

    [Fact]
    public void UnknownRootTokenReturnsFailureExitCode()
    {
        Assert.NotEqual(0, ChapterToolCliHost.Run(["nosuchcommand"]));
    }

    [Fact]
    public void CommandLevelFormatsReturnsSuccess()
    {
        Assert.Equal(0, ChapterToolCliHost.Run(["formats"]));
    }

    [Fact]
    public void CommandLevelConvertWritesOutputFile()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "chapters.txt");
        try
        {
            var exitCode = ChapterToolCliHost.Run([
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

            Assert.Equal(0, exitCode);
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
        var exitCode = ChapterToolCliHost.Run([
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

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(outputPath));
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

    [Fact]
    public async Task ConvertFailsWhenInputPathIsMissing()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var missing = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "missing.xml");

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(missing, "txt", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(missing, console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertFailsForUnsupportedInputExtension()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "notes.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "not a chapter file", TestContext.Current.CancellationToken);
        try
        {
            var exitCode = await app.ConvertAsync(
                new CliConvertRequest(path, "txt", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, exitCode);
            Assert.Contains(path, console.Stderr, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConvertReadsMplsPlaylistFixture()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var input = TestRepository.CoreFixture("Importing", "Disc", "Mpls", "00011_24_Eva.mpls");

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(input, "txt", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01=", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertRefusesExistingOutputUnlessForced()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "existing.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, "sentinel", TestContext.Current.CancellationToken);

        try
        {
            var refused = await app.ConvertAsync(
                new CliConvertRequest(XmlFixture(), "txt", outputPath, Stdout: false, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, refused);
            Assert.Contains("already exists", console.Stderr, StringComparison.Ordinal);
            Assert.Equal("sentinel", await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken));

            console = new RecordingCliConsole();
            app = new ChapterToolCliApplication(console: console);
            var overwritten = await app.ConvertAsync(
                new CliConvertRequest(XmlFixture(), "txt", outputPath, Stdout: false, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null, Force: true),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, overwritten);
            var content = await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Contains("CHAPTER01=", content, StringComparison.Ordinal);
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
    public async Task ConvertRejectsNonFiniteFrameRate()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var nan = await app.ConvertAsync(
            new CliConvertRequest(XmlFixture(), "qpf", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: double.NaN),
            TestContext.Current.CancellationToken);
        var infinity = await app.ConvertAsync(
            new CliConvertRequest(XmlFixture(), "qpf", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: double.PositiveInfinity),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, nan);
        Assert.Equal(1, infinity);
        Assert.Contains("finite number greater than zero", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertReportsOutOfRangeGroupIndex()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(XmlFixture(), "txt", OutputPath: null, Stdout: true, GroupIndex: 5, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Group index 5 is outside the available range.", console.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("Multiple groups are available", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWritesRequestedEncodingAndBom()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);
        var outputPath = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"), "chapters.txt");

        try
        {
            var exitCode = await app.ConvertAsync(
                new CliConvertRequest(
                    XmlFixture(),
                    "txt",
                    outputPath,
                    Stdout: false,
                    GroupIndex: 0,
                    EntryIndex: 0,
                    EntryId: null,
                    XmlLanguage: null,
                    SourceFileName: null,
                    FrameRate: null,
                    TextEncoding: "utf16le",
                    EmitBom: true),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            var bytes = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.True(bytes.Length >= 2);
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xFE, bytes[1]);
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
    public async Task ConvertRejectsUnsupportedEncoding()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(XmlFixture(), "txt", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null, TextEncoding: "latin1"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unsupported text encoding 'latin1'.", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizationManagerAcceptsShortLanguageCodesWithoutRewritingThreadCulture()
    {
        var before = CultureInfo.CurrentUICulture;
        var localizer = new CliLocalizationManager("zh");

        Assert.Equal(UiLanguageCode.Chinese, localizer.CurrentCultureName);
        Assert.Equal("输入格式", localizer.GetString("Cli.Header.InputFormats"));
        Assert.Equal(before, CultureInfo.CurrentUICulture);
    }

    private static string XmlFixture() => TestRepository.CoreFixture(
        "Importing",
        "Text",
        "Xml",
        "xml (T2 - 4 Editions).xml");

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
