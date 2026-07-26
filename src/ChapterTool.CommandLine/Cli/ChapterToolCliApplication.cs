using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Runs ChapterTool command-line inspect, convert, and format workflows.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
    private readonly ICliConsole console;
    private readonly IChapterImporterRegistry importerRegistry;
    private readonly ChapterExportService exporter;
    private readonly IChapterExpressionEngine expressionEngine;
    private readonly string? configuredSavingPath;
    private readonly ISettingsStore<ChapterToolSettings> settingsStore;

    public ChapterToolCliApplication(
        ICliConsole? console = null,
        IChapterImporterRegistry? importerRegistry = null,
        ChapterExportService? exporter = null,
        string? configuredSavingPath = null,
        ISettingsStore<ChapterToolSettings>? settingsStore = null,
        string? settingsDirectory = null,
        IChapterExpressionEngine? expressionEngine = null)
    {
        this.console = console ?? new SystemCliConsole();
        var directory = ChapterToolRuntimeComposition.ResolveSettingsDirectory(settingsDirectory);
        this.settingsStore = settingsStore ?? new ChapterToolSettingsStore(directory);

        // Shared factories with GUI composition; injection seams remain for tests.
        this.expressionEngine = expressionEngine ?? new LuaExpressionScriptService();
        this.importerRegistry = importerRegistry
            ?? ChapterToolRuntimeComposition.CreateImporterRegistry(this.settingsStore);
        this.exporter = exporter
            ?? ChapterToolRuntimeComposition.CreateExportService(this.expressionEngine);
        this.configuredSavingPath = configuredSavingPath;
    }

    public int ShowFormats()
    {
        console.WriteLine("Input formats");
        foreach (var line in SupportedInputFormats())
        {
            console.WriteLine($"  {line}");
        }

        console.WriteLine();
        console.WriteLine("Output formats");
        foreach (var format in ChapterToolCliSupport.OutputFormats)
        {
            console.WriteLine($"  {format.Name,-12} {format.FileExtension,-18} {format.Description}");
        }

        console.WriteLine();
        console.WriteLine("Scope");
        console.WriteLine("  Basic import/export and terminal output are supported.");
        console.WriteLine("  Convert supports optional Lua expressions and built-in expression presets.");
        return 0;
    }

    private IEnumerable<string> SupportedInputFormats()
    {
        var importers = new[]
        {
            importerRegistry.Resolve("chapters.txt"),
            importerRegistry.Resolve("chapters.csv"),
            importerRegistry.Resolve("chapters.xml"),
            importerRegistry.Resolve("chapters.vtt"),
            importerRegistry.Resolve("chapters.cue"),
            importerRegistry.Resolve("chapters.flac"),
            importerRegistry.Resolve("chapters.tak"),
            importerRegistry.Resolve("chapters.mpls"),
            importerRegistry.Resolve("chapters.ifo"),
            importerRegistry.Resolve("chapters.xpl"),
            importerRegistry.Resolve("chapters.mkv"),
            importerRegistry.Resolve("chapters.mp4")
        }
        .OfType<IChapterImporter>()
        .DistinctBy(static importer => importer.Id)
        .OrderBy(static importer => importer.Id, StringComparer.Ordinal);

        foreach (var importer in importers)
        {
            var extensions = string.Join(", ", importer.SupportedExtensions.OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase));
            yield return $"{importer.Id,-20} {extensions}";
        }

        yield return "bdmv-directory       BDMV/PLAYLIST directory";
    }

    private static IEnumerable<string> FormatDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic => $"{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.DisplayCode}: {diagnostic.Message}");

    private void RenderFailure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        console.WriteErrorLine(message);
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteErrorLine($"  {line}");
        }
    }

    private void WriteDiagnosticsToError(IEnumerable<ChapterDiagnostic> diagnostics)
    {
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteErrorLine(line);
        }
    }

    private sealed record CliImportExecution(bool Success, IChapterImporter Importer, ChapterImportResult Result)
    {
        public static CliImportExecution Failure(params ChapterDiagnostic[] diagnostics) =>
            new(false, new NullImporter(), new ChapterImportResult(false, [], diagnostics));
    }

    private sealed class NullImporter : IChapterImporter
    {
        public string Id => "none";

        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>();

        public ValueTask<ChapterImportResult> ImportAsync(ChapterImportRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.Unavailable, "Importer is unavailable.")));
    }
}
