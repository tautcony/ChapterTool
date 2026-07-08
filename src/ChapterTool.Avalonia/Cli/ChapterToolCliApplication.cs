using System.Globalization;
using System.Text;
using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Services;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Models;

namespace ChapterTool.Avalonia.Cli;

public sealed class ChapterToolCliApplication(
    ICliConsole? console = null,
    RuntimeChapterImporterRegistry? importerRegistry = null,
    ChapterExportService? exporter = null)
{
    private readonly ICliConsole console = console ?? new SystemCliConsole();
    private readonly RuntimeChapterImporterRegistry importerRegistry = importerRegistry ?? CreateImporterRegistry();
    private readonly ChapterExportService exporter = exporter ?? new ChapterExportService(new Core.Transform.ChapterTimeFormatter());

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
        console.WriteLine("  Expression and other advanced transforms are intentionally disabled in CLI.");
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

        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderFailure("Import failed.", import.Result.Diagnostics);
            return 1;
        }

        var selection = SelectOption(import.Result.Groups, request);
        if (selection is not { IsSuccess: true })
        {
            RenderFailure(selection?.Message ?? "Selection failed.", selection?.Diagnostics ?? Array.Empty<ChapterDiagnostic>());
            return 1;
        }

        var info = selection.Option!.ChapterInfo;
        var export = exporter.Export(
            info with
            {
                FramesPerSecond = request.FrameRate ?? info.FramesPerSecond
            },
            new ChapterExportOptions(
                format.Format,
                XmlLanguage: request.XmlLanguage,
                SourceFileName: request.SourceFileName,
                ApplyExpression: false,
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
            console.WriteErrorLine("Options --stdout and --output cannot be used together.");
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

    private async Task<int> WriteExportOutputAsync(
        CliConvertRequest request,
        CliOutputFormatDefinition format,
        ChapterInfo info,
        ChapterExportResult export,
        CancellationToken cancellationToken)
    {
        if (request.Stdout)
        {
            console.Write(export.Content);
            return 0;
        }

        var targetPath = ResolveOutputPath(request, format, info);
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

    private async Task<CliImportExecution> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, "MissingInput", "Input path is required."));
        }

        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, "InputNotFound", $"Input path '{inputPath}' was not found."));
        }

        var importer = importerRegistry.Resolve(inputPath);
        if (importer is null)
        {
            return CliImportExecution.Failure(new ChapterDiagnostic(DiagnosticSeverity.Error, "UnsupportedInput", $"No importer is available for '{inputPath}'."));
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
                            "FallbackImporterUsed",
                            $"Primary importer '{importer.Id}' could not be invoked; fallback importer '{fallback.Id}' was used.")
                    ]).ToList();

                    return new CliImportExecution(true, fallback, result with { Diagnostics = diagnostics });
                }
            }
        }

        return new CliImportExecution(result.Success, importer, result);
    }

    private CliSelectionResult? SelectOption(IReadOnlyList<ChapterInfoGroup> groups, CliConvertRequest request)
    {
        if (groups.Count == 0)
        {
            return CliSelectionResult.Failure("No chapter groups were imported.", []);
        }

        if (!TryResolveGroupIndex(groups, request, out var group, out var failure))
        {
            return failure;
        }

        if (group is null || group.Options.Count == 0)
        {
            return CliSelectionResult.Failure($"Group {request.GroupIndex ?? 0} contains no selectable chapter options.", []);
        }

        return ResolveOptionFromGroup(group, request);
    }

    private bool TryResolveGroupIndex(
        IReadOnlyList<ChapterInfoGroup> groups,
        CliConvertRequest request,
        out ChapterInfoGroup? group,
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

    private CliSelectionResult ResolveOptionFromGroup(ChapterInfoGroup group, CliConvertRequest request)
    {
        var groupIndex = request.GroupIndex ?? 0;

        if (!string.IsNullOrWhiteSpace(request.OptionId))
        {
            return ResolveOptionById(group, request.OptionId, groupIndex);
        }

        if (request.OptionIndex is not null)
        {
            return ResolveOptionByIndex(group, request.OptionIndex.Value, groupIndex);
        }

        if (group.Options.Count == 1)
        {
            return CliSelectionResult.Success(group.Options[0]);
        }

        return CliSelectionResult.Failure(
            $"Group {groupIndex} has multiple options. Specify --option-id or --option-index.",
            AmbiguousSelectionDiagnostics([group], groupIndex));
    }

    private CliSelectionResult ResolveOptionById(ChapterInfoGroup group, string optionId, int groupIndex)
    {
        var option = group.Options.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, optionId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            return CliSelectionResult.Failure(
                $"Option id '{optionId}' was not found in group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(option);
    }

    private CliSelectionResult ResolveOptionByIndex(ChapterInfoGroup group, int optionIndex, int groupIndex)
    {
        if (optionIndex < 0 || optionIndex >= group.Options.Count)
        {
            return CliSelectionResult.Failure(
                $"Option index {optionIndex} is out of range for group {groupIndex}.",
                AmbiguousSelectionDiagnostics([group], groupIndex));
        }

        return CliSelectionResult.Success(group.Options[optionIndex]);
    }

    private static string ResolveOutputPath(CliConvertRequest request, CliOutputFormatDefinition format, ChapterInfo info)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return Path.GetFullPath(request.OutputPath);
        }

        var inputFullPath = Path.GetFullPath(request.InputPath);
        var directory = Path.GetDirectoryName(inputFullPath) ?? Environment.CurrentDirectory;
        var baseName = Path.GetFileNameWithoutExtension(inputFullPath);
        return Path.Combine(directory, baseName + format.FileExtension);
    }

    private static IEnumerable<string> DescribeGroup(ChapterInfoGroup group)
    {
        for (var optionIndex = 0; optionIndex < group.Options.Count; optionIndex++)
        {
            var option = group.Options[optionIndex];
            var defaultMarker = optionIndex == group.DefaultOptionIndex ? " default" : string.Empty;
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"  ({optionIndex}) id={option.Id} name=\"{option.DisplayName}\" chapters={option.ChapterInfo.Chapters.Count(static chapter => !chapter.IsSeparator)} fps={option.ChapterInfo.FramesPerSecond:0.###}{defaultMarker}");
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

    private IReadOnlyList<ChapterDiagnostic> AmbiguousSelectionDiagnostics(IReadOnlyList<ChapterInfoGroup> groups, int groupOffset = 0)
    {
        var diagnostics = new List<ChapterDiagnostic>();
        for (var localGroupIndex = 0; localGroupIndex < groups.Count; localGroupIndex++)
        {
            var group = groups[localGroupIndex];
            var groupIndex = localGroupIndex + groupOffset;
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                "AvailableGroup",
                $"group={groupIndex} default-option-index={group.DefaultOptionIndex} source={group.SourcePath}"));
            for (var optionIndex = 0; optionIndex < group.Options.Count; optionIndex++)
            {
                var option = group.Options[optionIndex];
                diagnostics.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    "AvailableOption",
                    $"group={groupIndex} option-index={optionIndex} option-id={option.Id} name={option.DisplayName}"));
            }
        }

        return diagnostics;
    }

    private IEnumerable<string> FormatDiagnostics(IEnumerable<ChapterDiagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic => $"{diagnostic.Severity.ToString().ToUpperInvariant()} {diagnostic.Code}: {diagnostic.Message}");

    private void RenderFailure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        console.WriteErrorLine(message);
        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteErrorLine($"  {line}");
        }
    }

    private static RuntimeChapterImporterRegistry CreateImporterRegistry()
    {
        var formatter = new Core.Transform.ChapterTimeFormatter();
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "ChapterTool.Cli");
        Directory.CreateDirectory(settingsDirectory);
        var appSettingsStore = new Infrastructure.Configuration.AppSettingsStore(settingsDirectory);
        var toolLocator = new Infrastructure.Tools.ExternalToolLocator(appSettingsStore, AppCompositionRoot.PathSearchDirectoriesForTests().ToList());
        return new RuntimeChapterImporterRegistry(
            formatter,
            toolLocator,
            AppCompositionRoot.CreateProcessRunner(),
            new Infrastructure.Importing.Media.FfprobeMediaChapterReader(toolLocator, AppCompositionRoot.CreateProcessRunner()),
            AppCompositionRoot.CreateMp4ChapterReader());
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
            ValueTask.FromResult(ChapterImportResult.Failed(new ChapterDiagnostic(DiagnosticSeverity.Error, "Unavailable", "Importer is unavailable.")));
    }
}

public sealed record CliInspectRequest(string InputPath);

public sealed record CliConvertRequest(
    string InputPath,
    string Format,
    string? OutputPath,
    bool Stdout,
    int? GroupIndex,
    int? OptionIndex,
    string? OptionId,
    string? XmlLanguage,
    string? SourceFileName,
    double? FrameRate);

public sealed record CliSelectionResult(bool IsSuccess, ChapterSourceOption? Option, string Message, IReadOnlyList<ChapterDiagnostic> Diagnostics)
{
    public static CliSelectionResult Success(ChapterSourceOption option) => new(true, option, string.Empty, []);

    public static CliSelectionResult Failure(string message, IReadOnlyList<ChapterDiagnostic> diagnostics) => new(false, null, message, diagnostics);
}

public static class CliInputResolver
{
    public static string? Resolve(string? argumentInput, string? sourceOption) =>
        !string.IsNullOrWhiteSpace(sourceOption)
            ? sourceOption
            : string.IsNullOrWhiteSpace(argumentInput) ? null : argumentInput;
}
