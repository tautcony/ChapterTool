# WinForms -> Avalonia interaction map

| WinForms | Avalonia | Notes |
| --- | --- | --- |
| Form | Window | lifetime |
| UserControl | UserControl | |
| Button | Button | Command binding |
| TextBox | TextBox | two-way |
| CheckBox | CheckBox | IsChecked |
| ComboBox | ComboBox | items + selected item |
| NumericUpDown | NumericUpDown | width for spinners |
| DataGridView | DataGrid | row VMs |
| StatusStrip | status area layout | |
| Menu / ToolStrip | Menu / toolbar | |
| ContextMenuStrip | ContextMenu / MenuFlyout | |
| File/Folder dialogs | storage provider / picker service | async |
| MessageBox | dialog service | |
| ProgressBar | ProgressBar | bind Value |

## Events → commands

| WinForms | Avalonia approach |
| --- | --- |
| Click | Command |
| CheckedChanged | bind IsChecked |
| SelectedIndexChanged | bind selection |
| CellEndEdit | VM edit method |
| DragDrop | code-behind gesture adapter -> application command |
| KeyDown shortcuts | KeyBinding |
| FormClosing | Closing policy bound to lifecycle contract |

## Layout

Use responsive panels, stable minimum sizes, accessible names, explicit focus order, and
scrolling for narrow states. Do not encode normal workflow layout with absolute coordinates.
