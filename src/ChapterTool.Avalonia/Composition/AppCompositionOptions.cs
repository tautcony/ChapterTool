using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Composition;

/// <summary>Defines host values and test replacements for the desktop graph.</summary>
public sealed record AppCompositionOptions
{
    /// <summary>Gets a value indicating whether disables production modules for an intentionally incomplete test graph.</summary>
    public bool RegisterProductionModules { get; init; } = true;

    public string? StartupPath { get; init; }

    public string? SettingsDirectory { get; init; }

    public IRuntimeCapabilities? Capabilities { get; init; }

    public IExpressionAuthoringService? ExpressionAuthoringService { get; init; }

    public Action<Autofac.ContainerBuilder>? ConfigureOverrides { get; init; }
}
