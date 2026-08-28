using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Command-line convert workflow for projection and export.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
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

        var info = await SelectChapterSetAsync(request, cancellationToken);
        if (info is null)
        {
            return 1;
        }

        var export = CreateExport(format, info, request, expression, expressionPresetId, expressionSourceName);
        if (!export.Success)
        {
            RenderFailure(localizer.GetString("Cli.Error.ExportFailed"), export.Diagnostics);
            return 1;
        }

        return await WriteExportOutputAsync(request, format, info, export, cancellationToken);
    }

    private async Task<ChapterSet?> SelectChapterSetAsync(CliConvertRequest request, CancellationToken cancellationToken)
    {
        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderImportFailure(import.Result.Diagnostics);
            return null;
        }

        var selection = SelectOption(import.Result.Groups, request);
        if (selection is not { IsSuccess: true })
        {
            RenderSelectionFailure(selection);
            return null;
        }

        return selection.Entry!.ChapterSet;
    }

    private void RenderImportFailure(IReadOnlyList<ChapterDiagnostic> diagnostics) =>
        RenderFailure(localizer.GetString("Cli.Error.ImportFailed"), diagnostics);

    private void RenderSelectionFailure(CliSelectionResult? selection)
    {
        if (selection is null)
        {
            RenderFailure(localizer.GetString("Cli.Error.SelectionFailed"), []);
            return;
        }

        RenderFailure(selection.Message, selection.Diagnostics);
    }

    private ChapterExportResult CreateExport(
        CliOutputFormatDefinition format,
        ChapterSet info,
        CliConvertRequest request,
        string? expression,
        string expressionPresetId,
        string expressionSourceName)
    {
        var projected = info with { FramesPerSecond = request.FrameRate ?? info.FramesPerSecond };
        var options = new ChapterExportOptions(
            format.Format,
            XmlLanguage: request.XmlLanguage,
            SourceFileName: request.SourceFileName,
            ApplyExpression: expression is not null,
            Expression: expression ?? "t",
            ExpressionPresetId: expressionPresetId,
            ExpressionSourceName: expressionSourceName,
            ProjectOutput: true);
        return exporter.Export(projected, options);
    }

    private bool TryValidateRequest(CliConvertRequest request, out CliOutputFormatDefinition format, out int errorCode)
    {
        if (ValidateOutputTarget(request)
            && ValidateFrameRate(request)
            && ValidateEncoding(request)
            && ValidateFormat(request, out format))
        {
            errorCode = 0;
            return true;
        }

        format = null!;
        errorCode = 1;
        return false;
    }

    private bool ValidateOutputTarget(CliConvertRequest request) => !(request.Stdout && !string.IsNullOrWhiteSpace(request.OutputPath)) || WriteValidationError("Cli.Error.StdoutOutputConflict");

    private bool ValidateFrameRate(CliConvertRequest request) => request.FrameRate is not { } frameRate || (double.IsFinite(frameRate) && frameRate > 0) || WriteValidationError("Cli.Error.FrameRatePositive");

    private bool ValidateEncoding(CliConvertRequest request) => string.IsNullOrWhiteSpace(request.TextEncoding) || OutputTextEncodings.TryParse(request.TextEncoding, out _) || WriteValidationMessage(localizer.Format("Cli.Error.UnsupportedEncoding", new Dictionary<string, object?> { ["encoding"] = request.TextEncoding }));

    private bool ValidateFormat(CliConvertRequest request, out CliOutputFormatDefinition format)
    {
        if (ChapterToolCliSupport.TryParseFormat(request.Format, out format))
        {
            return true;
        }
        console.WriteErrorLine(localizer.Format("Cli.Error.UnsupportedFormat", new Dictionary<string, object?> { ["format"] = request.Format }));
        console.WriteErrorLine(localizer.GetString("Cli.Error.FormatsHint"));
        return false;
    }

    private bool WriteValidationError(string message)
    {
        console.WriteErrorLine(localizer.GetString(message));
        return false;
    }

    private bool WriteValidationMessage(string message)
    {
        console.WriteErrorLine(message);
        return false;
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
            console.WriteErrorLine(localizer.GetString("Cli.Error.ExpressionConflict"));
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
            console.WriteErrorLine(localizer.Format("Cli.Error.UnknownExpressionPreset", new Dictionary<string, object?> { ["preset"] = request.ExpressionPreset }));
            console.WriteErrorLine(localizer.Format("Cli.Error.AvailablePresets", new Dictionary<string, object?> { ["presets"] = string.Join(", ", expressionEngine.Presets.Select(static candidate => candidate.Id)) }));
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
            WriteToStdout(export);
            return 0;
        }

        var targetPath = await ResolveOutputPathAsync(request, format, info, cancellationToken);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            console.WriteErrorLine(localizer.GetString("Cli.Error.OutputDirectory"));
            return 1;
        }

        if (!CanWriteToPath(targetPath, request.Force))
        {
            return 1;
        }

        await WriteFileAsync(targetPath, export, request, cancellationToken);
        console.WriteLine(targetPath);
        WriteExportDiagnostics(export.Diagnostics);
        return 0;
    }

    private void WriteToStdout(ChapterExportResult export)
    {
        console.Write(export.Content);
        WriteDiagnosticsToError(export.Diagnostics);
    }

    private bool CanWriteToPath(string targetPath, bool force)
    {
        if (!File.Exists(targetPath) || force)
        {
            return true;
        }

        console.WriteErrorLine(localizer.Format("Cli.Error.OutputExists", new Dictionary<string, object?> { ["path"] = targetPath }));
        return false;
    }

    private async Task WriteFileAsync(string targetPath, ChapterExportResult export, CliConvertRequest request, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoding = OutputTextEncodings.TryParse(request.TextEncoding, out var parsedEncoding)
            ? parsedEncoding
            : OutputTextEncoding.Utf8;
        await File.WriteAllTextAsync(
            targetPath,
            export.Content,
            OutputTextEncodings.Create(encoding, request.EmitBom),
            cancellationToken);
    }

    private void WriteExportDiagnostics(IReadOnlyList<ChapterDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        foreach (var line in FormatDiagnostics(diagnostics))
        {
            console.WriteLine($"  {line}");
        }
    }
}
