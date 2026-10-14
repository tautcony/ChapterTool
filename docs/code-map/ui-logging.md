# UI 日志审查（Logging Review）

本文件记录共享 Avalonia UI（`src/ChapterTool.Avalonia.UI`）中所有日志记录场景、触发条件与记录内容。用于排查问题和评估日志覆盖面。

## 日志基础设施

所有 UI 日志统一经由以下路径写入：

- 调用入口：`MainWindowViewModel.Log(...)`（`ViewModels/MainWindowViewModel.StatusLog.cs`，薄封装转发）与 `StatusDiagnosticsPresenter.Log / LogImportSummary / LogDiagnostics`
- 实现：`StatusDiagnosticsPresenter.Log(...)`（`Workflows/StatusDiagnosticsPresenter.cs`）
- 输出：结构化 `ILogger`（`Microsoft.Extensions.Logging`），每个条目为一个键值对集合（命名参数）

日志存储位置（`LoggingModule.cs`）：

| 目标 | 说明 |
|---|---|
| Serilog 文件 | `<设置目录>/logs/chaptertool-YYYYMMDD.log`，按天滚动，保留 14 个文件，单个 10 MB，最低级别 Debug |
| 内存日志面板 | `ApplicationLogPanelProvider`，容量 500 条，最低级别 Information，驱动应用内"日志"工具（`LogToolViewModel`） |
| 日志导出 | 日志工具可将条目导出为 JSON / CSV 到 `<设置目录>/logs/chaptertool-export-*.{json,csv}`（`ApplicationLogFileExporter`） |

每个条目统一携带的字段：

- `Message`：**英文原生消息**（调用点直接插值拼装，如 `Loading source: path='...'`），formatter 原样返回，文件日志与面板共用同一文本 —— 文件日志行可直接读懂，不再依赖本地化资源键
- `Operation`：显式操作分组标签（Load / Save / Edit / Append / Zones / Open / Template / Settings / Lua expression script 等），用于日志面板分组
- `TechnicalDetail`（可选）：技术细节，如异常消息、诊断明细；只进面板检查器，不混入参数列表
- `ExceptionText`：意外异常的完整 `exception.ToString()`（同时作为 `exception` 传给 `logger.Log`，Serilog 文件 sink 会将其渲染在消息下方）
- 其余命名参数（`(string Name, object? Value)` 元组数组）：场景相关数据，供面板检查器与 JSON/CSV 导出（见下表）

级别约定：默认 `Information`；可预期失败用 `Warning`；意外异常与导入整体失败用 `Error`。诊断（`ChapterDiagnostic`）按严重度映射：Error → `Error`，Warning → `Warning`，其余 → `Information`。

## 日志场景一览

| # | 场景 | 触发条件 | 级别 | 消息格式（英文原生示例） | 结构化参数 |
|---|---|---|---|---|---|
| 1 | 加载源文件 | 开始加载（拖拽、打开文件、重新加载） | Info | `Loading source: path='/disc/BDMV/INDEX.BDMV'` | `path`（本地源为完整路径 `NormalizedPath`，其余为 `DisplayName`） |
| 2 | 加载结果汇总 | 加载流程结束（成功 / 失败 / 部分成功） | Info 或 Error | `Load completed: groups=2, entries=3, chapters=10, diagnostics=1`（部分成功 → `completed with partial results`，失败 → `failed`；追加时 operation 为 `Append load`） | `operation`、`result`、`success`、`partial`、`groups`、`entries`、`chapters`、`diagnostics` 数量、`details`（逐组逐条目的结构化明细：源路径、媒体轨、时长、FPS 等）、`importOverview`（文本概览） |
| 3 | 加载诊断 | 加载产生诊断（仅 Warning 及以上） | Warn / Error | `Load diagnostic: severity=Warning, code=Import.Partial, message='部分章节缺失'`（有 location 时追加 `, location='...'`） | `severity`、`code`、`location`、`message`、`details`、诊断附加参数；`TechnicalDetail`=诊断 details |
| 4 | 无可选源 | 源显示名为空，或加载状态为 EmptyPath | Info | `Load source skipped: no source selected` | — |
| 5 | 追加 MPLS | 开始追加 | Info | `Appending MPLS: path='/disc/BDMV/PLAYLIST/00042.mpls', currentEntries=3, currentChapters=10` | `path`（完整路径）、`displayName`、`currentEntries`、`currentChapters` |
| 6 | 追加结果汇总 | 追加流程结束 | Info 或 Error | `Append load completed: groups=1, entries=1, chapters=5, diagnostics=0` | 同 #2，`operation`=Append load |
| 7 | 无当前 MPLS 组 | 追加时无活动会话 | Info | `Append MPLS skipped: no active MPLS group` | — |
| 8 | 追加过渡编辑失败 | 追加后合并编辑失败 | Warn / Error | `Append edit diagnostic: severity=Error, code=..., message='...'` | 同 #3，`operation`=Append edit |
| 9 | 保存章节 | 开始保存 | Info | `Saving chapters: format=OGM, directory='...', source='...', chapters=10, applyExpression=False, expression='', xmlLanguage='und', encoding=UTF8, bom=True` | `format`、`directory`、`source`、`chapters`、`applyExpression`、`expression`、`xmlLanguage`、`encoding`、`bom` |
| 10 | 输出投影诊断 | 保存前投影计算产生诊断 | Info / Warn / Error | `Output projection diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Output projection |
| 11 | 保存诊断 | 保存结束后 | Info / Warn / Error | `Save diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Save（含 `Saved` 结果诊断） |
| 12 | 切换源（clip） | 用户选择另一个源选项 | Info | `Selected source option: index=1, label='00015.mpls', source='00007', sourceType=Blu-ray MPLS, chapters=10, fps=23.976` | `index`、`label`、`source`、`sourceType`、`chapters`、`fps` |
| 13 | 单元格编辑 | 编辑时间 / 名称 / 帧单元格 | Info | `Edit time: row=3, value='00:00:05.000', previous='00:00:04.500'` | `action`（含 kind、row、新值、旧值）、`before`、`after`（章节数） |
| 14 | 组合 / 拆分片段 | 组合或还原组合 | Info | `Combine segments: entries=2, sourceType=Blu-ray MPLS: chapters 10 -> 8`（拆分 → `Split combined segments`） | `action`、`before`、`after` |
| 15 | 其他章节编辑 | 删除行、插入行、帧平移等（经 `ApplyEdit` / 端口适配器） | Info | `Delete rows: indexes=1,3: chapters 10 -> 8` / `Insert row: index=2: chapters 10 -> 11` / `Edit chapters: chapters 10 -> 10`（默认） | `action`、`before`、`after` |
| 16 | 编辑诊断 | 编辑产生诊断 | Info / Warn / Error | `Edit diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Edit |
| 17 | 帧信息更新 | 用户触发帧率选项变化后 | Info | `Frame info updated: option=23.976, fps=23.976, round=False, chapters=10`（自动检测时追加 `, autoDetected=true, confidence=...`） | `option`、`fps`、`round`、`chapters`、`autoDetected`、`confidence` |
| 18 | FPS 转换成功 | 转换当前 FPS 成功 | Info | `Convert to current FPS: option='24/1.001 (23.976)', source=23.976, target=23.976, chapters 10 -> 10` | `option`、`sourceFps`、`targetFps`、`before`、`after` |
| 19 | FPS 转换失败 | 转换失败 | Warn / Error | `Change FPS diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Change FPS |
| 20 | 创建 zones | 生成章节区域 | Info | `Create zones: selectedRows=10, chapters=10, zones=9` | `selectedRows`、`chapters`、`zones` |
| 21 | 创建 zones 诊断 | 生成产生诊断 | Info / Warn / Error | `Create zones diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Create zones |
| 22 | 打开关联媒体成功 | 系统成功打开媒体路径 | Info | `Opened related media: path='/disc/BDMV/STREAM/00007.m2ts'` | `status`、`path` |
| 23 | 关联媒体不存在 | 引用路径缺失或解析为空 | Warn | `Related media not found: reference='00007.m2ts', resolved='/disc/BDMV/STREAM/00007.m2ts'` | `status`、`reference`、`resolved` |
| 24 | shell 不可用 | 无法打开本地路径 | Warn | `Open related media skipped: shell unavailable` | `reference` |
| 25 | 加载章节名模板成功 | 模板文件读取成功且非空 | Info | `Loaded chapter name template 'template.txt' from '/path/template.txt'` | `path`、`name` |
| 26 | 加载章节名模板失败 | 文件为空，或 IO / 权限 / 参数异常 | Warn | `Failed to load chapter name template from '/path/template.txt': file is empty`（异常时后缀为异常消息） | `path`、`reason`=empty；异常消息记入 `TechnicalDetail` |
| 27 | 启动加载设置 | 应用启动时加载用户设置 | Info | `Settings loaded: savingPath='...', language='en-US', defaultSaveFormat=OGM, frameDisplay=Milliseconds, frameAccuracy=0.15, xmlLanguage='und'` | `savingPath`、`language`、`defaultSaveFormat`、`frameDisplay`、`frameAccuracy`、`xmlLanguage` |
| 28 | 切换 UI 语言 | 用户更改界面语言 | Info | `Language set to zh-CN` | `language` |
| 29 | Lua 表达式诊断 | 表达式应用 / 校验产生诊断（签名变化时才记录，避免重复刷屏） | Info / Warn / Error | `Lua expression script diagnostic: severity=..., code=..., message='...'` | 同 #3，`operation`=Lua expression script |
| 30 | Lua 脚本加载成功 | 从文件加载表达式脚本成功 | Info | `Lua expression script loaded: path='custom.lua'` | `path` |
| 31 | 意外 UI 异常 | 未处理的 UI 操作异常 | Error | `Unexpected UI operation failure: ...` | `exception`（完整 `exception.ToString()`，面板检查器可展开；`TechnicalDetail` 同文本） |

## 消息完整性审查（2026-09-01）与修复结果

审查时实际检查 `<设置目录>/logs/chaptertool-*.log` 文件，确认当时文件日志每行只有裸 `MessageKey`，不含任何参数：

```
2026-08-31 18:21:02.607 +08:00 [INF] Log.LoadingSource
2026-08-31 18:21:02.627 +08:00 [INF] Log.ImportSummary
2026-08-31 22:18:40.741 +08:00 [ERR] Log.Diagnostic
```

### 已修复：文件日志丢失全部参数（高优先级）

根因：`StatusDiagnosticsPresenter.Log` 传给 `logger.Log` 的 formatter 只返回 `MessageKey`，Serilog 文件 sink 的默认输出模板 `{Message:lj}` 不渲染结构化属性，因此路径、格式、数量、异常全部丢失。最严重的是意外异常：堆栈放在 `TechnicalDetail`、formatter 不输出、`exception` 参数又为 `null`，崩溃时文件日志只有一行裸键。

修复：**彻底移除 MessageKey 与日志本地化**（en-US / zh-CN / ja-JP 三个资源文件中 29 个 `Log.*`、`EditKind.*`、`Action.*` 键全部删除）。调用点直接写英文插值消息，formatter 原样返回该文本，文件日志与内存面板共用同一完整消息；`exception` 参数如实传入，Serilog 在消息下方渲染完整堆栈。

### 已完善：个别场景参数不足（中优先级）

| 场景 | 审查时的缺失 | 修复结果 |
|---|---|---|
| 加载源 / 追加 MPLS | `path` 仅为 `DisplayName`（`LocalPathChapterSource` 默认 `Path.GetFileName`），同名文件无法区分 | 改为完整路径（`NormalizedPath` / `GetFullPath`），加载与追加消息均携带 |
| 单元格编辑 | 只记新值，before/after 是章节数，值编辑前后不变 | 新增 `OldCellValue` 助手，记录编辑前旧值（时间经 `IChapterTimeFormatter` 格式化），`previous='...'` 直接出现在消息与参数中 |
| 快照类状态日志（无可选源、无会话、shell 不可用、Lua 脚本加载成功） | 仅状态栏本地化文本，无触发上下文 | 改为显式场景消息：`Load source skipped: no source selected`、`Append MPLS skipped: no active MPLS group`、`Open related media skipped: shell unavailable`、`Lua expression script loaded: path='...'` |
| 保存章节 | 缺导出选项 | 补 `xmlLanguage`、`encoding`、`bom`（实际写入文件的编码与 BOM 开关） |
| 启动加载设置 | 仅 savingPath、language | 补 `defaultSaveFormat`、`frameDisplay`、`frameAccuracy`、`xmlLanguage` |
| 创建 zones | 缺生成结果 | 补 `zones`（实际生成的 zone 数） |
| FPS 转换 | 缺所选选项 | 补 `option`（选项显示名） |
| 帧信息更新（自动检测） | 只记 confidence | `autoDetected=true` 时在消息与参数中显式标出检测来源与置信度 |

### 备注

- 程序化（非用户）的源切换会抑制源选项日志（`SelectClip(logSelection: false)`），避免会话恢复时刷屏。
- 加载 / 追加的逐条诊断只在 Warning 及以上级别才写入日志（`LogDiagnostics` 过滤）。
- 日志面板条目可筛选严重度、搜索（含嵌套参数与异常文本）、导出 JSON / CSV（`LogToolViewModel`）。
- 文件日志与面板消息同源：面板检查器展示的消息即文件日志行的消息文本，排障时无需在两边对照。
