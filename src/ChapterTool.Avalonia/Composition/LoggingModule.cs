using Autofac;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ChapterTool.Avalonia.Composition;

internal sealed class LoggingModule(string settingsDirectory) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(_ => new ApplicationLogPanelProvider(capacity: 500, minimumLevel: LogLevel.Information))
            .As<IApplicationLogService>()
            .As<ILoggerProvider>()
            .SingleInstance();
        builder.Register(_ => CreateSerilogLogger(settingsDirectory))
            .As<Logger>()
            .SingleInstance()
            .ExternallyOwned();
        builder.Register(context => LoggerFactory.Create(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddSerilog(context.Resolve<Logger>(), dispose: true);
                logging.AddProvider(context.Resolve<ILoggerProvider>());
            }))
            .As<ILoggerFactory>()
            .SingleInstance();
        builder.Register(context => context.Resolve<ILoggerFactory>().CreateLogger<UI.ViewModels.MainWindowViewModel>())
            .As<ILogger<UI.ViewModels.MainWindowViewModel>>()
            .SingleInstance();
    }

    private static Logger CreateSerilogLogger(string settingsDirectory)
    {
        var logDirectory = Path.Combine(settingsDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "chaptertool-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }
}
