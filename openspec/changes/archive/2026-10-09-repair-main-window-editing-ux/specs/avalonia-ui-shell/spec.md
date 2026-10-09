## MODIFIED Requirements

### Requirement: Global styles preserve workflow usability
The ported style layer SHALL keep ChapterTool workflows usable at supported window sizes. The main window SHALL preserve separate layout bounds for its toolbar, chapter workspace, bottom options, content preview, and status strip. Layout changes SHALL preserve drafts, candidate identity, chapter selection, and the document history cursor.

#### Scenario: Main window uses the global styles
- **WHEN** the main window opens
- **THEN** the load and save area, chapter grid, options area, and status strip SHALL remain available
- **AND** controls SHALL not overlap

#### Scenario: Tool window uses a narrow supported size
- **WHEN** a tool window is resized to its minimum width
- **THEN** primary actions SHALL remain visible
- **AND** text SHALL remain inside its control bounds

#### Scenario: Main window crosses the options breakpoint repeatedly
- **WHEN** a loaded main window with a pending preview changes between widths 860 and 861 and then returns to its original width
- **THEN** the preview SHALL remain in a separate allocated region below the options form
- **AND** the form, preview, and status strip SHALL not overlap
- **AND** layout changes SHALL not evaluate, apply, or cancel the candidate
- **AND** the draft, candidate identity, chapter selection, and history cursor SHALL remain unchanged

#### Scenario: Minimum main window shows a long preview
- **WHEN** the main window renders at 760×520 with long localized labels and a long pending preview
- **THEN** the toolbar, usable chapter viewport, options inputs, preview entry point, Apply, Cancel, and status strip SHALL remain reachable
- **AND** long results SHALL scroll within their allocated region or remain reachable through the complete-difference surface
- **AND** result text SHALL not cover an input or action

## ADDED Requirements

### Requirement: History opens in a button-invoked dialog
Avalonia 编辑历史 MUST 由历史按钮打开独立模态弹框。主窗口 MUST NOT 常驻显示历史列表或为历史预留侧栏空间。历史弹框 MUST 使用当前文档会话的历史和命令。打开、关闭或调整弹框尺寸 MUST NOT 创建历史事务。

#### Scenario: Load without opening history
- **WHEN** 用户加载章节文档但没有点击历史按钮
- **THEN** 主窗口不显示历史列表或历史侧栏占位
- **AND** 章节工作区使用完整中央宽度
- **AND** 历史按钮具有本地化的可访问名称

#### Scenario: Open and dismiss history
- **WHEN** 用户点击历史按钮
- **THEN** 一个属于主窗口的模态历史弹框打开
- **AND** 弹框显示当前节点、撤销、重做、分支导航和当前会话生命周期提示
- **AND** 主窗口后台点击、快捷键和拖放不能通过弹框修改文档
- **WHEN** 用户通过关闭按钮、窗口关闭或 Escape 退出弹框
- **THEN** 弹框关闭并归还焦点到历史按钮
- **AND** 仅关闭弹框不会回滚弹框内已执行的历史导航
- **AND** 主窗口章节区域不因开关弹框而改变分配尺寸

#### Scenario: Navigate history in the dialog
- **WHEN** 用户在弹框中执行撤销、重做或选择保留的另一分支
- **THEN** 操作调用当前会话的既有历史命令
- **AND** 弹框更新当前节点、按钮可用性和操作描述
- **AND** 主窗口章节数据刷新为对应历史状态
- **AND** 关闭并重新打开后显示相同的当前历史状态

#### Scenario: Reopen history and release its resources
- **WHEN** 用户反复打开和关闭历史弹框
- **THEN** 同一时刻最多存在一个该会话的历史弹框
- **AND** 已关闭弹框释放内容树及会话订阅
- **AND** 无可用文档会话时历史按钮禁用

#### Scenario: Inspect a long branching history
- **WHEN** 历史包含大量条目、多个分支和长操作描述
- **THEN** 用户能通过虚拟化列表滚动及键盘导航访问保留节点
- **AND** 当前节点和分支关系不只依赖颜色或空格识别
- **AND** 撤销、重做和关闭操作保持可达

### Requirement: Chapter grid fills its allocated workspace
章节表格 MUST 填充顶部工具区与底部选项之间的中央分配区。该区域 MUST NOT 为已隐藏的空状态或历史保留独立空白行。空状态 MUST 在相同工作区居中显示。列标题、值和编辑控件 MUST 保持可读。

#### Scenario: Loaded table uses the full central area
- **WHEN** 用户在 760×600、1280×800 或 760×520 的窗口加载足够多的章节
- **THEN** 表格边界覆盖中央分配区的完整可用宽度和高度
- **AND** 表格下方没有额外的等高空白区域
- **AND** 用户能滚动到最后一个章节并提交单元格编辑
- **AND** 表格虚拟化和扩展选择继续工作

#### Scenario: Empty and loaded states share the same area
- **WHEN** 窗口尚未加载章节
- **THEN** 空状态图像和提示在章节工作区居中
- **AND** 空状态不会阻止加载或拖放操作
- **WHEN** 章节加载成功
- **THEN** 空状态隐藏且表格使用同一完整工作区

#### Scenario: Resize with long chapter values
- **WHEN** 用户调整窗口尺寸并编辑长章节名称、时间或帧值
- **THEN** 列保留合理最小宽度，标题和内容不相互覆盖
- **AND** 必要的滚动保持可用
- **AND** 编辑按稳定列身份提交，选择和滚动位置在身份仍有效时保持

### Requirement: Main-window content previews have a bounded review region
表达式及主窗口其他待确认内容操作 MUST 显示在独立结果区域。该区域 MUST 显示操作名称、作用范围、完整变更计数和可检查的结果。状态条 MUST 只显示简短状态，不能作为唯一的差异展示。Apply 和 Cancel MUST 位于结果滚动区之外。没有预览时，结果区域 MUST 折叠。

#### Scenario: Preview an expression without overlapping options
- **WHEN** 用户输入有效表达式 `t + 1` 并得到候选
- **THEN** 预览出现在独立结果区域
- **AND** 保存格式、XML 语言、命名、编号偏移和表达式输入不被结果文字覆盖
- **AND** 用户能检查预览并点击 Apply 或 Cancel

#### Scenario: Review another content operation
- **WHEN** 用户准备命名、编号偏移或帧率变换候选
- **THEN** 同一结果区域显示该操作的名称、范围和修改前后值
- **AND** 用户不需要从状态条的技术摘要判断将要提交的内容
- **AND** Apply 绑定该区域所展示的候选

#### Scenario: Inspect all differences from a compact result
- **WHEN** 候选包含 1,000 个章节的变化，紧凑区域只能显示部分条目
- **THEN** 摘要计数覆盖完整候选
- **AND** 界面明确表示当前只显示部分条目
- **AND** 所有差异都能通过滚动或完整差异入口访问
- **AND** 完整差异入口展示同一个候选且不重新计算表达式

#### Scenario: Clear a preview
- **WHEN** 用户取消候选、应用成功或文档会话结束
- **THEN** 对应预览结果和确认操作清除
- **AND** 结果区域不保留空白占位或过期值

### Requirement: Main-window preview values use localized business formats
主窗口预览 MUST 从 typed snapshot 展示本地化业务值。它 MUST 按章节组织修改前、修改后和变化量。时间变化、帧信息更新和属性变化 MUST 明确区分。普通结果 MUST NOT 直接显示 ticks、内部字段标识、原始枚举或对象转储。

#### Scenario: Review a one-second change
- **WHEN** 候选将章节时间从 00:00:00 改为 00:00:01
- **THEN** 对比显示章节身份、可读的原时间、目标时间和正一秒变化量
- **AND** 同一章节的帧信息变化在该章节的明细中显示
- **AND** 原始 `StartTicks` 数字不是默认时间表示

#### Scenario: Distinguish frame-only and property-only changes
- **WHEN** 候选只更新帧信息或持久化属性而没有时间变化
- **THEN** 摘要明确说明没有章节时间变化
- **AND** 相关更新以本地化字段和原值、目标值显示
- **AND** 同一章节的多种变化只计为一个受影响章节

#### Scenario: Preserve precise and missing values
- **WHEN** 时间差小于一毫秒，或原帧信息缺失而目标为零帧
- **THEN** 时间精度能区分实际变化且变化量不显示为零
- **AND** 缺失帧值与零帧使用不同的业务表示
- **AND** 帧率依据不同的两侧各自显示依据且不生成误导的帧差

#### Scenario: Localize normal review content
- **WHEN** 用户使用中文或英文检查预览
- **THEN** 字段标签、单位、空值、状态和错误摘要使用当前语言
- **AND** accuracy 等枚举含义以业务说明展示
- **AND** 长文本可换行或展开且含义不只依赖颜色

### Requirement: Preview UX preserves candidate transactions
预览展示和布局调整 MUST 保留现有候选事务语义。准备 MUST 保持已提交章节和历史不变。Apply MUST 只提交展示的有效且当前的候选。输入、目标或历史变化 MUST 使旧候选不可应用。取消、失败和过期状态 MUST 不提交部分结果。

#### Scenario: Apply and undo the reviewed result
- **WHEN** 用户确认一个有效且当前的候选
- **THEN** 提交值与展示值完全相同且只创建一个事务
- **AND** 应用路径不重新计算表达式
- **AND** 一次撤销恢复该事务的全部变化

#### Scenario: Cancel or reject an invalid result
- **WHEN** 用户取消预览，或候选无效、无变化、等待计算或正在计算
- **THEN** 不创建内容事务
- **AND** 不符合提交条件时 Apply 禁用
- **AND** 错误和等待状态位于结果区域且不覆盖输入

#### Scenario: Invalidate a ready result
- **WHEN** 用户修改输入、切换目标或通过历史弹框改变基础内容
- **THEN** 旧候选不能提交
- **AND** 界面显示等待新预览或过期状态
- **AND** 拒绝过期提交时保留草稿并提供更新预览入口

### Requirement: UX regressions require rendered workflow evidence
实现 MUST 通过真实渲染和交互验证以上要求。测试 MUST 使用实际生产资源和确定性会话数据。截图 MUST 补充行为断言，不能替代断言。验收 MUST 包含中文、英文、默认、宽和最小窗口，以及布局阈值两侧。

#### Scenario: Verify layout and interactions
- **WHEN** 对本 change 执行验收
- **THEN** Headless 覆盖 760×600、1280×800、760×520、860 和 861 宽度及宽→窄→宽切换
- **AND** 断言章节表格覆盖中央分配区，选项和结果边界不相交
- **AND** 验证历史开关及分支、末行编辑、完整差异访问、应用、取消和一步撤销
- **AND** 代表性状态在放大字体和浅色、深色主题下保持可用
- **AND** 默认、宽、窄截图保存于 `artifacts/main-window-editing-ux/` 并复核
