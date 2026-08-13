using ChapterTool.Contracts.PlatformPorts;
using Microsoft.Extensions.Logging;

namespace ChapterTool.TestSupport;

/// <summary>
/// Builds an <see cref="ILogger{T}"/> from the application log service used in tests.
/// </summary>
public static class TestApplicationLogger
{
    /// <summary>
    /// Creates a typed logger that writes through the test log service provider.
    /// </summary>
    /// <typeparam name="T">The category type used to name the logger.</typeparam>
    public static ILogger<T> Create<T>(IApplicationLogService logService)
    {
        if (logService is not ILoggerProvider provider)
        {
            throw new InvalidOperationException("The test log service must also provide the logging pipeline sink.");
        }

        return new ProviderLogger<T>(provider);
    }

    private sealed class ProviderLogger<T>(ILoggerProvider provider) : ILogger<T>
    {
        private readonly ILogger inner = provider.CreateLogger(typeof(T).FullName ?? typeof(T).Name);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
