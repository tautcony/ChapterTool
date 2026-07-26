using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Infrastructure.Configuration;

namespace ChapterTool.CommandLine.Cli;

/// <summary>
/// Command-line output path resolution helpers.
/// </summary>
public sealed partial class ChapterToolCliApplication
{
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
}
