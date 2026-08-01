using Autofac;
using Avalonia.Controls;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Infrastructure.Platform;

namespace ChapterTool.Avalonia.Composition;

internal sealed class AvaloniaPlatformModule(AppCompositionOptions options) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AppLocalizationManager>().As<IAppLocalizer>().AsSelf().SingleInstance();
        builder.RegisterType<AvaloniaLocalizationResourceAdapter>().AsSelf().SingleInstance();
        builder.Register(_ => new AvaloniaFontFamilyCatalog()).As<IFontFamilyCatalog>().SingleInstance();
        builder.RegisterType<AvaloniaFontApplicationService>().As<IFontApplicationService>().SingleInstance();
        builder.RegisterType<AvaloniaThemeApplicationService>().As<IThemeApplicationService>().SingleInstance();
        builder.RegisterType<ShellService>().As<IShellService>().SingleInstance();
        builder.Register(_ => options.Capabilities ?? new RuntimeCapabilities(
                RuntimeSourceMode.LocalPath,
                RuntimeOutputMode.Directory,
                RuntimeSecondarySurfaceMode.NativeWindow,
                CanReadClipboard: true,
                CanWriteClipboard: true,
                CanConfigureExternalTools: true,
                CanRunExternalProcesses: true,
                CanOpenLocalPaths: true))
            .As<IRuntimeCapabilities>()
            .SingleInstance();

        builder.Register<Func<Window, IFilePickerService>>(context =>
            {
                var localizer = context.Resolve<IAppLocalizer>();
                return owner => new AvaloniaFilePickerService(owner, localizer);
            })
            .As<Func<Window, IFilePickerService>>()
            .SingleInstance();
        builder.Register<Func<Window, ISettingsPickerService>>(context =>
            {
                var localizer = context.Resolve<IAppLocalizer>();
                return owner => new AvaloniaSettingsPickerService(owner, localizer);
            })
            .As<Func<Window, ISettingsPickerService>>()
            .SingleInstance();
        builder.Register<Func<Window, IClipboardService>>(_ =>
                owner => new AvaloniaClipboardService(owner))
            .As<Func<Window, IClipboardService>>()
            .SingleInstance();
        builder.Register<Func<Control, IFilePickerService>>(context =>
            {
                var windowFactory = context.Resolve<Func<Window, IFilePickerService>>();
                return control => windowFactory(TopLevel.GetTopLevel(control) as Window
                    ?? throw new InvalidOperationException("The shared main view must be attached to a desktop window."));
            })
            .As<Func<Control, IFilePickerService>>()
            .SingleInstance();
        builder.RegisterType<AvaloniaSettingsCloseConfirmationService>()
            .As<ISettingsCloseConfirmationService>()
            .SingleInstance();
    }
}
