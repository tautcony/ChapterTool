# ChapterTool WebAssembly 功能完善计划

## 目标

以 `src/ChapterTool.Avalonia` 为行为参考，逐项完善
`src/ChapterTool.Wasm`，使浏览器端成为可实际使用的 ChapterTool
Core 工作台，而不是仅用于演示导入/导出的示例。

本计划只覆盖浏览器可实现的 Avalonia 功能。Lua 文件加载、Lua 专用编辑器、Lua
补全和脚本工作流明确不在范围内；表达式运行时本身保持现状，不扩展浏览器端的
Lua 编辑器能力。

## 已完成

- [x] 日志按钮与 Avalonia 状态栏风格统一，增加外发光效果。
- [x] 日志使用 Modal 对话框展示，支持清空和关闭。
- [x] 设置使用 Modal 对话框展示，按 Avalonia 结构划分页面。
- [x] 设置使用 `localStorage` 持久化。
- [x] 设置文档统一为 Avalonia 形态：`schemaVersion`、`application`、`theme`、`font`。
- [x] 设置 JSON 使用 camelCase，当前 schema 版本为 `1`。
- [x] 清理旧的 `settings.v1`、`settings.v2` 和不符合当前结构的旧存储值。
- [x] 保留保存格式选择，但移除浏览器端没有意义的 Save As 操作。
- [x] MPLS/IFO clip 选择框右键支持合并和拆分组合 clip。
- [x] FPS 选择框右键支持变更帧率。
- [x] 行右键支持选择、插入、复制、删除、复制名称和复制 `--zones`。
- [x] 以上编辑和转换复用 Core 的 `ChapterEditingService`、
      `ChapterSegmentService`、`ChapterFpsTransformService` 和导出服务。

## 实施顺序

### 1. 工作区会话与加载命令

- [x] 保存最近一次成功加载的文件名和字节内容，提供 Reload。
- [x] Load 按钮右键实现 Reload。
- [x] Load 按钮右键实现 Append MPLS 文件选择。
- [x] 使用 `ChapterSegmentService.Append` 将追加的 MPLS group 合并到当前会话。
- [x] 追加失败时保留当前会话，并将 Core diagnostics 写入日志和状态栏。
- [x] 成功加载新文件后清理旧会话状态、选择状态和诊断状态。

涉及位置：

- `src/ChapterTool.Wasm/Services/WasmWorkspace.cs`
- `src/ChapterTool.Wasm/Services/WasmChapterService.cs`
- `src/ChapterTool.Wasm/Pages/Home.razor`
- `src/ChapterTool.Wasm/wwwroot/js/download.js`

### 2. 章节命名模板

- [x] 将 Chapter name 选项补齐为 As is、Auto generate、Template。
- [x] 增加 `UseTemplateNames` 和 `ChapterNameTemplateText` 状态。
- [x] 增加浏览器文件选择读取模板文本。
- [x] 模板文件读取成功后显示文件名/状态，并自动切换到 Template。
- [x] 保存和预览统一通过 `ChapterExportOptions` 的模板字段投影名称。
- [x] 模板读取失败时不覆盖当前模板内容。

涉及位置：

- `src/ChapterTool.Wasm/Services/WasmModels.cs`
- `src/ChapterTool.Wasm/Services/WasmChapterService.cs`
- `src/ChapterTool.Wasm/Services/WasmWorkspace.cs`
- `src/ChapterTool.Wasm/Pages/Home.razor`

### 3. 多选与批量编辑

- [x] 章节表格改为多选模型，支持 Ctrl/Shift 选择。
- [x] 保留当前行选择并同步选中行集合。
- [x] 行右键菜单作用于选中行集合；未选中目标行时先选中目标行。
- [x] 批量删除选中行。
- [x] 批量生成 `--zones`。
- [x] 批量前移翻译。
- [x] 对没有合法选择的操作禁用菜单项或给出明确状态。

涉及位置：

- `src/ChapterTool.Wasm/Services/WasmModels.cs`
- `src/ChapterTool.Wasm/Services/WasmWorkspace.cs`
- `src/ChapterTool.Wasm/Pages/Home.razor`
- `src/ChapterTool.Wasm/wwwroot/css/app.css`

### 4. 主窗口快捷命令和刷新

- [x] 增加 Preview 按钮。
- [x] 增加 Refresh rows 按钮。
- [x] 增加 Load/Save/Reload/Preview/Refresh 的浏览器快捷键。
- [x] 快捷键在文本输入框和设置输入控件中不抢占用户输入。
- [x] 增加统一的 busy、禁用和状态日志反馈。

参考 Avalonia：

- `src/ChapterTool.Avalonia/Views/MainWindow.axaml`
- `src/ChapterTool.Avalonia/Views/MainWindow.axaml.cs`
- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.Expression.cs`

### 5. Preview、Zones 和 Forward Translation

- [x] Grid 右键增加 Preview。
- [x] Preview 使用文本 Modal，内容来自当前投影和
      `ChapterExportService`，与 Save 使用同一套格式、命名和表达式选项。
- [x] Preview 支持复制和下载当前预览文本。
- [x] Grid 右键增加 Zones，支持选中行集合并复制结果。
- [x] Grid 右键增加 Forward Translation，使用帧数输入 Modal。
- [x] 前移操作复用 `IChapterEditingService.ShiftFramesForward`。
- [x] 操作完成后刷新行、帧信息、诊断和日志。

参考 Avalonia：

- `src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/ChapterTool.Avalonia/ViewModels/Tools/ForwardShiftToolViewModel.cs`
- `src/ChapterTool.Avalonia/Views/Tools/ForwardShiftToolView.axaml`

### 6. Related Media

- [x] Grid 右键增加 Open Related Media。
- [x] 展示当前 clip 的 `ReferencedMediaFiles` 和相对路径。
- [x] 浏览器端使用下载/打开链接表达可用的媒体引用，不伪造桌面 shell 打开能力。
- [x] 找不到可用引用时写入状态和日志。
- [x] 记录浏览器平台限制：媒体引用可能是相对路径，浏览器无法访问本地目录。

涉及 Core 模型：

- `src/ChapterTool.Core/Models/ChapterImportEntry.cs`
- `src/ChapterTool.Core/Models/ReferencedMediaFile.cs`

### 7. 拖放加载

- [x] 实现页面拖放文件加载。
- [x] 拖放内容与 Load 文件选择共用同一个字节导入入口。
- [x] 拖放期间显示可投放状态，完成后恢复正常状态。
- [x] 处理浏览器禁止读取、超大文件和空文件错误。

### 8. 浏览器端本地化

- [x] 建立 WASM 本地化字典，覆盖 `zh-CN`、`en-US`、`ja-JP`。
- [x] 替换主窗口、右键菜单、Modal、状态和设置页中的硬编码用户可见文本。
- [x] UI language 改变后立即刷新页面文本，并继续写入 Avalonia 兼容设置格式。
- [x] Core diagnostic 原文保持可读，必要时增加稳定 code 显示。

### 9. 命名、代码地图和文档收尾

- [x] 将用户可见的 Demo 文案改为 Browser/WebAssembly 工作台文案。
- [x] 将浏览器工作台从 `samples` 迁移到 `src/ChapterTool.Wasm`，并统一正式程序集、命名空间和测试项目命名。
- [x] 根据最终模块职责更新 `docs/code-map/core.md` 和相关 code-map 索引。
- [x] 更新 `src/ChapterTool.Wasm/README.md`：功能矩阵、浏览器限制、
      操作方式、验证命令和源码入口。
- [x] 更新根 `README.md` 的 WASM 入口描述和徽章文案。
- [x] 更新 GitHub Pages 工作流注释和发布信息中的 Demo 表述。
- [x] 增加“已实现 / Lua 排除 / 浏览器限制”的功能差异说明，避免把桌面能力宣称为
      浏览器能力。

## 明确的平台限制

以下 Avalonia 能力不在浏览器端伪造：

- 选择本地保存目录；浏览器只能触发下载并使用浏览器下载目录。
- 配置和执行 `mkvtoolnix`、`eac3to`、`ffprobe`、`ffmpeg` 等本地可执行文件。
- 依赖外部工具的媒体容器和 BDMV 文件导入。
- 通过桌面 shell 直接打开本地 Related Media 文件。
- Lua 脚本文件加载、Lua 专用编辑器、补全和脚本工作流。

## 验证标准

- [x] `dotnet build src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-restore`
- [x] `git diff --check`
- [x] 对新增工作区行为增加可执行测试，优先覆盖 append、模板、多选、前移、zones、reload。
- [ ] 启动 WASM 后验证 Load、Reload、Append、编辑、多选右键、Preview、Save、设置持久化、
      拖放和语言切换。
- [ ] 在窄屏和宽屏下检查顶部操作区、章节表格、底部选项区、状态栏和各 Modal 不重叠。
- [ ] 发布目录验证 `index.html`、`404.html`、`_framework` 和 GitHub Pages base href。

## 变更记录

| 日期 | 内容 |
| --- | --- |
| 2026-07-19 | 建立 WASM 非 Lua 功能平等实施计划；记录已完成项、待办顺序和浏览器限制。 |
| 2026-07-19 | 完成浏览器工作台会话、模板、多选编辑、预览/快捷键、关联媒体、拖放和三语言本地化；新增工作区行为测试与发布说明。 |
