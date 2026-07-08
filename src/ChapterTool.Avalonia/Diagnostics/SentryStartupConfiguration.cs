using System.Globalization;
using System.Reflection;

namespace ChapterTool.Avalonia.Diagnostics;

internal sealed record SentryStartupOptions(
    bool Enabled,
    string? Dsn,
    string Environment,
    string? Release,
    string? Distribution,
    bool Debug,
    SentryLevel DiagnosticLevel,
    bool SendDefaultPii,
    double? TracesSampleRate,
    double? ProfilesSampleRate,
    string CacheDirectoryPath);

internal static class SentryStartupConfiguration
{
    internal const string DefaultDsn = "https://5f37a325bfae0275e1940fa0d2dfa5c0@o955448.ingest.us.sentry.io/4511698209931264";

    internal static SentryStartupOptions FromEnvironment(
        Func<string, string?> getEnvironmentVariable,
        Assembly applicationAssembly,
        string localApplicationDataPath,
        bool debugBuild)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        var dsn = FirstNonEmpty(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_DSN"),
            getEnvironmentVariable("SENTRY_DSN"),
            DefaultDsn);
        var enabled = ParseBoolean(
            FirstNonEmpty(getEnvironmentVariable("CHAPTERTOOL_SENTRY_ENABLED"), getEnvironmentVariable("SENTRY_ENABLED")),
            defaultValue: !string.IsNullOrWhiteSpace(dsn));
        var environment = FirstNonEmpty(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_ENVIRONMENT"),
            getEnvironmentVariable("SENTRY_ENVIRONMENT"),
            debugBuild ? "debug" : "production")!;
        var release = FirstNonEmpty(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_RELEASE"),
            getEnvironmentVariable("SENTRY_RELEASE"),
            ReleaseFromAssembly(applicationAssembly));
        var distribution = FirstNonEmpty(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_DIST"),
            getEnvironmentVariable("SENTRY_DIST"));
        var debug = ParseBoolean(getEnvironmentVariable("CHAPTERTOOL_SENTRY_DEBUG"), defaultValue: false);
        var diagnosticLevel = ParseSentryLevel(getEnvironmentVariable("CHAPTERTOOL_SENTRY_DIAGNOSTIC_LEVEL"), defaultValue: SentryLevel.Warning);
        var sendDefaultPii = ParseBoolean(getEnvironmentVariable("CHAPTERTOOL_SENTRY_SEND_DEFAULT_PII"), defaultValue: true);
        var tracesSampleRate = ParseSampleRate(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_TRACES_SAMPLE_RATE"),
            defaultValue: debugBuild ? null : 0.1d);
        var profilesSampleRate = ParseSampleRate(getEnvironmentVariable("CHAPTERTOOL_SENTRY_PROFILES_SAMPLE_RATE"), defaultValue: null);
        var cacheDirectoryPath = FirstNonEmpty(
            getEnvironmentVariable("CHAPTERTOOL_SENTRY_CACHE_DIR"),
            DefaultCacheDirectory(localApplicationDataPath))!;

        return new SentryStartupOptions(
            enabled,
            dsn,
            environment,
            release,
            distribution,
            debug,
            diagnosticLevel,
            sendDefaultPii,
            tracesSampleRate,
            profilesSampleRate,
            cacheDirectoryPath);
    }

    private static string? ReleaseFromAssembly(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = FirstNonEmpty(informationalVersion, assembly.GetName().Version?.ToString(3));
        return string.IsNullOrWhiteSpace(version) ? null : $"ChapterTool@{version}";
    }

    private static string DefaultCacheDirectory(string localApplicationDataPath) =>
        string.IsNullOrWhiteSpace(localApplicationDataPath)
            ? Path.Combine(AppContext.BaseDirectory, "sentry")
            : Path.Combine(localApplicationDataPath, "ChapterTool", "sentry");

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue
        };
    }

    private static double? ParseSampleRate(string? value, double? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return defaultValue;
        }

        return parsed is >= 0 and <= 1 ? parsed : defaultValue;
    }

    private static SentryLevel ParseSentryLevel(string? value, SentryLevel defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return Enum.TryParse<SentryLevel>(value, ignoreCase: true, out var level) ? level : defaultValue;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
