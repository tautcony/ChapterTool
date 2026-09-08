using ChapterTool.Contracts.Shortcuts;

namespace ChapterTool.Avalonia.Tests.ViewModels;

public sealed class ShortcutMappingTests
{
    [Fact]
    public void CatalogHasDeterministicPrimaryActionsAndClipGestures()
    {
        Assert.Equal(["load", "save", "reload", "previous-clip", "next-clip", "preview", "log", "insert", "delete"], ShortcutCatalog.All.Select(static action => action.Id));
    }

    [Theory]
    [InlineData("shift+ctrl+s", "Ctrl+Shift+S")]
    [InlineData(" f5 ", "F5")]
    [InlineData("Meta+S", "Meta+S")]
    [InlineData("", null)]
    public void GestureTextNormalizesAndRejectsReservedKeys(string input, string? expected)
    {
        Assert.Equal(expected, ShortcutGestureText.Normalize(input));
        Assert.Equal(expected is null ? ShortcutGestureStatus.Cleared : ShortcutGestureStatus.Valid, ShortcutGestureText.Validate(input));
    }

    [Fact]
    public void GestureDisplayUsesModifierSymbolsAfterParsing()
    {
        Assert.Equal("⌘+⇧+S", ShortcutGestureText.FormatForDisplay("Meta+Shift+S"));
        Assert.Equal("⌃+⌥+P", ShortcutGestureText.FormatForDisplay("Alt+Ctrl+P"));
        Assert.Equal("⌘+⇧+↵", ShortcutGestureText.FormatForDisplay("Shift+Meta+Return"));
        Assert.Equal("⌘+⇧+↵", ShortcutGestureText.FormatForDisplay("Shift+Cmd+Return"));
        Assert.Equal("⌃+↑", ShortcutGestureText.FormatForDisplay("Ctrl+Up"));
        Assert.Equal("⌥+⇞", ShortcutGestureText.FormatForDisplay("Alt+PageUp"));
        Assert.Equal("⌦", ShortcutGestureText.FormatForDisplay("Delete"));
        Assert.Equal("⎀", ShortcutGestureText.FormatForDisplay("Insert"));
        Assert.Equal("not-a-gesture", ShortcutGestureText.FormatForDisplay("not-a-gesture"));
    }

    [Fact]
    public void ConflictingRowsAreReported()
    {
        var rows = ShortcutConflictValidator.DefaultRowGestures().ToDictionary(static pair => pair.Key, static pair => pair.Value);
        rows[ShortcutCatalog.SaveId] = "Ctrl+R";

        var conflicts = ShortcutConflictValidator.FindConflicts(rows);

        Assert.Contains(ShortcutCatalog.SaveId, conflicts);
        Assert.Contains(ShortcutCatalog.ReloadId, conflicts);
    }

    [Fact]
    public void CustomMappingReplacesDefaultGesture()
    {
        var settings = new ShortcutSettings
        {
            [ShortcutCatalog.SaveId] = "Ctrl+Shift+S"
        };
        var mapping = ShortcutMapping.Resolve(settings);

        Assert.Equal(ShortcutCatalog.SaveId, mapping.ActionForGesture("Ctrl+Shift+S"));
        Assert.Null(mapping.ActionForGesture("Ctrl+S"));
    }

    [Fact]
    public void NormalizationDropsUnknownAndBackfillsMissingActions()
    {
        var normalized = ShortcutSettings.Normalize(new ShortcutSettings
        {
            ["unknown"] = "Ctrl+X",
            [ShortcutCatalog.SaveId] = "Ctrl+Shift+S"
        });

        Assert.DoesNotContain("unknown", normalized.Keys);
        Assert.Equal("Ctrl+Shift+S", normalized[ShortcutCatalog.SaveId]);
        Assert.Equal(ShortcutCatalog.Reload.DefaultGesture, normalized[ShortcutCatalog.ReloadId]);
    }
}
