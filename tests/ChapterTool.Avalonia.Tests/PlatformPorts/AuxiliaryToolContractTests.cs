using Avalonia.Controls;
using ChapterTool.Avalonia.UI.PlatformPorts;

namespace ChapterTool.Avalonia.Tests.PlatformPorts;

public sealed class AuxiliaryToolContractTests
{
    [Fact]
    public void Tool_ids_use_ordinal_case_insensitive_equality()
    {
        var lower = new ToolId("custom-tool");
        var upper = new ToolId("CUSTOM-TOOL");

        Assert.Equal(lower, upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
    }

    [Fact]
    public void Catalog_rejects_duplicate_identifiers()
    {
        var descriptor = Descriptor(new ToolId("duplicate"));

        Assert.Throws<ArgumentException>(() => new ToolCatalog([descriptor, descriptor]));
    }

    [Fact]
    public async Task Embedded_host_uses_custom_descriptor_and_reuses_content()
    {
        var presenter = new EmbeddedToolPresenter();
        var state = new DisposableState();
        var descriptor = new ToolDescriptor(
            new ToolId("custom-tool"),
            "Tool.Custom.Title",
            new ToolSizeConstraints(),
            ToolRefreshPolicy.Reuse,
            _ => new Border { DataContext = state });
        var host = new EmbeddedAuxiliaryToolHost(
            new ToolCatalog([descriptor]),
            presenter,
            _ => new ToolCreationContext(
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                string.Empty,
                null!,
                null!));

        var request = new AuxiliaryToolRequest(null!, null!, new RuntimeCapabilities(
            RuntimeSourceMode.LocalPath,
            RuntimeOutputMode.Directory,
            RuntimeSecondarySurfaceMode.InView,
            CanReadClipboard: false,
            CanWriteClipboard: false,
            CanConfigureExternalTools: false,
            CanRunExternalProcesses: false,
            CanOpenLocalPaths: false));
        var firstResult = await host.OpenAsync(new ToolId("CUSTOM-TOOL"), request, CancellationToken.None);
        var firstContent = presenter.Content;
        var secondResult = await host.OpenAsync(new ToolId("custom-tool"), request, CancellationToken.None);

        Assert.Equal(AuxiliaryToolResultKind.Opened, firstResult.Kind);
        Assert.Equal(AuxiliaryToolResultKind.Activated, secondResult.Kind);
        Assert.Same(firstContent, presenter.Content);

        await host.CloseAsync(new ToolId("custom-tool"), CancellationToken.None);

        Assert.Null(presenter.Content);
        Assert.True(state.IsDisposed);
    }

    [Fact]
    public async Task Unknown_embedded_tool_returns_safe_result()
    {
        var presenter = new EmbeddedToolPresenter();
        var host = new EmbeddedAuxiliaryToolHost(
            new ToolCatalog([]),
            presenter,
            _ => null!);

        var result = await host.OpenAsync(
            "missing-tool",
            new AuxiliaryToolRequest(null!, null!, new RuntimeCapabilities(
                RuntimeSourceMode.LocalPath,
                RuntimeOutputMode.Directory,
                RuntimeSecondarySurfaceMode.InView,
                CanReadClipboard: false,
                CanWriteClipboard: false,
                CanConfigureExternalTools: false,
                CanRunExternalProcesses: false,
                CanOpenLocalPaths: false)),
            CancellationToken.None);

        Assert.Equal(AuxiliaryToolResultKind.Unknown, result.Kind);
        Assert.Null(presenter.Content);
    }

    [Fact]
    public async Task Unavailable_capability_adapters_are_safe_no_ops()
    {
        var clipboard = new UnavailableClipboardService();
        var picker = new UnavailableSettingsPickerService();
        var filePicker = new UnavailableFilePickerService();
        var shell = new UnavailableShellService();
        var locator = new UnavailableExternalToolLocator();

        Assert.Null(await clipboard.GetTextAsync(CancellationToken.None));
        await clipboard.SetTextAsync("ignored", CancellationToken.None);
        Assert.Null(await picker.PickDirectoryAsync("ignored", CancellationToken.None));
        Assert.Null(await picker.PickExecutableAsync("ignored", CancellationToken.None));
        Assert.Null(await filePicker.PickSourceAsync(CancellationToken.None));
        await shell.OpenAsync("ignored", CancellationToken.None);
        await shell.RevealInFolderAsync("ignored", CancellationToken.None);
        await shell.OpenTerminalAsync("ignored", CancellationToken.None);
        Assert.False((await locator.LocateAsync("ignored", CancellationToken.None)).Found);
    }

    private static ToolDescriptor Descriptor(ToolId id) =>
        new(id, "Tool.Test.Title", new ToolSizeConstraints(), ToolRefreshPolicy.Reuse, _ => new Border());

    private sealed class DisposableState : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
