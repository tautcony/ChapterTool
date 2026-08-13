using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;

namespace ChapterTool.Avalonia.Services;

/// <summary>Creates the standard desktop catalog as an ordinary injected value.</summary>
public static class StandardToolCatalogFactory
{
    public static IToolCatalog Create()
    {
        return new ToolCatalog(
        [
            new ToolDescriptor(
                ToolIds.Preview,
                "Tool.Preview.Title",
                new ToolSizeConstraints(800, 500, 560, 360),
                ToolRefreshPolicy.Reuse,
                context => new TextToolView
                {
                    DataContext = new TextToolViewModel(
                        context.Session.BuildPreview,
                        new TextToolOptions
                        {
                            FormatSelector = new TextToolFormatSelector(context.Session.ExportPreferences),
                            ErrorHandler = context.Session.ReportUnexpectedUiException
                        })
                }),
            new ToolDescriptor(
                ToolIds.Log,
                "Tool.Log.Title",
                new ToolSizeConstraints(800, 500, 560, 360),
                ToolRefreshPolicy.Reuse,
                context => new LogToolView
                {
                    DataContext = new LogToolViewModel(
                        context.Session.LogService,
                        context.Localizer,
                        context.Clipboard)
                }),
            new ToolDescriptor(
                ToolIds.Settings,
                "Tool.Settings.Title",
                new ToolSizeConstraints(600, 560, 600, 420),
                ToolRefreshPolicy.Reuse,
                context => new SettingsToolView
                {
                    DataContext = new SettingsToolViewModel(
                        context.Session.Preferences,
                        context.SettingsStore,
                        context.Localizer,
                        context.SettingsPicker,
                        context.ExternalToolLocator,
                        context.ThemeApplicationService,
                        context.ShellService,
                        context.FontFamilyCatalog,
                        context.FontApplicationService,
                        context.SettingsDirectory,
                        context.Capabilities,
                        unexpectedErrorHandler: context.Session.ReportUnexpectedUiException)
                },
                RequiresCloseConfirmation: true),
            new ToolDescriptor(
                ToolIds.Language,
                "Tool.Language.Title",
                new ToolSizeConstraints(520, 220, 420, 180),
                ToolRefreshPolicy.Reuse,
                context => new LanguageToolView
                {
                    DataContext = new LanguageToolViewModel(
                        context.Session.Preferences,
                        context.Session.ReportUnexpectedUiException)
                }),
            new ToolDescriptor(
                ToolIds.Expression,
                "Tool.Expression.Title",
                new ToolSizeConstraints(680, 420, 520, 320),
                ToolRefreshPolicy.Reuse,
                context => new ExpressionToolView
                {
                    DataContext = new ExpressionToolViewModel(
                        context.Session.Expression,
                        context.FilePicker,
                        context.ExpressionAuthoringService,
                        context.Session.ReportUnexpectedUiException)
                }),
            new ToolDescriptor(
                ToolIds.TemplateNames,
                "Tool.TemplateNames.Title",
                new ToolSizeConstraints(520, 220, 420, 180),
                ToolRefreshPolicy.Reuse,
                context => new TemplateNamesToolView
                {
                    DataContext = new TemplateNamesToolViewModel(context.Session.NamingPreferences)
                }),
            new ToolDescriptor(
                ToolIds.Zones,
                "Tool.Zones.Title",
                new ToolSizeConstraints(),
                ToolRefreshPolicy.RefreshRequest,
                context => new TextToolView
                {
                    DataContext = new TextToolViewModel(
                        context.Session.CreateZonesText,
                        new TextToolOptions { ErrorHandler = context.Session.ReportUnexpectedUiException })
                }),
            new ToolDescriptor(
                ToolIds.ForwardShift,
                "Tool.ForwardShift.Title",
                new ToolSizeConstraints(520, 220, 420, 180),
                ToolRefreshPolicy.RefreshRequest,
                context => new ForwardShiftToolView
                {
                    DataContext = new ForwardShiftToolViewModel(
                        context.Session.ChapterEdit,
                        context.Session.ReportUnexpectedUiException)
                })
        ]);
    }
}
