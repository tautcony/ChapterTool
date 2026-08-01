using Autofac;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;
using ChapterTool.Core.Transform.Expressions.Lua;
using ChapterTool.Infrastructure.Importing.Media;
using ChapterTool.Infrastructure.Importing.Runtime;

namespace ChapterTool.Avalonia.Composition;

internal sealed class WorkspaceModule(AppCompositionOptions options) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ChapterTimeFormatter>().As<IChapterTimeFormatter>().SingleInstance();
        builder.RegisterType<FrameRateService>().As<IFrameRateService>().SingleInstance();
        builder.RegisterType<LuaExpressionScriptService>().As<IChapterExpressionEngine>().SingleInstance();
        builder.Register(context => options.ExpressionAuthoringService
                ?? new ExpressionAuthoringService(context.Resolve<IChapterExpressionEngine>()))
            .As<IExpressionAuthoringService>()
            .SingleInstance();
        builder.RegisterType<ChapterExportService>().AsSelf().SingleInstance();
        builder.RegisterType<ChapterEditingService>().As<IChapterEditingService>().SingleInstance();
        builder.RegisterType<ChapterSegmentService>().AsSelf().SingleInstance();
        builder.RegisterType<FfprobeMediaChapterReader>().AsSelf().SingleInstance();
        builder.RegisterType<AtlMp4ChapterReader>().AsSelf().SingleInstance();
        builder.Register(context => new RuntimeChapterImporterRegistry(
                context.Resolve<IChapterTimeFormatter>(),
                context.Resolve<IExternalToolLocator>(),
                context.Resolve<ChapterTool.Infrastructure.Services.IProcessRunner>(),
                context.Resolve<FfprobeMediaChapterReader>(),
                context.Resolve<AtlMp4ChapterReader>()))
            .As<IChapterImporterRegistry>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<RuntimeChapterLoadService>().As<IChapterLoadService>().SingleInstance();
        builder.RegisterType<RuntimeChapterSaveService>().As<IChapterSaveService>().SingleInstance();
    }
}
