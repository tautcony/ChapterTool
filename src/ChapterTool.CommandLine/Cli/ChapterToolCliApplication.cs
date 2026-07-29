using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;

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
    private readonly ICliLocalizer localizer;

    public ChapterToolCliApplication(
        ICliConsole? console = null,
        IChapterImporterRegistry? importerRegistry = null,
        ChapterExportService? exporter = null,
        string? configuredSavingPath = null,
        ISettingsStore<ChapterToolSettings>? settingsStore = null,
        string? settingsDirectory = null,
        IChapterExpressionEngine? expressionEngine = null,
        ICliLocalizer? localizer = null)
    {
        this.console = console ?? new SystemCliConsole();
        this.localizer = localizer ?? new CliLocalizationManager();
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
        console.WriteLine(localizer.GetString("Cli.Header.InputFormats"));
        foreach (var line in SupportedInputFormats())
        {
            console.WriteLine($"  {line}");
        }

        console.WriteLine();
        console.WriteLine(localizer.GetString("Cli.Header.OutputFormats"));
        foreach (var format in ChapterToolCliSupport.OutputFormats)
        {
            console.WriteLine($"  {format.Name,-12} {format.FileExtension,-18} {format.Description}");
        }

        console.WriteLine();
        console.WriteLine(localizer.GetString("Cli.Header.Scope"));
        console.WriteLine($"  {localizer.GetString("Cli.Scope.Basic")}");
        console.WriteLine($"  {localizer.GetString("Cli.Scope.Expression")}");
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

    private IEnumerable<string> FormatDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(diagnostic =>
        {
            var key = $"Diagnostic.{diagnostic.DisplayCode}";
            var message = localizer.TryGetString(key, out _)
                ? localizer.Format(key, diagnostic.Arguments ?? new Dictionary<string, object?> { ["message"] = diagnostic.Message })
                : diagnostic.Message;
            return $"{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.DisplayCode}: {message}";
        });

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
