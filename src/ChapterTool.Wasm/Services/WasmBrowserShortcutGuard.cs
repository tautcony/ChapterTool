namespace ChapterTool.Wasm.Services;

/// <summary>
/// Decides which browser default shortcuts the Wasm shell must block.
/// The same rules are implemented in wwwroot/js/download.js.
/// </summary>
public static class WasmBrowserShortcutGuard
{
    public static bool ShouldPreventBrowserDefault(string key, bool ctrlOrMeta, bool isEditableTarget)
    {
        if (IsReloadKey(key, ctrlOrMeta))
        {
            return true;
        }

        return !isEditableTarget && IsAppShortcut(key, ctrlOrMeta);
    }

    public static bool IsReloadKey(string key, bool ctrlOrMeta) =>
        string.Equals(key, "F5", StringComparison.OrdinalIgnoreCase)
        || (ctrlOrMeta && string.Equals(key, "r", StringComparison.OrdinalIgnoreCase));

    public static bool IsAppShortcut(string key, bool ctrlOrMeta) =>
        (ctrlOrMeta && key.ToLowerInvariant() is "s" or "o" or "l")
        || string.Equals(key, "F11", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "F9", StringComparison.OrdinalIgnoreCase);
}
