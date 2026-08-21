using Avalonia.Controls;
using ChapterTool.Avalonia.UI.ViewModels;

namespace ChapterTool.Avalonia.UI.PlatformPorts;

public sealed class EmbeddedToolPresenter : IEmbeddedToolPresenter
{
    public Control? Content { get; private set; }

    public ToolId? ToolId { get; private set; }

    public event EventHandler? ContentChanged;

    public void SetContent(ToolId? toolId, Control? content)
    {
        this.ToolId = toolId;
        this.Content = content;
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Hosts catalog content in a single embedded presenter.</summary>
public sealed class EmbeddedAuxiliaryToolHost : IAuxiliaryToolHost
{
    private readonly IToolCatalog catalog;
    private readonly EmbeddedToolPresenter presenter;
    private readonly Func<AuxiliaryToolRequest, ToolCreationContext> contextFactory;
    private readonly ISettingsCloseConfirmationPort closeConfirmation;
    private readonly Dictionary<ToolId, Control> contentById = [];
    private bool disposed;

    public EmbeddedAuxiliaryToolHost(
        IToolCatalog catalog,
        EmbeddedToolPresenter presenter,
        Func<AuxiliaryToolRequest, ToolCreationContext> contextFactory,
        ISettingsCloseConfirmationPort? closeConfirmation = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.closeConfirmation = closeConfirmation ?? new UnavailableSettingsCloseConfirmationPort();
    }

    public ValueTask<AuxiliaryToolResult> OpenAsync(
        ToolId toolId,
        AuxiliaryToolRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!catalog.TryGet(toolId, out var descriptor))
        {
            return ValueTask.FromResult(AuxiliaryToolResult.Unknown(toolId));
        }

        if (contentById.TryGetValue(toolId, out var existing))
        {
            if (descriptor.RefreshPolicy == ToolRefreshPolicy.RefreshRequest)
            {
                DisposeContent(existing);
                existing = descriptor.CreateContent(contextFactory(request));
                contentById[toolId] = existing;
            }

            presenter.SetContent(toolId, existing);
            return ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Activated, toolId));
        }

        var content = descriptor.CreateContent(contextFactory(request));
        contentById.Add(toolId, content);
        presenter.SetContent(toolId, content);
        return ValueTask.FromResult(new AuxiliaryToolResult(AuxiliaryToolResultKind.Opened, toolId));
    }

    public async ValueTask<AuxiliaryToolResult> CloseAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!contentById.TryGetValue(toolId, out var content))
        {
            return AuxiliaryToolResult.Unknown(toolId);
        }

        if (content.DataContext is SettingsToolViewModel { HasUnsavedChanges: true } settings)
        {
            var action = await closeConfirmation.ConfirmCloseAsync(toolId, cancellationToken);
            switch (action)
            {
                case SettingsCloseAction.Cancel:
                    return new AuxiliaryToolResult(AuxiliaryToolResultKind.Activated, toolId);
                case SettingsCloseAction.Save:
                    await settings.SaveCommand.ExecuteAsync(cancellationToken: cancellationToken);
                    break;
                case SettingsCloseAction.Discard:
                    settings.DiscardUnsavedChanges();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        contentById.Remove(toolId);
        if (presenter.ToolId == toolId)
        {
            presenter.SetContent(null, null);
        }

        DisposeContent(content);
        return new AuxiliaryToolResult(AuxiliaryToolResultKind.Closed, toolId);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var content in contentById.Values)
        {
            DisposeContent(content);
        }

        contentById.Clear();
        presenter.SetContent(null, null);
    }

    private static void DisposeContent(Control content)
    {
        if (content.DataContext is IDisposable disposable)
        {
            content.DataContext = null;
            disposable.Dispose();
        }
        else
        {
            content.DataContext = null;
        }
    }
}
