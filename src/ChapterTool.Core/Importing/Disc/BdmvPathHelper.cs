using ChapterTool.Core.Diagnostics;

namespace ChapterTool.Core.Importing.Disc;

internal static class BdmvPathHelper
{
    internal static string? FindBdmvRoot(string mplsPath)
    {
        if (string.IsNullOrWhiteSpace(mplsPath) || !File.Exists(mplsPath))
        {
            return null;
        }

        try
        {
            var dir = Path.GetDirectoryName(mplsPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, "BDMV", "CLIPINF")) &&
                    Directory.Exists(Path.Combine(dir, "BDMV", "PLAYLIST")))
                {
                    return dir;
                }

                var parent = Path.GetDirectoryName(dir);
                if (parent == dir || parent == null)
                {
                    break;
                }

                dir = parent;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    internal static string? GetClpiPath(string bdmvRoot, string clipName) =>
        string.IsNullOrWhiteSpace(bdmvRoot) || string.IsNullOrWhiteSpace(clipName)
            ? null
            : Path.Combine(bdmvRoot, "BDMV", "CLIPINF", $"{clipName}.clpi");

    internal static IReadOnlyDictionary<string, Clpi.ClpiFile> DiscoverClpiFiles(
        string bdmvRoot,
        IEnumerable<string> clipNames,
        List<ChapterDiagnostic>? diagnostics = null)
    {
        var result = new Dictionary<string, Clpi.ClpiFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var clipName in clipNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var clpiPath = GetClpiPath(bdmvRoot, clipName);
            if (clpiPath == null || !File.Exists(clpiPath))
            {
                diagnostics?.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.ClpiFileNotFound,
                    $"CLPI file not found for clip '{clipName}', skipping.",
                    clpiPath));
                continue;
            }

            var clpi = Clpi.ClpiFile.TryRead(clpiPath, out var error);
            if (clpi != null)
            {
                result[clipName] = clpi;
                diagnostics?.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Info,
                    ChapterDiagnosticCode.ClpiFileLoaded,
                    $"Loaded CLPI for '{clipName}': " +
                    $"stream_type={clpi.ClipInfo.ClipStreamType}, " +
                    $"duration={clpi.ClipInfo.DurationFromPackets}, " +
                    $"cc5={clpi.ClipInfo.IsCC5}, " +
                    $"atc_sequences={clpi.SequenceInfo?.ATCSequences.Count ?? 0}",
                    clpiPath));
            }
            else
            {
                diagnostics?.Add(new ChapterDiagnostic(
                    DiagnosticSeverity.Warning,
                    ChapterDiagnosticCode.ClpiParseFailed,
                    $"Failed to parse CLPI for '{clipName}': {error}",
                    clpiPath,
                    error));
            }
        }

        return result;
    }

    internal static string? GetIndexPath(string bdmvRoot) =>
        string.IsNullOrWhiteSpace(bdmvRoot) ? null : Path.Combine(bdmvRoot, "BDMV", "index.bdmv");

    internal static string? GetMetaXmlPath(string bdmvRoot)
    {
        if (string.IsNullOrWhiteSpace(bdmvRoot))
        {
            return null;
        }

        try
        {
            var meta = Path.Combine(bdmvRoot, "BDMV", "META", "DL");
            return Directory.Exists(meta)
                ? Directory.EnumerateFiles(meta, "*.xml").FirstOrDefault()
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
