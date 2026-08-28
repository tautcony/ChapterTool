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

        var import = await ImportAsync(request.InputPath, cancellationToken);
        if (!import.Success)
        {
            RenderFailure(localizer.GetString("Cli.Error.ImportFailed"), import.Result.Diagnostics);
            return 1;
        }

        var selection = SelectOption(import.Result.Groups, request);
        if (selection is not { IsSuccess: true })
        {
            RenderFailure(selection?.Message ?? localizer.GetString("Cli.Error.SelectionFailed"), selection?.Diagnostics ?? []);
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
            RenderFailure(localizer.GetString("Cli.Error.ExportFailed"), export.Diagnostics);
            return 1;
        }

        return await WriteExportOutputAsync(request, format, info, export, cancellationToken);
    }

    private bool TryValidateRequest(CliConvertRequest request, out CliOutputFormatDefinition format, out int errorCode)
    {
        format = null!;
        errorCode = 1;
        return ValidateOutputTarget(request)
            && ValidateFrameRate(request)
            && ValidateEncoding(request)
            && ValidateFormat(request, out format)
            ? (errorCode = 0) == 0
            : false;
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
            console.Write(export.Content);
            WriteDiagnosticsToError(export.Diagnostics);
            return 0;
        }

        var targetPath = await ResolveOutputPathAsync(request, format, info, cancellationToken);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            console.WriteErrorLine(localizer.GetString("Cli.Error.OutputDirectory"));
            return 1;
        }

        if (File.Exists(targetPath) && !request.Force)
        {
            console.WriteErrorLine(localizer.Format("Cli.Error.OutputExists", new Dictionary<string, object?> { ["path"] = targetPath }));
            return 1;
        }

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
}
