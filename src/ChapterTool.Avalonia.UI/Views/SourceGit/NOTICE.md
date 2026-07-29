# SourceGit user interface resources

This directory contains adapted user interface resources from SourceGit 2026.15.

- Upstream repository: `https://github.com/sourcegit-scm/sourcegit`
- Imported source directory: `sourcegit-master/src/Resources`
- Imported files: `Icons.axaml`, `Themes.axaml`, `Styles.axaml`, and the JetBrains Mono NL font files
- Upstream license: MIT. See `LICENSE` in this directory.

## Adaptations

- The port targets Avalonia 12.1.0 instead of Avalonia 11.3.18.
- The port removes the obsolete `Window.SystemDecorations` style setters. ChapterTool uses native window frames.
- The port keeps the Avalonia 12 window template. The Avalonia 11 SourceGit template does not contain the Avalonia 12 overlay layer.
- The port keeps the Avalonia 12 ComboBox popup placement. The Avalonia 11 placement override prevents Headless overlay hosting.
- The port replaces `TextBox.Watermark` with `TextBox.PlaceholderText`.
- The port replaces `Preferences.Instance` bindings with `SourceGit.FontSize.*` resources.
- The port lets `ContentPresenter` inherit its parent font. This preserves ChapterTool monospace editor controls.
- The port does not apply the SourceGit application zoom transform.
- The port keeps the opt-in `ScrollViewer.static_scrollbar` style without the SourceGit preference binding.
- The port replaces `StringConverters.FromKeyGesture` with the Avalonia template value.
- The port uses the ChapterTool assembly path for JetBrains Mono NL.
- The port uses the platform sans-serif family instead of the SourceGit Inter package.

## Excluded Git-domain templates

The `MenuItem` template does not include these SourceGit data templates:

- `SourceGit.Views.NameHighlightedTextBlock`
- `SourceGit.ViewModels.FilterModeInGraph` with `SourceGit.Views.FilterModeInGraph`
- `SourceGit.ViewModels.CustomActionContextMenuLabel`

These types require SourceGit Git-domain state. The reusable `MenuItem` control template remains in the port.
