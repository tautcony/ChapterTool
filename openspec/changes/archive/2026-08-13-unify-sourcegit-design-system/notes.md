# Change notes

## 1.1 Initial unconsumed-definition list

The audit script is `scripts/audit-ui-resources.ps1`.

The first run reported 0 unresolved references and 34 unconsumed definitions.

Unconsumed imported surface tokens:

- `Brush.Badge`
- `Brush.BadgeFG`
- `Brush.Border0`
- `Brush.Conflict`
- `Brush.Conflict.Foreground`
- `Brush.Conflict.MineBG`
- `Brush.Conflict.TheirsBG`
- `Brush.Diff.AddedBG`
- `Brush.Diff.AddedHighlight`
- `Brush.Diff.BlockBorderHighlight`
- `Brush.Diff.DeletedBG`
- `Brush.Diff.DeletedHighlight`
- `Brush.Diff.EmptyBG`
- `Brush.FlatButton.FloatingBorder`
- `Brush.HistoryBG`
- `Brush.InlineCode`
- `Brush.TitleBar`

Unconsumed ChapterTool alias brushes with no view consumer:

- `ChapterTool.AuxiliaryPopupBackgroundBrush`
- `ChapterTool.AuxiliaryTitleBackgroundBrush`

Style-file keys that no view references as `{DynamicResource}` / `{StaticResource}`:

- `ComboBoxDropDownBackground`
- `ComboBoxDropDownBorderBrush`
- `ComboBoxDropdownBorderPadding`
- `ComboBoxItemThemePadding`
- `SliderHorizontalThumbHeight`
- `SliderHorizontalThumbWidth`
- `SliderPostContentMargin`
- `SliderPreContentMargin`
- `SliderThumbCornerRadius`
- `SliderTopHeaderMargin`
- `SystemControlErrorTextForegroundBrush`
- `SystemErrorTextColor`
- `TabItemPipeThickness`

Theme-dictionary names, not consumable brushes:

- `Light`
- `Dark`

The `Color.*` sources for the unused `Brush.*` tokens stay defined. Task 3.4 deletes those unused imported tokens after the style prune.

## 3.5 Final audit

After the brush merge and style prune, the audit reported 0 unresolved references.

Unconsumed imported surface tokens were removed. Remaining `x:Key` values in `Styles.axaml` are either theme-dictionary names (`Light`, `Dark`) or keys inside `Style.Resources` that the control templates consume. The audit script treats those as consumed.
