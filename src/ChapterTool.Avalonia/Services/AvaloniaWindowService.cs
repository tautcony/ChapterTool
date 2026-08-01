using Avalonia.Controls;
using ChapterTool.Avalonia.UI.Localization;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.Configuration;
using ChapterTool.Contracts.PlatformPorts;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Services;

/// <summary>Hosts injected auxiliary-tool descriptors in Native Window surfaces.</summary>
public sealed class AvaloniaWindowService : IAuxiliaryToolHost
{
    private readonly ISettingsStore<ChapterToolSettings> settingsStore;
    private readonly IThemeApplicationService themeApplicationService;
    private readonly IFontFamilyCatalog fontFamilyCatalog;
    private readonly IFontApplicationService fontApplicationService;
    private readonly ISettingsCloseConfirmationService settingsCloseConfirmationService;
    private readonly Func<Window, ISettingsPickerService> settingsPickerFactory;
    private readonly IExternalToolLocator externalToolLocator;
    private readonly IShellService shellService;
    private readonly string settingsDirectory;
    private readonly IExpressionAuthoringService expressionAuthoringService;
    private readonly Func<Window, IClipboardService> clipboardServiceFactory;
    private readonly IToolCatalog toolCatalog;
    private readonly Dictionary<string, Window> windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAppLocalizer localizer;
    private readonly EventHandler cultureChangedHandler;
    private bool disposed;

    public AvaloniaWindowService(
        IAppLocalizer localizer,
        ISettingsStore<ChapterToolSettings> settingsStore,
        IThemeApplicationService themeApplicationService,
        Func<Window, ISettingsPickerService> settingsPickerFactory,
        IExternalToolLocator externalToolLocator,
        ISettingsCloseConfirmationService settingsCloseConfirmationService,
        IShellService shellService,
        IFontFamilyCatalog fontFamilyCatalog,
        IFontApplicationService fontApplicationService,
        string settingsDirectory,
        IExpressionAuthoringService expressionAuthoringService,
        Func<Window, IClipboardService> clipboardServiceFactory,
        IToolCatalog toolCatalog)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(themeApplicationService);
        ArgumentNullException.ThrowIfNull(settingsPickerFactory);
        ArgumentNullException.ThrowIfNull(externalToolLocator);
        ArgumentNullException.ThrowIfNull(settingsCloseConfirmationService);
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(fontFamilyCatalog);
        ArgumentNullException.ThrowIfNull(fontApplicationService);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        ArgumentNullException.ThrowIfNull(expressionAuthoringService);
        ArgumentNullException.ThrowIfNull(clipboardServiceFactory);
        ArgumentNullException.ThrowIfNull(toolCatalog);
        this.settingsStore = settingsStore;
        this.themeApplicationService = themeApplicationService;
        this.fontFamilyCatalog = fontFamilyCatalog;
        this.fontApplicationService = fontApplicationService;
        this.localizer = localizer;
        this.settingsCloseConfirmationService = settingsCloseConfirmationService;
        this.settingsPickerFactory = settingsPickerFactory;
        this.externalToolLocator = externalToolLocator;
        this.shellService = shellService;
        this.settingsDirectory = settingsDirectory;
        this.expressionAuthoringService = expressionAuthoringService;
        this.clipboardServiceFactory = clipboardServiceFactory;
        this.toolCatalog = toolCatalog;
        cultureChangedHandler = (_, _) =>
        {
            foreach (var (id, window) in windows)
            {
                window.Title = Title(id);
            }
        };
        this.localizer.CultureChanged += cultureChangedHandler;
    }

    public ValueTask<AuxiliaryToolResult> OpenAsync(
        ToolId toolId,
        AuxiliaryToolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!toolCatalog.TryGet(toolId, out var descriptor))
        {
            return ValueTask.FromResult(AuxiliaryToolResult.Unknown(toolId));
        }

        if (windows.TryGetValue(toolId.Value, out var existing))
        {
            if (descriptor.RefreshPolicy == ToolRefreshPolicy.RefreshRequest)
            {
                DisposeContentDataContext(existing);
                existing.Content = CreateTypedContent(existing, descriptor, request);
            }

            existing.Activate();
            return ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Activated, toolId));
        }

        var window = new Window
        {
            Title = localizer.GetString(descriptor.TitleResourceKey),
            Width = descriptor.Size.PreferredWidth,
            Height = descriptor.Size.PreferredHeight,
            MinWidth = descriptor.Size.MinWidth,
            MinHeight = descriptor.Size.MinHeight
        };
        window.Classes.Add("auxiliaryHost");
        window.Content = CreateTypedContent(window, descriptor, request);
        ConfigureCloseBehavior(window, toolId, descriptor);
        window.Closed += (_, _) =>
        {
            DisposeContentDataContext(window);
            window.Content = null;
            windows.Remove(toolId.Value);
        };
        windows[toolId.Value] = window;
        window.Show();
        return ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Opened, toolId));
    }

    public ValueTask<AuxiliaryToolResult> CloseAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!windows.TryGetValue(toolId.Value, out var window))
        {
            return ValueTask.FromResult(AuxiliaryToolResult.Unknown(toolId));
        }

        window.Close();
        return ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Closed, toolId));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        localizer.CultureChanged -= cultureChangedHandler;
        foreach (var window in windows.Values.ToArray())
        {
            DisposeContentDataContext(window);
            window.Content = null;
            window.Close();
        }

        windows.Clear();
    }

    private Control CreateTypedContent(Window window, ToolDescriptor descriptor, AuxiliaryToolRequest request)
    {
        var filePicker = request.FilePicker ?? new AvaloniaFilePickerService(window, localizer);
        var context = new ToolCreationContext(
            request.Session,
            request.Localizer,
            settingsStore,
            themeApplicationService,
            settingsPickerFactory(window),
            externalToolLocator,
            shellService,
            fontFamilyCatalog,
            fontApplicationService,
            settingsDirectory,
            expressionAuthoringService,
            request.Clipboard ?? clipboardServiceFactory(window),
            window,
            filePicker,
            request.Capabilities);
        return descriptor.CreateContent(context);
    }

    private void ConfigureCloseBehavior(Window window, ToolId toolId, ToolDescriptor descriptor)
    {
        if (!descriptor.RequiresCloseConfirmation)
        {
            return;
        }

        var closeAccepted = false;
        var closeConfirmationPort = new DesktopSettingsCloseConfirmationPort(
            settingsCloseConfirmationService,
            window);
        window.Closing += async (_, args) =>
        {
            if (closeAccepted || window.Content is not Control control)
            {
                return;
            }

            if (control.DataContext is not SettingsToolViewModel settings || !settings.HasUnsavedChanges)
            {
                return;
            }

            args.Cancel = true;
            var action = await closeConfirmationPort.ConfirmCloseAsync(toolId, CancellationToken.None);
            switch (action)
            {
                case SettingsCloseAction.Save:
                    await settings.SaveCommand.ExecuteAsync(cancellationToken: CancellationToken.None);
                    closeAccepted = true;
                    window.Close();
                    break;
                case SettingsCloseAction.Discard:
                    settings.DiscardUnsavedChanges();
                    closeAccepted = true;
                    window.Close();
                    break;
            }
        };
    }

    private string Title(string id) =>
        toolCatalog.TryGet(new ToolId(id), out var descriptor)
            ? localizer.GetString(descriptor.TitleResourceKey)
            : id;

    private static void DisposeContentDataContext(Window window)
    {
        if (window.Content is Control { DataContext: IDisposable disposable } control)
        {
            control.DataContext = null;
            disposable.Dispose();
        }
    }
}

internal sealed class DesktopSettingsCloseConfirmationPort(
    ISettingsCloseConfirmationService service,
    Window owner) : ISettingsCloseConfirmationPort
{
    public ValueTask<SettingsCloseAction> ConfirmCloseAsync(ToolId toolId, CancellationToken cancellationToken)
        => service.ConfirmCloseAsync(owner, cancellationToken);
}
