using Avalonia.Controls;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.ViewModels.Tools;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Core.Transform;
using ChapterTool.Infrastructure.Configuration;
using ChapterTool.Infrastructure.Services;

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
}

/// <summary>Descriptor for a secondary tool window.</summary>
public sealed record ToolWindowRegistration(
    string Id,
    string TitleResourceKey,
    Func<ToolWindowCreateContext, Control> CreateContent,
    double PreferredWidth = 620);

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
                    new TextToolOptions { FormatSelector = new TextToolFormatSelector(context.Owner.PortAdapters.ExportPreferences) })
            },
            PreferredWidth: 760),
        new(
            "log",
            "Tool.Log.Title",
            context => new TextToolView
            {
                DataContext = new TextToolViewModel(
                    context.Owner.LogText,
                    new TextToolOptions
                    {
                        ClearAction = context.Owner.ClearLog,
                        LiveRefreshService = context.Owner.LogService
                    })
            },
            PreferredWidth: 760),
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
            PreferredWidth: 760),
        new(
            "language",
            "Tool.Language.Title",
            context => new LanguageToolView { DataContext = new LanguageToolViewModel(context.Owner.PortAdapters.Preferences) }),
        new(
            "expression",
            "Tool.Expression.Title",
            context => new ExpressionToolView
            {
                DataContext = new ExpressionToolViewModel(
                    context.Owner.PortAdapters.Expression,
                    new AvaloniaFilePickerService(context.HostWindow, context.Localizer),
                    context.ExpressionAuthoringService)
            }),
        new(
            "template-names",
            "Tool.TemplateNames.Title",
            context => new TemplateNamesToolView { DataContext = new TemplateNamesToolViewModel(context.Owner.PortAdapters.NamingPreferences) }),
        new(
            "zones",
            "Tool.Zones.Title",
            context => new TextToolView { DataContext = new TextToolViewModel(context.Owner.CreateZonesText) }),
        new(
            "forward-shift",
            "Tool.ForwardShift.Title",
            context => new ForwardShiftToolView { DataContext = new ForwardShiftToolViewModel(context.Owner.PortAdapters.ChapterEdit) }),
    ];

    public static ToolWindowRegistration? Find(string id) =>
        DefaultRegistrations.FirstOrDefault(registration =>
            string.Equals(registration.Id, id, StringComparison.OrdinalIgnoreCase));
}
