## Why

当前 Avalonia 快捷键分散在视图代码和 `ShortcutRouter` 中，存在硬编码、重复映射和无法自定义的问题。用户需要在不修改配置文件或重新编译应用的情况下调整常用操作，并且设置变更必须可持久化、可验证且不破坏现有工作流。

## What Changes

- 抽取统一的快捷键动作目录、默认手势和运行时路由，消除视图中的散落硬编码。
- 在设置工具中新增“快捷键”Tab，按动作显示当前手势，支持录入、重置和保存。
- 对非法手势、重复手势和平台不可用手势提供即时校验，并阻止保存冲突配置。
- 将快捷键映射加入版本化 `settings.json`，旧配置自动使用默认映射，保留未知字段兼容性。
- 让主窗口菜单、提示文本和 Headless 测试使用同一份运行时映射。
- 增加 ViewModel、设置持久化和 Avalonia Headless 工作流测试，覆盖自定义、冲突、重置、保存及重新加载。

## Capabilities

### New Capabilities

- `keyboard-shortcut-customization`: 定义动作目录、手势校验、用户自定义生命周期及运行时路由契约。

### Modified Capabilities

- `avalonia-ui-shell`: 主窗口快捷键和菜单手势改为读取可配置映射，同时保留默认行为。
- `versioned-settings-document`: 版本化设置文档新增快捷键 section，并定义缺省与升级行为。

## Impact

影响 `ShortcutRouter`、主窗口菜单/提示绑定、`SettingsToolViewModel` 与 `SettingsToolView`，以及 `ChapterToolSettings` 的序列化和规范化逻辑。需要新增快捷键领域模型与本地化资源，并扩展 `ChapterTool.Avalonia.Tests`、`ChapterTool.Avalonia.Headless.Tests`；不引入外部依赖。
