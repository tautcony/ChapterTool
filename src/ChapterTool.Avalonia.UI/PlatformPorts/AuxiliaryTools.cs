using Avalonia.Controls;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

/// <summary>Identifies one auxiliary tool independently of its presentation host.</summary>
public readonly record struct ToolId
{
    public ToolId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A tool identifier is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public bool Equals(ToolId other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator ToolId(string value) => new(value);

    public static implicit operator string(ToolId id) => id.Value;
}

public static class ToolIds
{
    public static readonly ToolId Preview = new("preview");
    public static readonly ToolId Log = new("log");
    public static readonly ToolId Settings = new("settings");
    public static readonly ToolId Language = new("language");
    public static readonly ToolId Expression = new("expression");
    public static readonly ToolId TemplateNames = new("template-names");
    public static readonly ToolId Zones = new("zones");
    public static readonly ToolId ForwardShift = new("forward-shift");
}

public enum ToolRefreshPolicy
{
    Reuse,
    RefreshRequest
}

public enum AuxiliaryToolResultKind
{
    Opened,
    Activated,
    Closed,
    Unknown,
    Unavailable
}

public sealed record AuxiliaryToolResult(AuxiliaryToolResultKind Kind, ToolId ToolId)
{
    public static AuxiliaryToolResult Unknown(ToolId id) => new(AuxiliaryToolResultKind.Unknown, id);
}

/// <summary>Typed data supplied when the shell opens an auxiliary tool.</summary>
public sealed record AuxiliaryToolRequest(
    IWorkspaceToolSession Session,
    IAppLocalizer Localizer,
    IRuntimeCapabilities Capabilities,
    Window? HostWindow = null,
    IFilePickerService? FilePicker = null,
    IClipboardService? Clipboard = null);

public interface IAuxiliaryToolHost : IDisposable
{
    ValueTask<AuxiliaryToolResult> OpenAsync(
        ToolId toolId,
        AuxiliaryToolRequest request,
        CancellationToken cancellationToken);

    ValueTask<AuxiliaryToolResult> CloseAsync(ToolId toolId, CancellationToken cancellationToken);
}

public interface IEmbeddedToolPresenter
{
    Control? Content { get; }

    ToolId? ToolId { get; }

    event EventHandler? ContentChanged;
}

public sealed class NoContentEmbeddedToolPresenter : IEmbeddedToolPresenter
{
    public Control? Content => null;

    public ToolId? ToolId => null;

    public event EventHandler? ContentChanged
    {
        add { }
        remove { }
    }
}

public interface ISettingsCloseConfirmationPort
{
    ValueTask<SettingsCloseAction> ConfirmCloseAsync(ToolId toolId, CancellationToken cancellationToken);
}

public sealed class UnavailableSettingsCloseConfirmationPort : ISettingsCloseConfirmationPort
{
    public ValueTask<SettingsCloseAction> ConfirmCloseAsync(ToolId toolId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(SettingsCloseAction.Cancel);
}

public sealed record ToolSizeConstraints(
    double PreferredWidth = 620,
    double PreferredHeight = 460,
    double MinWidth = 420,
    double MinHeight = 280);

public sealed record ToolCreationContext(
    IWorkspaceToolSession Session,
    IAppLocalizer Localizer,
    ISettingsStore<ChapterToolSettings> SettingsStore,
    IThemeApplicationService ThemeApplicationService,
    ISettingsPickerService SettingsPicker,
    IExternalToolLocator ExternalToolLocator,
    IShellService ShellService,
    IFontFamilyCatalog FontFamilyCatalog,
    IFontApplicationService FontApplicationService,
    string SettingsDirectory,
    IExpressionAuthoringService ExpressionAuthoringService,
    IClipboardService Clipboard,
    Window? HostWindow = null,
    IFilePickerService? FilePicker = null,
    IRuntimeCapabilities? Capabilities = null);

public sealed record ToolDescriptor(
    ToolId Id,
    string TitleResourceKey,
    ToolSizeConstraints Size,
    ToolRefreshPolicy RefreshPolicy,
    Func<ToolCreationContext, Control> CreateContent,
    bool RequiresCloseConfirmation = false,
    IReadOnlySet<string>? RequiredPorts = null);

public interface IToolCatalog
{
    IReadOnlyList<ToolDescriptor> Descriptors { get; }

    bool TryGet(ToolId id, out ToolDescriptor descriptor);
}

public sealed class ToolCatalog : IToolCatalog
{
    private readonly IReadOnlyDictionary<ToolId, ToolDescriptor> descriptors;

    public ToolCatalog(IEnumerable<ToolDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        var items = descriptors.ToArray();
        if (items.Any(static item => item is null))
        {
            throw new ArgumentException("Tool descriptors cannot be null.", nameof(descriptors));
        }

        if (items.Any(static item => string.IsNullOrWhiteSpace(item.Id.Value)))
        {
            throw new ArgumentException("Tool descriptors require a tool identifier.", nameof(descriptors));
        }

        if (items.Any(static item => string.IsNullOrWhiteSpace(item.TitleResourceKey)))
        {
            throw new ArgumentException("Tool descriptors require a title resource key.", nameof(descriptors));
        }

        if (items.Any(static item => item.CreateContent is null))
        {
            throw new ArgumentException("Tool descriptors require a content factory.", nameof(descriptors));
        }

        var duplicate = items
            .GroupBy(static item => item.Id)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate tool identifier: {duplicate.Key}.", nameof(descriptors));
        }

        this.descriptors = items.ToDictionary(static item => item.Id);
        Descriptors = items;
    }

    public IReadOnlyList<ToolDescriptor> Descriptors { get; }

    public bool TryGet(ToolId id, out ToolDescriptor descriptor) => descriptors.TryGetValue(id, out descriptor!);
}

public sealed record WorkspaceHostServices(
    IChapterLoadService LoadService,
    IChapterSaveService SaveService,
    IChapterEditingService EditingService,
    ChapterSegmentService SegmentService,
    IChapterTimeFormatter Formatter,
    IFrameRateService FrameRateService,
    IChapterExpressionEngine ExpressionEngine,
    ChapterExportService ExportService,
    IExpressionAuthoringService ExpressionAuthoringService);

public sealed record HostEffectServices(
    IApplicationLogService LogService,
    Microsoft.Extensions.Logging.ILogger<MainWindowViewModel> Logger,
    IShellService ShellService);

public sealed record SettingsAppearanceServices(
    ISettingsStore<ChapterToolSettings> SettingsStore,
    IThemeApplicationService ThemeApplicationService,
    IFontFamilyCatalog FontFamilyCatalog,
    IFontApplicationService FontApplicationService,
    IExternalToolLocator ExternalToolLocator,
    string SettingsDirectory);

public sealed record LocalizationServices(IAppLocalizer Localizer);

public sealed record RuntimeHostServices(IRuntimeCapabilities Capabilities);

public sealed record AuxiliaryToolHostServices(IAuxiliaryToolHost Host, IEmbeddedToolPresenter Presenter);

public sealed record AvaloniaHostDependencies(
    WorkspaceHostServices Workspace,
    HostEffectServices Effects,
    SettingsAppearanceServices Settings,
    LocalizationServices Localization,
    RuntimeHostServices Runtime,
    AuxiliaryToolHostServices AuxiliaryTools)
{
    public AvaloniaHostComposition Compose()
    {
        var composition = new AvaloniaHostComposition(
            Workspace,
            Effects,
            Settings,
            Localization,
            Runtime,
            AuxiliaryTools);
        composition.Validate();
        return composition;
    }
}

public sealed record AvaloniaHostComposition(
    WorkspaceHostServices Workspace,
    HostEffectServices Effects,
    SettingsAppearanceServices Settings,
    LocalizationServices Localization,
    RuntimeHostServices Runtime,
    AuxiliaryToolHostServices AuxiliaryTools)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Workspace);
        ArgumentNullException.ThrowIfNull(Effects);
        ArgumentNullException.ThrowIfNull(Settings);
        ArgumentNullException.ThrowIfNull(Localization);
        ArgumentNullException.ThrowIfNull(Runtime);
        ArgumentNullException.ThrowIfNull(AuxiliaryTools);
        ArgumentNullException.ThrowIfNull(AuxiliaryTools.Host);
        ArgumentNullException.ThrowIfNull(AuxiliaryTools.Presenter);

        ArgumentNullException.ThrowIfNull(Workspace.LoadService);
        ArgumentNullException.ThrowIfNull(Workspace.SaveService);
        ArgumentNullException.ThrowIfNull(Workspace.EditingService);
        ArgumentNullException.ThrowIfNull(Workspace.SegmentService);
        ArgumentNullException.ThrowIfNull(Workspace.Formatter);
        ArgumentNullException.ThrowIfNull(Workspace.FrameRateService);
        ArgumentNullException.ThrowIfNull(Workspace.ExpressionEngine);
        ArgumentNullException.ThrowIfNull(Workspace.ExportService);
        ArgumentNullException.ThrowIfNull(Workspace.ExpressionAuthoringService);
        ArgumentNullException.ThrowIfNull(Effects.LogService);
        ArgumentNullException.ThrowIfNull(Effects.Logger);
        ArgumentNullException.ThrowIfNull(Effects.ShellService);
        ArgumentNullException.ThrowIfNull(Settings.SettingsStore);
        ArgumentNullException.ThrowIfNull(Settings.ThemeApplicationService);
        ArgumentNullException.ThrowIfNull(Settings.FontFamilyCatalog);
        ArgumentNullException.ThrowIfNull(Settings.FontApplicationService);
        ArgumentNullException.ThrowIfNull(Settings.ExternalToolLocator);
        ArgumentNullException.ThrowIfNull(Localization.Localizer);
        ArgumentNullException.ThrowIfNull(Runtime.Capabilities);
    }
}
