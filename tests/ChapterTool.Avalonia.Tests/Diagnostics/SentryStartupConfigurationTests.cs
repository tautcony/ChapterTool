using ChapterTool.Avalonia.Diagnostics;

namespace ChapterTool.Avalonia.Tests.Diagnostics;

public sealed class SentryStartupConfigurationTests
{
    [Fact]
    public void FromEnvironmentUsesSafeDefaults()
    {
        var options = SentryStartupConfiguration.FromEnvironment(
            _ => null,
            typeof(SentryStartupConfiguration).Assembly,
            "/tmp/local",
            debugBuild: false);

        Assert.True(options.Enabled);
        Assert.Equal(SentryStartupConfiguration.DefaultDsn, options.Dsn);
        Assert.Equal("production", options.Environment);
        Assert.StartsWith("ChapterTool@23.0.0", options.Release, StringComparison.Ordinal);
        Assert.False(options.Debug);
        Assert.Equal(SentryLevel.Warning, options.DiagnosticLevel);
        Assert.True(options.SendDefaultPii);
        Assert.Equal(0.1d, options.TracesSampleRate);
        Assert.Null(options.ProfilesSampleRate);
        Assert.Equal(Path.Combine("/tmp/local", "ChapterTool", "sentry"), options.CacheDirectoryPath);
    }

    [Fact]
    public void FromEnvironmentAllowsChapterToolOverrides()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["CHAPTERTOOL_SENTRY_ENABLED"] = "off",
            ["CHAPTERTOOL_SENTRY_DSN"] = "https://example.test/1",
            ["CHAPTERTOOL_SENTRY_ENVIRONMENT"] = "staging",
            ["CHAPTERTOOL_SENTRY_RELEASE"] = "ChapterTool@test",
            ["CHAPTERTOOL_SENTRY_DIST"] = "macos-arm64",
            ["CHAPTERTOOL_SENTRY_DEBUG"] = "true",
            ["CHAPTERTOOL_SENTRY_DIAGNOSTIC_LEVEL"] = "Error",
            ["CHAPTERTOOL_SENTRY_SEND_DEFAULT_PII"] = "yes",
            ["CHAPTERTOOL_SENTRY_TRACES_SAMPLE_RATE"] = "0.25",
            ["CHAPTERTOOL_SENTRY_PROFILES_SAMPLE_RATE"] = "0.5",
            ["CHAPTERTOOL_SENTRY_CACHE_DIR"] = "/tmp/sentry"
        };

        var options = SentryStartupConfiguration.FromEnvironment(
            name => values.GetValueOrDefault(name),
            typeof(SentryStartupConfiguration).Assembly,
            "/tmp/local",
            debugBuild: false);

        Assert.False(options.Enabled);
        Assert.Equal("https://example.test/1", options.Dsn);
        Assert.Equal("staging", options.Environment);
        Assert.Equal("ChapterTool@test", options.Release);
        Assert.Equal("macos-arm64", options.Distribution);
        Assert.True(options.Debug);
        Assert.Equal(SentryLevel.Error, options.DiagnosticLevel);
        Assert.True(options.SendDefaultPii);
        Assert.Equal(0.25d, options.TracesSampleRate);
        Assert.Equal(0.5d, options.ProfilesSampleRate);
        Assert.Equal("/tmp/sentry", options.CacheDirectoryPath);
    }

    [Fact]
    public void FromEnvironmentFallsBackWhenSampleRatesAreInvalid()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["CHAPTERTOOL_SENTRY_TRACES_SAMPLE_RATE"] = "2",
            ["CHAPTERTOOL_SENTRY_PROFILES_SAMPLE_RATE"] = "abc"
        };

        var options = SentryStartupConfiguration.FromEnvironment(
            name => values.GetValueOrDefault(name),
            typeof(SentryStartupConfiguration).Assembly,
            "/tmp/local",
            debugBuild: false);

        Assert.Equal(0.1d, options.TracesSampleRate);
        Assert.Null(options.ProfilesSampleRate);
    }
}
