using System.Reflection;
using System.Runtime.Versioning;
using ChapterTool.Wasm.Components;
using Microsoft.JSInterop;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmDialogTests
{
    [Fact]
    public async Task Disposal_after_runtime_disconnect_does_not_throw()
    {
        var runtime = new DisconnectedRuntime();
        var dialog = new WasmDialog();
        typeof(WasmDialog).GetProperty("Js", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dialog, runtime);

        await dialog.DisposeAsync();

        Assert.Equal("chapterToolWasm.disposeDialog", runtime.Identifier);
    }

    private sealed class DisconnectedRuntime : IJSRuntime
    {
        public string? Identifier { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Identifier = identifier;
            return ValueTask.FromException<TValue>(new JSDisconnectedException("The runtime is disconnected."));
        }
    }
}
