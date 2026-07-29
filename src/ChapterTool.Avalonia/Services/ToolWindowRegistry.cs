using Avalonia.Controls;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.ViewModels.Tools;
using ChapterTool.Avalonia.UI.Views.Tools;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Services;

/// <summary>Context supplied when creating tool window content.</summary>
public sealed class ToolWindowCreateContext
{
    public required Window HostWindow { get; init; }

    public required MainWindowViewModel Owner { get; init; }

    public required IAppLocalizer Localizer { get; init; }

    public ISettingsStore<ChapterToolSettings>? SettingsStore { get; init; }

    public IThemeApplicationService? ThemeApplicationService { get; init; }

    public IFontFamilyCatalog? FontFamilyCatalog { get; init; }

    public IFontApplicationService? FontApplicationService { get; init; }

    public Func<Window, ISettingsPickerService>? SettingsPickerFactory { get; init; }

    public IExternalToolLocator? ExternalToolLocator { get; init; }

    public IShellService? ShellService { get; init; }

    public string? SettingsDirectory { get; init; }

    public IExpressionAuthoringService? ExpressionAuthoringService { get; init; }

    public IClipboardService? ClipboardService { get; init; }
}

/// <summary>Descriptor for a secondary tool window.</summary>
public sealed record ToolWindowRegistration(
    string Id,
    string TitleResourceKey,
    Func<ToolWindowCreateContext, Control> CreateContent,
    double PreferredWidth = 620,
    double PreferredHeight = 460,
    double MinWidth = 420,
    double MinHeight = 280);

/// <summary>Registration table for tool windows (replaces string-id switch soup).</summary>
public static class ToolWindowRegistry
{
    public static IReadOnlyList<ToolWindowRegistration> DefaultRegistrations { get; } =
    [
        new(
            "preview",
            "Tool.Preview.Title",
            context => new TextToolView
            {
                DataContext = new TextToolViewModel(
                    context.Owner.BuildPreview,
                    new TextToolOptions
                    {
                        FormatSelector = new TextToolFormatSelector(context.Owner.PortAdapters.ExportPreferences),
                        ErrorHandler = context.Owner.ReportUnexpectedUiException
                    })
            },
            PreferredWidth: 800,
            PreferredHeight: 500,
            MinWidth: 560,
            MinHeight: 360),
        new(
            "log",
            "Tool.Log.Title",
            context => new LogToolView
            {
                DataContext = new LogToolViewModel(
                    context.Owner.LogService,
                    context.Localizer,
                    context.ClipboardService),
            },
            PreferredWidth: 800,
            PreferredHeight: 500,
            MinWidth: 560,
            MinHeight: 360),
        new(
            "settings",
            "Tool.Settings.Title",
            context => new SettingsToolView
            {
                DataContext = new SettingsToolViewModel(
                    context.Owner.PortAdapters.Preferences,
                    context.SettingsStore,
                    context.Localizer,
                    context.SettingsPickerFactory?.Invoke(context.HostWindow),
                    context.ExternalToolLocator,
                    context.ThemeApplicationService,
                    context.ShellService,
                    context.FontFamilyCatalog,
                    context.FontApplicationService,
                    context.SettingsDirectory)
            },
            PreferredWidth: 600,
            PreferredHeight: 560,
            MinWidth: 600,
            MinHeight: 420),
        new(
            "language",
            "Tool.Language.Title",
            context => new LanguageToolView { DataContext = new LanguageToolViewModel(context.Owner.PortAdapters.Preferences) },
            PreferredWidth: 520,
            PreferredHeight: 220,
            MinHeight: 180),
        new(
            "expression",
            "Tool.Expression.Title",
            context => new ExpressionToolView
            {
                DataContext = new ExpressionToolViewModel(
                    context.Owner.PortAdapters.Expression,
                    new AvaloniaFilePickerService(context.HostWindow, context.Localizer),
                    context.ExpressionAuthoringService)
            },
            PreferredWidth: 680,
            PreferredHeight: 420,
            MinWidth: 520,
            MinHeight: 320),
        new(
            "template-names",
            "Tool.TemplateNames.Title",
            context => new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(context.Owner.PortAdapters.NamingPreferences) },
            PreferredWidth: 520,
            PreferredHeight: 220,
            MinHeight: 180),
        new(
            "zones",
            "Tool.Zones.Title",
            context => new TextToolView
            {
                DataContext = new TextToolViewModel(
                    context.Owner.CreateZonesText,
                    new TextToolOptions { ErrorHandler = context.Owner.ReportUnexpectedUiException })
            }),
        new(
            "forward-shift",
            "Tool.ForwardShift.Title",
            context => new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(context.Owner.PortAdapters.ChapterEdit) },
            PreferredWidth: 520,
            PreferredHeight: 220,
            MinHeight: 180),
    ];

    public static ToolWindowRegistration? Find(string id) =>
        DefaultRegistrations.FirstOrDefault(registration =>
            string.Equals(registration.Id, id, StringComparison.OrdinalIgnoreCase));
}
