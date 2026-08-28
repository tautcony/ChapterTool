using System.Globalization;
using System.Text;
using ChapterTool.CommandLine.Cli;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
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
    public void CliLocalizationSettingTheSameCultureReturnsEarly()
    {
        var localizer = new CliLocalizationManager("en-US");

        localizer.SetCulture("en-US");

        Assert.Equal("en-US", localizer.CurrentCultureName);
    }

    [Fact]
    public void CliLocalizationFallsBackToEnglishWhenCurrentCultureMissesAKey()
    {
        var localizer = new CliLocalizationManager(
            "zh-CN",
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["zh-CN"] = new Dictionary<string, string>()
            });

        Assert.True(localizer.TryGetString("Cli.Header.InputFormats", out var value));
        Assert.Equal("Input formats", value);
    }

    [Fact]
    public void CliLocalizationFormatWithoutArgumentsReturnsRawString()
    {
        var localizer = new CliLocalizationManager("en-US");

        Assert.Equal("Input formats", localizer.Format("Cli.Header.InputFormats"));
    }

    [Fact]
    public void SystemCliConsoleWritesThroughStandardStreams()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Console.SetOut(output);
            Console.SetError(error);
            var console = new SystemCliConsole();

            console.Write("out");
            console.WriteLine("line");
            console.WriteError("err");
            console.WriteErrorLine("line");

            Assert.Equal("outline\n", output.ToString());
            Assert.Equal("errline\n", error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
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
        Assert.Contains("Convert supports custom Lua expressions and built-in expression presets.", console.Stdout, StringComparison.Ordinal);
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
    public async Task ConvertWithNoOutputPathResolvesDirectoryBesideSource()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "chapters.xml");
        File.Copy(XmlFixture(), input);
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(
            console: console,
            settingsDirectory: Path.Combine(temp.Path, "settings"));

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(input, "xml", OutputPath: null, Stdout: false, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        var written = Assert.Single(Directory.GetFiles(temp.Path, "*.xml"), file => !string.Equals(file, input, StringComparison.Ordinal));
        Assert.Contains(written, console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithNoOutputPathUsesConfiguredSavingPath()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "chapters.xml");
        File.Copy(XmlFixture(), input);
        var outputDir = Path.Combine(temp.Path, "out");
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, configuredSavingPath: outputDir);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(input, "xml", OutputPath: null, Stdout: false, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        var written = Assert.Single(Directory.GetFiles(outputDir, "*.xml"));
        Assert.Contains(written, console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithNoOutputPathFallsBackWhenSettingsAreUnreadable()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "chapters.xml");
        File.Copy(XmlFixture(), input);
        var settingsPath = Path.Combine(temp.Path, "settings");
        Directory.CreateDirectory(settingsPath);
        Directory.CreateDirectory(Path.Combine(settingsPath, "settings.json"));
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, settingsDirectory: settingsPath);

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(input, "xml", OutputPath: null, Stdout: false, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("chapters", console.Stdout, StringComparison.Ordinal);
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
    public async Task ConvertPropagatesOperationCancellation()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new ThrowingCancellationRegistry());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => app.ConvertAsync(
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
                cts.Token));
    }

    [Fact]
    public async Task InspectPropagatesOperationCancellation()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new ThrowingCancellationRegistry());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => app.InspectAsync(new CliInspectRequest(XmlFixture()), cts.Token));
    }

    [Fact]
    public async Task ConvertUsesFallbackImporterWhenPrimaryFails()
    {
        var console = new RecordingCliConsole();
        var store = new ChapterToolSettingsStore(Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N")));
        var real = ChapterToolRuntimeComposition.CreateImporterRegistry(store);
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FailingThenFallbackRegistry(real));

        var exitCode = await app.ConvertAsync(
            new CliConvertRequest(XmlFixture(), "txt", OutputPath: null, Stdout: true, GroupIndex: 0, EntryIndex: 0, EntryId: null, XmlLanguage: null, SourceFileName: null, FrameRate: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01=", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithNoGroupsReportsNoGroupsError()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(SelectionResult()));

        var exitCode = await app.ConvertAsync(ConvertRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("No chapter groups were imported.", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithEmptyGroupReportsEmptyGroupError()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(SelectionResult(Group("a.mpls"))));

        var exitCode = await app.ConvertAsync(ConvertRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("contains no selectable chapter entries", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithMultipleGroupsAndNoGroupIndexReportsAmbiguity()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(
            SelectionResult(Group("a.mpls", Entry("e1", "A")), Group("b.mpls", Entry("e2", "B")))));

        var exitCode = await app.ConvertAsync(
            ConvertRequest(groupIndex: null, entryIndex: null, entryId: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Multiple groups are available.", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithEntryIdSelectsEntry()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(
            SelectionResult(Group("a.mpls", Entry("e1", "A"), Entry("e2", "B")))));

        var exitCode = await app.ConvertAsync(
            ConvertRequest(groupIndex: 0, entryIndex: null, entryId: "e2"),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01NAME=B", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithUnknownEntryIdReportsNotFound()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(
            SelectionResult(Group("a.mpls", Entry("e1", "A")))));

        var exitCode = await app.ConvertAsync(
            ConvertRequest(groupIndex: 0, entryIndex: null, entryId: "missing"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("was not found in group", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithOutOfRangeEntryIndexReportsError()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(
            SelectionResult(Group("a.mpls", Entry("e1", "A")))));

        var exitCode = await app.ConvertAsync(
            ConvertRequest(groupIndex: 0, entryIndex: 99, entryId: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("is out of range for group", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithSingleEntryGroupSelectsTheOnlyEntry()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(
            SelectionResult(Group("a.mpls", Entry("e1", "A")))));

        var exitCode = await app.ConvertAsync(
            ConvertRequest(groupIndex: null, entryIndex: null, entryId: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CHAPTER01NAME=A", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectWithEmptyInputReportsInputRequired()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console);

        var exitCode = await app.InspectAsync(new CliInspectRequest(string.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Input", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectRendersImportDiagnosticsSection()
    {
        var console = new RecordingCliConsole();
        var result = new ChapterImportResult(
            true,
            [Group("a.mpls", Entry("e1", "A"))],
            [new ChapterDiagnostic(DiagnosticSeverity.Info, ChapterDiagnosticCode.BdmvScanCandidate, "scanned 1 playlist")]);
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FixedImportRegistry(result));

        var exitCode = await app.InspectAsync(new CliInspectRequest(XmlFixture()), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Diagnostics", console.Stdout, StringComparison.Ordinal);
        Assert.Contains("scanned 1 playlist", console.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectCommandRunsThroughTheCliHost()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "chapters.xml");
        File.Copy(XmlFixture(), input);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            var command = new InspectCliCommand { Input = input };

            var exitCode = await command.RunAsync();

            Assert.Equal(0, exitCode);
            Assert.Contains("Groups: 1", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void UnrecognizedLanguageWarnsOnStandardError()
    {
        var originalError = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);
            CliLanguage.WarnIfUnrecognized("bogus");
            Assert.Contains("Unrecognized language 'bogus'", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task ConvertWithUnavailableFallbackReportsImportFailure()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FailingNoFallbackRegistry());

        var exitCode = await app.ConvertAsync(ConvertRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Import failed", console.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertWithFailingFallbackReportsImportFailure()
    {
        var console = new RecordingCliConsole();
        var app = new ChapterToolCliApplication(console: console, importerRegistry: new FailingFallbackRegistry());

        var exitCode = await app.ConvertAsync(ConvertRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Import failed", console.Stderr, StringComparison.Ordinal);
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

    private static CliConvertRequest ConvertRequest(
        int? groupIndex = 0,
        int? entryIndex = 0,
        string? entryId = null) =>
        new(XmlFixture(), "txt", OutputPath: null, Stdout: true, GroupIndex: groupIndex, EntryIndex: entryIndex, EntryId: entryId, XmlLanguage: null, SourceFileName: null, FrameRate: null);

    private static ChapterImportResult SelectionResult(params ChapterImportSource[] groups) =>
        new(true, groups, []);

    private static ChapterImportSource Group(string source, params ChapterImportEntry[] entries) =>
        new(source, entries);

    private static ChapterImportEntry Entry(string id, string name) =>
        new(id, name, new ChapterSet(name, name, ChapterImportFormat.Ogm, 24, TimeSpan.Zero, [new Chapter(1, TimeSpan.Zero, name)]));

    private sealed class ThrowingCancellationImporter : IChapterImporter
    {
        public string Id => "cancel-probe";

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { ".xml" };

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class ThrowingCancellationRegistry : IChapterImporterRegistry
    {
        private readonly IChapterImporter importer = new ThrowingCancellationImporter();

        public IChapterImporter? Resolve(string path) => importer;

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => null;
    }

    private sealed class FailingThenFallbackRegistry : IChapterImporterRegistry
    {
        private readonly IChapterImporterRegistry inner;

        public FailingThenFallbackRegistry(IChapterImporterRegistry inner) => this.inner = inner;

        public IChapterImporter? Resolve(string path) => new FailingImporter();

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => inner.Resolve(path);
    }

    private sealed class FailingNoFallbackRegistry : IChapterImporterRegistry
    {
        public IChapterImporter? Resolve(string path) => new FailingImporter();

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => null;
    }

    private sealed class FailingFallbackRegistry : IChapterImporterRegistry
    {
        public IChapterImporter? Resolve(string path) => new FailingImporter();

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => new FailingImporter();
    }

    private sealed class FailingImporter : IChapterImporter
    {
        public string Id => "failing-primary";

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { ".xml" };

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ChapterImportResult.Failed(
                new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.Unavailable, "Primary importer failed.")));
    }

    private sealed class FixedImportRegistry(ChapterImportResult result) : IChapterImporterRegistry
    {
        public IChapterImporter? Resolve(string path) => new FixedImporter(result);

        public IChapterImporter? ResolveFallback(string path, IChapterImporter primaryImporter, ChapterImportResult primaryResult) => null;
    }

    private sealed class FixedImporter(ChapterImportResult result) : IChapterImporter
    {
        public string Id => "fixed";

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { ".xml" };

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ChapterTool_Cli_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
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
