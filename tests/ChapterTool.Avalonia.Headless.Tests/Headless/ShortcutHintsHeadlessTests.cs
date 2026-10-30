using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ShortcutHintsHeadlessTests
{
    [AvaloniaFact]
    public async Task Tooltips_follow_remaps_clears_fixed_aliases_and_language_changes()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LayoutAsync();

        var notifications = new List<string?>();
        host.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        host.ViewModel.SetShortcutSettings(new ShortcutSettings
        {
            [ShortcutCatalog.LoadId] = "Ctrl+Alt+L",
            [ShortcutCatalog.SaveId] = string.Empty,
            [ShortcutCatalog.ReloadId] = string.Empty,
            [ShortcutCatalog.LogId] = "Ctrl+Alt+G",
            [ShortcutCatalog.PreviewId] = string.Empty
        });
        await host.LayoutAsync();

        var loadButton = host.RequiredControl<SplitButton>("LoadButton");
        var saveButton = host.RequiredControl<Button>("SaveButton");
        var previewButton = host.RequiredControl<Button>("PreviewButton");
        var logButton = host.RequiredControl<Button>("LogButton");

        foreach (var (culture, shortcutPrefix) in new[]
        {
            ("en-US", "Shortcut: "),
            ("zh-CN", "快捷键："),
            ("ja-JP", "ショートカット: ")
        })
        {
            host.Localizer.SetCulture(culture);
            await host.LayoutAsync();

            var loadTooltip = ToolTip.GetTip(loadButton)?.ToString() ?? string.Empty;
            var saveTooltip = ToolTip.GetTip(saveButton)?.ToString() ?? string.Empty;
            var previewTooltip = ToolTip.GetTip(previewButton)?.ToString() ?? string.Empty;
            var logTooltip = ToolTip.GetTip(logButton)?.ToString() ?? string.Empty;

            Assert.Equal(host.ViewModel.LoadButtonTooltip, loadTooltip);
            Assert.Equal(host.ViewModel.SaveButtonTooltip, saveTooltip);
            Assert.Equal(host.ViewModel.PreviewButtonTooltip, previewTooltip);
            Assert.Equal(host.ViewModel.LogButtonTooltip, logTooltip);
            Assert.Contains($"{shortcutPrefix}⌃+⌥+L, ⌘+O", loadTooltip, StringComparison.Ordinal);
            Assert.Contains($"{shortcutPrefix}⌘+S", saveTooltip, StringComparison.Ordinal);
            Assert.DoesNotContain("F11", previewTooltip, StringComparison.Ordinal);
            Assert.Contains($"{shortcutPrefix}⌃+⌥+G", logTooltip, StringComparison.Ordinal);
        }

        Assert.Contains("⌃+⌥+L", host.ViewModel.LoadButtonTooltip, StringComparison.Ordinal);
        Assert.Contains("⌘+O", host.ViewModel.LoadButtonTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+O", host.ViewModel.LoadButtonTooltip, StringComparison.Ordinal);
        Assert.Contains("⌘+S", host.ViewModel.SaveButtonTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+S", host.ViewModel.SaveButtonTooltip, StringComparison.Ordinal);
        Assert.Contains("F5", host.ViewModel.ReloadButtonTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+R", host.ViewModel.ReloadButtonTooltip, StringComparison.Ordinal);
        Assert.Contains(nameof(MainWindowViewModel.LoadButtonTooltip), notifications);
        Assert.Contains(nameof(MainWindowViewModel.SaveButtonTooltip), notifications);
        Assert.Contains(nameof(MainWindowViewModel.ReloadButtonTooltip), notifications);

        notifications.Clear();
        host.Localizer.SetCulture("zh-CN");

        Assert.Contains("快捷键：", host.ViewModel.LoadButtonTooltip, StringComparison.Ordinal);
        Assert.Contains("⌃+⌥+L", host.ViewModel.LoadButtonTooltip, StringComparison.Ordinal);
        Assert.Contains(nameof(MainWindowViewModel.LoadButtonTooltip), notifications);
        Assert.Contains(nameof(MainWindowViewModel.SaveButtonTooltip), notifications);
        Assert.Contains(nameof(MainWindowViewModel.ReloadButtonTooltip), notifications);
    }
}
