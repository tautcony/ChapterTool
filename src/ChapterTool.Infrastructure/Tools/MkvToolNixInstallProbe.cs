using Microsoft.Win32;
using System.Runtime.Versioning;

namespace ChapterTool.Infrastructure.Tools;

public interface IMkvToolNixInstallProbe
{
    IEnumerable<string> FindMkvExtractCandidates(string executableName);
}

public static class MkvToolNixInstallProbe
{
    public static IMkvToolNixInstallProbe CreateDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsMkvToolNixInstallProbe(new WindowsRegistryInstallProbe());
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacMkvToolNixInstallProbe(["/Applications"]);
        }

        return new UnixMkvToolNixInstallProbe();
    }
}

public sealed class UnixMkvToolNixInstallProbe : IMkvToolNixInstallProbe
{
    public IEnumerable<string> FindMkvExtractCandidates(string executableName) => [];
}

public sealed class MacMkvToolNixInstallProbe(
    IReadOnlyList<string> applicationRoots,
    bool? enabled = null) : IMkvToolNixInstallProbe
{
    public IEnumerable<string> FindMkvExtractCandidates(string executableName)
    {
        if (enabled is false || (enabled is null && !OperatingSystem.IsMacOS()))
        {
            yield break;
        }

        foreach (var root in applicationRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var appDirectory in Directory.EnumerateDirectories(root, "MKVToolNix*.app").OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                yield return Path.Combine(appDirectory, "Contents", "MacOS", executableName);
            }
        }
    }
}

public sealed class WindowsMkvToolNixInstallProbe(
    IWindowsRegistryInstallProbe registry,
    bool? enabled = null) : IMkvToolNixInstallProbe
{
    public IEnumerable<string> FindMkvExtractCandidates(string executableName)
    {
        if (enabled is false || (enabled is null && !OperatingSystem.IsWindows()))
        {
            yield break;
        }

        foreach (var value in registry.ReadMkvToolNixInstallValues())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalizedValue = NormalizeRegistryPath(value);
            var directory = Directory.Exists(normalizedValue)
                ? normalizedValue
                : Path.GetDirectoryName(normalizedValue);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return Path.Combine(directory, executableName);
            }
        }
    }

    private static string NormalizeRegistryPath(string value) =>
        TrimDisplayIconSuffix(value).Trim().Trim('"');

    private static string TrimDisplayIconSuffix(string value)
    {
        var comma = value.LastIndexOf(',');
        return comma > 0 && value[(comma + 1)..].All(char.IsDigit)
            ? value[..comma]
            : value;
    }
}

public interface IWindowsRegistryInstallProbe
{
    IEnumerable<string> ReadMkvToolNixInstallValues();
}

public sealed class WindowsRegistryInstallProbe : IWindowsRegistryInstallProbe
{
    public IEnumerable<string> ReadMkvToolNixInstallValues()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        foreach (var value in ReadWindowsValues())
        {
            yield return value;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ReadWindowsValues()
    {
        foreach (var (hive, view, path) in UninstallKeys)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            if (key is null)
            {
                continue;
            }

            foreach (var name in new[] { "InstallLocation", "DisplayIcon" })
            {
                if (key.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static (RegistryHive Hive, RegistryView View, string Path)[] UninstallKeys =>
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix"),
        (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix"),
        (RegistryHive.CurrentUser, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix")
    ];
}
