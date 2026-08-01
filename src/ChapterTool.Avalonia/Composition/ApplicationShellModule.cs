using Autofac;
using Avalonia.Controls;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views;
using ChapterTool.Avalonia.Views;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.Composition;

internal sealed class ApplicationShellModule(AppCompositionOptions options) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(context => new WorkspaceHostServices(
                context.Resolve<IChapterLoadService>(),
                context.Resolve<IChapterSaveService>(),
                context.Resolve<IChapterEditingService>(),
                context.Resolve<ChapterSegmentService>(),
                context.Resolve<IChapterTimeFormatter>(),
                context.Resolve<IFrameRateService>(),
                context.Resolve<IChapterExpressionEngine>(),
                context.Resolve<ChapterExportService>(),
                context.Resolve<IExpressionAuthoringService>()))
            .AsSelf().SingleInstance();
        builder.Register(context => new HostEffectServices(
                context.Resolve<IApplicationLogService>(),
                context.Resolve<Microsoft.Extensions.Logging.ILogger<MainWindowViewModel>>(),
                context.Resolve<IShellService>()))
            .AsSelf().SingleInstance();
        builder.Register(context => new SettingsAppearanceServices(
                context.Resolve<ISettingsStore<ChapterToolSettings>>(),
                context.Resolve<IThemeApplicationService>(),
                context.Resolve<IFontFamilyCatalog>(),
                context.Resolve<IFontApplicationService>(),
                context.Resolve<IExternalToolLocator>(),
                options.SettingsDirectory ?? throw new InvalidOperationException("Settings directory was not resolved.")))
            .AsSelf().SingleInstance();
        builder.Register(context => new LocalizationServices(context.Resolve<IAppLocalizer>())).AsSelf().SingleInstance();
        builder.Register(context => new RuntimeHostServices(context.Resolve<IRuntimeCapabilities>())).AsSelf().SingleInstance();
        builder.Register(context => new AuxiliaryToolHostServices(
                context.Resolve<IAuxiliaryToolHost>(),
                context.Resolve<IEmbeddedToolPresenter>()))
            .AsSelf().SingleInstance();

        builder.Register(context => new AvaloniaHostComposition(
                context.Resolve<WorkspaceHostServices>(),
                context.Resolve<HostEffectServices>(),
                context.Resolve<SettingsAppearanceServices>(),
                context.Resolve<LocalizationServices>(),
                context.Resolve<RuntimeHostServices>(),
                context.Resolve<AuxiliaryToolHostServices>()))
            .AsSelf().SingleInstance();
        builder.Register(context => new MainWindowViewModel(context.Resolve<AvaloniaHostComposition>()))
            .AsSelf().SingleInstance();
        builder.Register(context => new MainView(
                context.Resolve<MainWindowViewModel>(),
                context.Resolve<Func<Control, IFilePickerService>>(),
                context.Resolve<IEmbeddedToolPresenter>()))
            .AsSelf().SingleInstance();
        builder.Register(context => new MainWindow(
                context.Resolve<MainView>(),
                $"{context.Resolve<IAppLocalizer>().GetString("App.Title")} v{typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}"))
            .AsSelf().SingleInstance();
    }
}
