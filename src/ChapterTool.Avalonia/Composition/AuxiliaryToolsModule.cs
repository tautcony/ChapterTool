using Autofac;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.PlatformPorts;

namespace ChapterTool.Avalonia.Composition;

internal sealed class AuxiliaryToolsModule(string settingsDirectory) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(context => StandardToolCatalogFactory.Create(context.ResolveOptional<IApplicationLogExporter>())).As<IToolCatalog>().SingleInstance();
        builder.RegisterType<NoContentEmbeddedToolPresenter>().As<IEmbeddedToolPresenter>().SingleInstance();
        builder.RegisterType<AvaloniaWindowService>()
            .WithParameter("settingsDirectory", settingsDirectory)
            .As<IAuxiliaryToolHost>()
            .AsSelf()
            .SingleInstance();
        builder.Register(_ => new UnavailableSettingsCloseConfirmationPort())
            .As<ISettingsCloseConfirmationPort>()
            .SingleInstance();
    }
}
