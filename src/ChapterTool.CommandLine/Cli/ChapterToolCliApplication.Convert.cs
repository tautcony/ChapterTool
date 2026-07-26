using System.Text;
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
}
