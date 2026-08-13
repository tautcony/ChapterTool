using System.Runtime.Versioning;
using ChapterTool.Wasm.Services;

namespace ChapterTool.Wasm.Tests;

[SupportedOSPlatform("browser")]
public sealed class WasmBrowserShortcutGuardTests
{
    [Theory]
    [InlineData("F5", false, false)]
    [InlineData("F5", false, true)]
    [InlineData("r", true, false)]
    [InlineData("R", true, true)]
    public void ReloadKeysAlwaysPreventBrowserDefault(string key, bool ctrlOrMeta, bool isEditable)
    {
        Assert.True(WasmBrowserShortcutGuard.ShouldPreventBrowserDefault(key, ctrlOrMeta, isEditable));
    }

    [Theory]
    [InlineData("s", true)]
    [InlineData("o", true)]
    [InlineData("l", true)]
    [InlineData("F11", false)]
    [InlineData("F9", false)]
    public void AppShortcutsPreventBrowserDefaultOutsideInputs(string key, bool ctrlOrMeta)
    {
        Assert.True(WasmBrowserShortcutGuard.ShouldPreventBrowserDefault(key, ctrlOrMeta, isEditableTarget: false));
        Assert.False(WasmBrowserShortcutGuard.ShouldPreventBrowserDefault(key, ctrlOrMeta, isEditableTarget: true));
    }

    [Fact]
    public void UnrelatedKeysDoNotPreventBrowserDefault()
    {
        Assert.False(WasmBrowserShortcutGuard.ShouldPreventBrowserDefault("a", ctrlOrMeta: true, isEditableTarget: false));
        Assert.False(WasmBrowserShortcutGuard.ShouldPreventBrowserDefault("Enter", ctrlOrMeta: false, isEditableTarget: false));
    }
}
