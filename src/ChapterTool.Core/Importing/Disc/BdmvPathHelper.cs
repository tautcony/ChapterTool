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
        var requestedClips = clipNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var loadedClips = new List<object?>();
        var missingClips = new List<object?>();
        foreach (var clipName in requestedClips)
        {
            var clpiPath = GetClpiPath(bdmvRoot, clipName);
            if (clpiPath == null || !File.Exists(clpiPath))
            {
                missingClips.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["clip"] = clipName,
                    ["path"] = clpiPath
                });
                continue;
            }

            var clpi = Clpi.ClpiFile.TryRead(clpiPath, out var error);
            if (clpi != null)
            {
                result[clipName] = clpi;
                loadedClips.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["clip"] = clipName,
                    ["path"] = clpiPath,
                    ["streamType"] = clpi.ClipInfo.ClipStreamType,
                    ["duration"] = clpi.ClipInfo.DurationFromPackets,
                    ["cc5"] = clpi.ClipInfo.IsCC5,
                    ["atcSequences"] = clpi.SequenceInfo?.ATCSequences.Count ?? 0
                });
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

        if (diagnostics != null && loadedClips.Count > 0)
        {
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.ClpiFileLoaded,
                $"Loaded {loadedClips.Count} CLPI files for {requestedClips.Count} unique clips.",
                Path.Combine(bdmvRoot, "BDMV", "CLIPINF"),
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["loadedCount"] = loadedClips.Count,
                    ["requestedCount"] = requestedClips.Count,
                    ["clips"] = loadedClips
                }));
        }

        if (diagnostics != null && missingClips.Count > 0)
        {
            diagnostics.Add(new ChapterDiagnostic(
                DiagnosticSeverity.Info,
                ChapterDiagnosticCode.ClpiFileNotFound,
                $"CLPI files were not found for {missingClips.Count} of {requestedClips.Count} unique clips.",
                Path.Combine(bdmvRoot, "BDMV", "CLIPINF"),
                Arguments: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["missingCount"] = missingClips.Count,
                    ["requestedCount"] = requestedClips.Count,
                    ["clips"] = missingClips
                }));
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
