using Autofac;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Importing.Runtime;
using ChapterTool.Infrastructure.Platform;
using ChapterTool.Infrastructure.Processes;
using ChapterTool.Infrastructure.Services;
using ChapterTool.Infrastructure.Tools;

namespace ChapterTool.Avalonia.Composition;

internal sealed class InfrastructureModule(string settingsDirectory) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(_ => new ChapterToolSettingsStore(settingsDirectory))
            .As<ChapterToolSettingsStore>()
            .As<ISettingsStore<ChapterToolSettings>>()
            .SingleInstance();
        builder.Register(context => new ExternalToolLocator(
                context.Resolve<ISettingsStore<ChapterToolSettings>>(),
                [.. ChapterToolRuntimeComposition.PathSearchDirectories()]))
            .As<ExternalToolLocator>()
            .As<IExternalToolLocator>()
            .SingleInstance();
        builder.RegisterType<ProcessRunner>().As<IProcessRunner>().SingleInstance();
        builder.Register(context => new FileSystemNativeDependencyService(
                [.. ChapterToolRuntimeComposition.PathSearchDirectories().Prepend(AppContext.BaseDirectory)]))
            .As<INativeDependencyService>()
            .SingleInstance();
    }
}
