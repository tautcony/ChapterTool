## Why

近期功能修改引入了主窗口 UX 回归。用户截图显示编辑历史常驻占用右侧空间、章节列表下方出现大块空白，以及表达式预览与底部选项重叠，影响章节编辑和操作确认。

## What Changes

- 将编辑历史收进历史按钮打开的模态弹框。保留撤销、重做、分支导航和当前会话提示。
- 让章节列表使用顶部工具区与底部选项之间的完整可用区域。让空状态与列表共用同一区域。
- 为表达式和其他内容操作预览提供独立布局区域。修复宽窄布局切换后的行定位、滚动和操作按钮布局。
- 用本地化的业务值展示变更。提供修改前、修改后和变化量，避免直接展示 `StartTicks`、`FramesInfo` 和枚举名称。
- 保留预览、应用、取消和一步撤销的事务语义。防止重排布局或打开历史创建内容变更。
- 增加实际渲染、键盘交互、控件边界和窗口尺寸切换的回归验收。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `avalonia-ui-shell`: 增加历史弹框行为、章节区域空间分配和主窗口内容预览的布局及可读性要求。补充响应式布局的行为验收。

## Impact

- 主要影响 `src/ChapterTool.Avalonia.UI/Views/MainView.axaml`、其代码后置和 `MainWindowViewModel` 的历史与内容预览展示。
- 历史弹框使用现有辅助工具协议、桌面工具目录和 `AvaloniaWindowService`。按需增加窄会话端口及工具视图。
- 预览展示复用已有 typed candidate 和表达式比较投影。Core 历史、表达式计算和导出规则保持既有契约。
- 相关验证位于 `tests/ChapterTool.Avalonia.Tests` 和 `tests/ChapterTool.Avalonia.Headless.Tests`。实现时更新相关 `docs/code-map/` 页面和本地化资源投影。
- 本 change 聚焦截图所示 Avalonia UX 回归。它不包含完整视觉重设计、Blazor 页面改版、CLI 修改或新编辑功能。
