using System.Globalization;
using System.Text;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.CommandLine;

public sealed class ChapterToolCliApplication
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

    public async Task<int> InspectAsync(CliInspectRequest request, CancellationToken cancellationToken)
    {
        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderFailure("Import failed.", import.Result.Diagnostics);
            return 1;
        }

        console.WriteLine($"Source: {Path.GetFullPath(request.InputPath)}");
        console.WriteLine($"Importer: {import.Importer.Id}");
        console.WriteLine($"Groups: {import.Result.Groups.Count}");

        for (var groupIndex = 0; groupIndex < import.Result.Groups.Count; groupIndex++)
        {
            var group = import.Result.Groups[groupIndex];
            console.WriteLine();
            console.WriteLine($"[{groupIndex}] {Path.GetFileName(group.SourcePath)}");
            foreach (var optionLine in DescribeGroup(group))
            {
                console.WriteLine(optionLine);
            }
        }

        if (import.Result.Diagnostics.Count > 0)
        {
            console.WriteLine();
            console.WriteLine("Diagnostics");
            foreach (var line in FormatDiagnostics(import.Result.Diagnostics))
            {
                console.WriteLine($"  {line}");
            }
        }

        return 0;
    }

    public async Task<int> ConvertAsync(CliConvertRequest request, CancellationToken cancellationToken)
    {
        if (!TryValidateRequest(request, out var format, out var errorCode))
        {
            return errorCode;
        }

        if (!TryResolveExpression(request, out var expression, out var expressionPresetId, out var expressionSourceName))
        {
            return 1;
        }

        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderFailure("Import failed.", import.Result.Diagnostics);
            return 1;
        }

        var selection = SelectOption(import.Result.Groups, request);
        if (selection is not { IsSuccess: true })
        {
            RenderFailure(selection?.Message ?? "Selection failed.", selection?.Diagnostics ?? []);
            return 1;
        }

        var info = selection.Entry!.ChapterSet;
        var export = exporter.Export(
            info with
            {
                FramesPerSecond = request.FrameRate ?? info.FramesPerSecond
            },
            new ChapterExportOptions(
                format.Format,
                XmlLanguage: request.XmlLanguage,
                SourceFileName: request.SourceFileName,
                ApplyExpression: expression is not null,
                Expression: expression ?? "t",
                ExpressionPresetId: expressionPresetId,
                ExpressionSourceName: expressionSourceName,
                ProjectOutput: true));

        if (!export.Success)
        {
            RenderFailure("Export failed.", export.Diagnostics);
            return 1;
        }

        return await WriteExportOutputAsync(request, format, info, export, cancellationToken);
    }

    private bool TryValidateRequest(CliConvertRequest request, out CliOutputFormatDefinition format, out int errorCode)
    {
        if (request.Stdout && !string.IsNullOrWhiteSpace(request.OutputPath))
        {
            console.WriteErrorLine("Entries --stdout and --output cannot be used together.");
            format = null!;
            errorCode = 1;
            return false;
        }

        if (request.FrameRate is <= 0)
        {
            console.WriteErrorLine("Frame rate must be greater than zero when --frame-rate is specified.");
            format = null!;
            errorCode = 1;
            return false;
        }

        if (!ChapterToolCliSupport.TryParseFormat(request.Format, out format))
        {
            console.WriteErrorLine($"Unsupported output format '{request.Format}'.");
            console.WriteErrorLine("Run `formats` to see the supported CLI conversion targets.");
            errorCode = 1;
            return false;
        }

        errorCode = 0;
        return true;
    }

    private bool TryResolveExpression(
        CliConvertRequest request,
        out string? expression,
        out string expressionPresetId,
        out string expressionSourceName)
    {
        expression = null;
        expressionPresetId = string.Empty;
        expressionSourceName = string.Empty;

        var hasExpression = !string.IsNullOrWhiteSpace(request.Expression);
        var hasPreset = !string.IsNullOrWhiteSpace(request.ExpressionPreset);
        if (hasExpression && hasPreset)
        {
            console.WriteErrorLine("Options --expression and --expression-preset cannot be used together.");
            return false;
        }

        if (hasExpression)
        {
            expression = request.Expression;
            expressionSourceName = "CLI expression";
            return true;
        }

        if (!hasPreset)
        {
            return true;
        }

        var preset = expressionEngine.Presets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, request.ExpressionPreset, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            console.WriteErrorLine($"Unknown expression preset '{request.ExpressionPreset}'.");
            console.WriteErrorLine("Available presets: " + string.Join(", ", expressionEngine.Presets.Select(static candidate => candidate.Id)));
            return false;
        }

        expression = preset.ScriptText;
        expressionPresetId = preset.Id;
        expressionSourceName = preset.DisplayName;
        return true;
    }

    private async Task<int> WriteExportOutputAsync(
        CliConvertRequest request,
        CliOutputFormatDefinition format,
        ChapterSet info,
        ChapterExportResult export,
        CancellationToken cancellationToken)
    {
        if (request.Stdout)
        {
            console.Write(export.Content);
            WriteDiagnosticsToError(export.Diagnostics);
            return 0;
        }

        var targetPath = await ResolveOutputPathAsync(request, format, info, cancellationToken);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            console.WriteErrorLine("Output directory was not resolved. Provide --output or set a default save directory in settings.");
            return 1;
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(targetPath, export.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        console.WriteLine(targetPath);

        if (export.Diagnostics.Count > 0)
        {
            foreach (var line in FormatDiagnostics(export.Diagnostics))
            {
                console.WriteLine($"  {line}");
            }
        }

        return 0;
    }

    private void WriteDiagnosticsToError(IEnumerable<ChapterDiagnostic> diagnostics)
    {
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteErrorLine(line);
        }
    }

    private async Task<CliImportExecution> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.MissingInput, "Input path is required."));
        }

        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.InputNotFound, $"Input path '{inputPath}' was not found."));
        }

        var importer = importerRegistry.Resolve(inputPath);
        if (importer is null)
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, ChapterDiagnosticCode.UnsupportedInput, $"No importer is available for '{inputPath}'."));
        }

        var result = await importer.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
        if (!result.Success)
        {
            var fallback = importerRegistry.ResolveFallback(inputPath, importer, result);
            if (fallback is not null)
            {
                result = await fallback.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
                if (result.Success)
                {
                    var diagnostics = result.Diagnostics.Concat([
                        new ChapterDiagnostic(
                            DiagnosticSeverity.Info,
                            ChapterDiagnosticCode.ImporterFallbackUsed,
                            $"Primary importer '{importer.Id}' could not be invoked; fallback importer '{fallback.Id}' was used.")
                    ]).ToList();

                    return new CliImportExecution(true, fallback, result with { Diagnostics = diagnostics });
                }
            }
        }

        return new CliImportExecution(result.Success, importer, result);
    }

    private CliSelectionResult? SelectOption(IReadOnlyList<ChapterImportSource> groups, CliConvertRequest request)
    {
        if (groups.Count == 0)
        {
            return CliSelectionResult.Failure("No chapter groups were imported.", []);
        }

        if (!TryResolveGroupIndex(groups, request, out var group, out var failure))
        {
            return failure;
        }

        if (group is null || group.Entries.Count == 0)
        {
            return CliSelectionResult.Failure($"Group {request.GroupIndex ?? 0} contains no selectable chapter entries.", []);
        }

        return ResolveEntryFromGroup(group, request);
    }

    private bool TryResolveGroupIndex(
        IReadOnlyList<ChapterImportSource> groups,
        CliConvertRequest request,
        out ChapterImportSource? group,
        out CliSelectionResult? failure)
    {
        var groupIndex = request.GroupIndex ?? (groups.Count == 1 ? 0 : null);
        if (groupIndex is null || groupIndex < 0 || groupIndex >= groups.Count)
        {
            group = null;
            failure = CliSelectionResult.Failure(
                "Multiple groups are available. Specify --group-index to select one.",
                AmbiguousSelectionDiagnostics(groups));
            return false;
        }

        group = groups[groupIndex.Value];
        failure = null;
        return true;
    }

    private CliSelectionResult ResolveEntryFromGroup(ChapterImportSource group, CliConvertRequest request)
    {
        var groupIndex = request.GroupIndex ?? 0;

        if (!string.IsNullOrWhiteSpace(request.EntryId))
        {
            return ResolveEntryById(group, request.EntryId, groupIndex);
        }

        if (request.EntryIndex is not null)
        {
            return ResolveEntryByIndex(group, request.EntryIndex.Value, groupIndex);
        }

        if (group.Entries.Count == 1)
        {
            return CliSelectionResult.Success(group.Entries[0]);
        }

        return CliSelectionResult.Failure(
            $"Group {groupIndex} has multiple entries. Specify --entry-id or --entry-index.",
            AmbiguousSelectionDiagnostics([group], groupIndex));
    }

    private CliSelectionResult ResolveEntryById(ChapterImportSource group, string entryId, int groupIndex)
    {
        var entry = group.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entryId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return CliSelectionResult.Failure(
                $"Entry id '{entryId}' was not found in group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(entry);
    }

    private CliSelectionResult ResolveEntryByIndex(ChapterImportSource group, int entryIndex, int groupIndex)
    {
        if (entryIndex < 0 || entryIndex >= group.Entries.Count)
        {
            return CliSelectionResult.Failure(
                $"Entry index {entryIndex} is out of range for group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(group.Entries[entryIndex]);
    }

    private async Task<string?> ResolveOutputPathAsync(
        CliConvertRequest request,
        CliOutputFormatDefinition format,
        ChapterSet info,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return Path.GetFullPath(request.OutputPath);
        }

        var directory = await ResolveDefaultOutputDirectoryAsync(request.InputPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        var baseName = ChapterSavePath.BuildBaseFileName(info, request.InputPath);
        return ChapterSavePath.AllocateUniqueFilePath(directory, baseName, format.FileExtension);
    }

    private async Task<string?> ResolveDefaultOutputDirectoryAsync(string inputPath, CancellationToken cancellationToken)
    {
        var savingPath = configuredSavingPath ?? await LoadConfiguredSavingPathAsync(cancellationToken);
        if (ChapterSavePath.TryNormalizeDirectory(savingPath, out var configured) && configured is not null)
        {
            return configured;
        }

        return ChapterSavePath.DirectoryOfSourcePath(inputPath);
    }

    private async Task<string?> LoadConfiguredSavingPathAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsStore.LoadAsync(cancellationToken);
            return settings.Application.SavingPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CorruptSettingsFileException)
        {
            return null;
        }
    }

    private static IEnumerable<string> DescribeGroup(ChapterImportSource group)
    {
        for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
        {
            var entry = group.Entries[entryIndex];
            var defaultMarker = entryIndex == group.DefaultEntryIndex ? " default" : string.Empty;
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"  ({entryIndex}) id={entry.Id} name=\"{entry.DisplayName}\" chapters={entry.ChapterSet.Chapters.Count(static chapter => !chapter.IsSeparator)} fps={entry.ChapterSet.FramesPerSecond:0.###}{defaultMarker}");
        }
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

    private IReadOnlyList<ChapterDiagnostic> AmbiguousSelectionDiagnostics(IReadOnlyList<ChapterImportSource> groups, int groupOffset = 0)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        for (var localGroupIndex = 0; localGroupIndex < groups.Count; localGroupIndex++)
        {
            var group = groups[localGroupIndex];
            var groupIndex = localGroupIndex + groupOffset;
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.SelectionGroupAvailable,
                $"group={groupIndex} default-entry-index={group.DefaultEntryIndex} source={group.SourcePath}"));
            for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                var entry = group.Entries[entryIndex];
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.SelectionOptionAvailable,
                    $"group={groupIndex} entry-index={entryIndex} entry-id={entry.Id} name={entry.DisplayName}"));
            }
        }

        return diagnostics;
    }

    private IEnumerable<string> FormatDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic => $"{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.DisplayCode}: {diagnostic.Message}");

    private void RenderFailure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        console.WriteErrorLine(message);
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteErrorLine($"  {line}");
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

public sealed record CliInspectRequest(string InputPath);

public sealed record CliConvertRequest(
    string InputPath,
    string Format,
    string? OutputPath,
    bool Stdout,
    int? GroupIndex,
    int? EntryIndex,
    string? EntryId,
    string? XmlLanguage,
    string? SourceFileName,
    double? FrameRate,
    string? Expression = null,
    string? ExpressionPreset = null);

public sealed record CliSelectionResult(bool IsSuccess, ChapterImportEntry? Entry, string Message, IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public static CliSelectionResult Success(ChapterImportEntry entry) => new(true, entry, string.Empty, []);

    public static CliSelectionResult Failure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics) => new(false, null, message, diagnostics);
}

public static class CliInputResolver
{
    public static string? Resolve(string? argumentInput, string? sourceOption) =>
        !string.IsNullOrWhiteSpace(sourceOption)
            ? sourceOption
            : string.IsNullOrWhiteSpace(argumentInput) ? null : argumentInput;
}
