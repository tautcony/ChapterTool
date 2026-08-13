# ChapterTool `src` 代码库审查（高视角）

本审查是 `src-code-review-remediation` 的依据；实现计划、任务证据和归档入口见 [`docs/code-review-remediation-plan.md`](code-review-remediation-plan.md) 与 [`openspec/changes/src-code-review-remediation/`](../openspec/changes/src-code-review-remediation/)。

| 项 | 内容 |
|---|---|
| 审查日期 | 2026-07-12 |
| 审查范围 | `src/ChapterTool.Core`、`src/ChapterTool.Infrastructure`、`src/ChapterTool.Avalonia`（约 2 万行业务源码） |
| 审查标准 | 严格可维护性 / 结构简化（code judo），并覆盖安全、分层、边界与测试友好性 |
| 分支上下文 | `feat/avalonia-workflow-modernization` |
| 落地 change | `openspec/changes/src-code-review-remediation`（proposal / design / specs / tasks） |

## 总体判断

**不批准“架构已经够用、可以长期继续堆功能”的状态。**

行为层面成熟、有不少优秀设计；但壳层存在清晰的 God Object 与半成品抽象，继续加功能会放大维护成本。Core/Infrastructure 明显好于 Avalonia 壳层。

---

## 架构总览

```text
Avalonia (UI/CLI/VM/Session/Composition)
    ↓
Infrastructure (process/tools/settings/media adapters)
    ↓
Core (models/import/edit/transform/export)
```

| 层 | 角色 | 健康度 |
|---|---|---|
| **Core** | 章节领域、导入/编辑/变换/导出 | 较好：契约清晰、记录类型多、格式插件化 |
| **Infrastructure** | 外部工具、进程、设置、平台、重依赖导入 | 较好：ProcessRunner/SettingsStore 质量高 |
| **Avalonia** | 壳、CLI、VM、Session、组合根 | 中等偏弱：状态集中、半提取、双命令面 |

### 应保留的优点

- 分层方向正确：Core 不引用 Avalonia/Infrastructure。
- `ChapterWorkspace` + `ClipSession` 把异步 load/append 的 revision / session-token 反陈旧规则抽出来，比散落 flag 干净得多。
- `AppCompositionRoot` 提供 GUI/CLI 共享工厂（importer registry / export service）。
- `ProcessRunner`：`ArgumentList`、`UseShellExecute=false`、输出上限、超时杀进程树。
- `ChapterToolSettingsStore`：路径锁、原子写、损坏保护、版本升级、缓存。
- 导入 registry + fallback（ffprobe ↔ ATL、mkvextract ↔ media 等）边界清楚。
- 诊断模型结构化（`Source.Reason`），便于本地化与日志。

---

## 1. 结构性问题（最高优先级）

### 1.1 `MainWindowViewModel` 是事实上的 God Object

跨 partial 合计约 **2160 行**，主文件接近 1k：

| 文件 | 行数（约） |
|---|---:|
| `MainWindowViewModel.cs` | 970 |
| `MainWindowViewModel.Editing.cs` | 408 |
| `MainWindowViewModel.StatusLog.cs` | 317 |
| `MainWindowViewModel.ImportExport.cs` | 194 |
| `MainWindowViewModel.Expression.cs` | 189 |
| `MainWindowViewModel.Settings.cs` | 82 |

同时实现：

- `IExpressionSessionPort`
- `IPreferenceSink`
- `IExportPreferencePort`
- `INamingPreferencePort`
- `IChapterEditPort`

并叠加 load/save/clip/projection/localization/status/log/命令状态广播。

**问题不只是“文件长”，而是概念过多：**  
`Session` 已经把状态所有权往外移，但 **编排权仍全压在一个类型上**。Partials 只是把 God Object 分页，没有减少读者必须同时持有的概念数。

**Code judo 方向：** 把 partial 变成真正的协作对象，而不是文件切片。

```text
MainWindowViewModel (薄绑定壳 + INotifyPropertyChanged)
  ├─ ChapterWorkspace          (已有：状态所有权)
  ├─ LoadSaveWorkflow          (load/append/save/progress)
  ├─ ClipEditingCoordinator    (select/combine/edit cells)
  ├─ ProjectionFacade          (expression/naming/order → rows)
  └─ StatusDiagnosticsPresenter(status/log/localized diagnostics)
```

目标：VM 只做绑定与命令转发；工具口（ports）指向小接口实现，而不是一个万能类。

**可执行门槛：** 若下一轮功能再让 `MainWindowViewModel.cs` 越过 1000 行，应先拆协调器，再加功能。

### 1.2 Settings 的“模块”是空壳，没有删掉复杂度

`SettingsToolViewModel` 仍约 **878 行**。  
下列类型在整个仓库 **无任何引用**，只是 POCO 注释式“所有权模块”：

- `ViewModels/Settings/SettingsExternalToolsModule.cs`
- `ViewModels/Settings/SettingsOutputDefaultsModule.cs`
- `ViewModels/Settings/SettingsAboutModule.cs`

状态与逻辑仍在主 VM。这是典型 **移动复杂度但未删除** 的半成品重构：

- 读者会以为设置已模块化；
- 实际仍是单文件 monologue；
- 多了一层假抽象。

**Code judo：** 二选一，不要停在中间态：

1. **真拆**：模块持有状态 + 命令 + dirty/snapshot/save 片段，VM 只做聚合；或
2. **删掉未接线的 module 类型**，避免“看起来像设计、实际是死代码”。

当前状态 **不应算完成的 Settings 现代化**。

### 1.3 双命令面：`MainWindow` 与 `MainWindowViewModel` 平行 `UiCommand`

`MainWindow.axaml.cs` 再包一层 `BrowseAndLoadCommand`、`SaveCommand`、`DeleteSelectedCommand`…，多数只是：

- 调文件选择器 / 读选中行；
- 再 `viewModel.XxxCommand.ExecuteAsync`。

结果：

- CanExecute 同步要双向 `RaiseCanExecuteChanged`；
- 行为分叉（有的逻辑在 View，有的在 VM）；
- 测试与 Headless 路径更绕。

**更直接的结构：**

- View 只保留 **需要 UI 能力的适配器**（picker、DataGrid 选中索引）；
- 其余命令单一权威在 VM；
- 或引入很薄的 `MainWindowInteractionService`，但不要两套命令表。

### 1.4 `ExpressionEditor` 过重且绕开组合根

约 **728 行** code-behind，内部：

```csharp
private readonly IExpressionAuthoringService authoringService = new ExpressionAuthoringService();
```

绕开 DI/Composition：

- 再造默认 `LuaExpressionScriptService`（与应用注入引擎不是同一实例）；
- 控件同时管编辑器、补全、诊断、主题、多行展开、定时器；
- 与 `Views/Controls/Expression/*` 的拆分仍不够——主类仍是上帝控件。

**Code judo：** 编辑器只做展示；authoring 分析进 VM 或注入服务；诊断/补全用已有 presentation 类型驱动。

---

## 2. 边界与抽象问题

### 2.1 可选依赖掩盖不变量

多处 `IShellService?`、`ISettingsStore<>?`、`ISettingsPickerService?`：

- 运行时用 `if (x is null) { SetStatus(...); return; }` 分叉；
- 测试与生产构造函数形状不一致；
- 契约读起来像“可能没有设置/shell”，但产品几乎总需要。

**更清晰：** 组合根保证非空；测试注入 fake；不要用可空表示“可选产品能力”。

### 2.2 组合根是手写工厂，不是生命周期清晰的 DI

`AppCompositionRoot` 可理解且适合桌面体量，但有明显重复与生命周期模糊：

- `CreateSharedImporterRegistry` 与 `CreateExternalToolLocator` 各自 `new ExternalToolLocator(...)`；
- 多处 `new ChapterTimeFormatter()`；
- `CreateChapterSaveService()` / `CreateChapterExportService()` 可产生多实例；
- 设置加载 fire-and-forget（`_ = ApplyAppearanceSettingsAsync()`），主题/字体可能在窗口已显示后才到位（有 macOS 注释，可接受，但状态机应显式）。

若继续长，建议：

- 明确 **单例 vs 每次创建** 策略；
- 或引入轻量 `ServiceProvider`（不必上完整 ASP.NET DI 全家桶）。

### 2.3 Core 作为“纯领域”不纯，但作为库合理

Core 内大量 `File.OpenRead` / `ReadAllTextAsync`（导入器）。对 NuGet 库这是务实选择；语义上则是：

- **领域 + I/O 适配** 同包；
- 难做“纯内存管道”测试时只能靠 `ChapterImportRequest.Content` 旁路。

若未来要更干净：importer 接口保留 stream/content 优先，路径打开收到 Infrastructure/Avalonia 边界。不必立刻大挪，但应避免新逻辑继续加深“Core 直接摸盘”。

### 2.4 导出路径上的临时对象

`ChapterExportService.Export` 每次 `new ChapterOutputProjectionService(expressionEngine)`；  
`ExpressionAuthoringService` 在默认参数路径上可构造多个 Lua engine。小开销，但暴露 **缺少单一引擎实例策略**，与 ExpressionEditor 自建实例是同一类问题。

---

## 3. 安全与隐私

威胁模型：本地桌面工具，用户导入不可信媒体/XML/MPLS、可配置外部可执行文件、可选遥测。

### 3.1 XML 导入 XXE 风险 — **应修**

`XmlChapterImporter` 使用 `XmlDocument.Load` / `LoadXml`，未见：

- `XmlResolver = null`
- `DtdProcessing = Prohibit`
- 安全 `XmlReaderSettings`

恶意 Matroska XML 可触发外部实体/资源解析（平台与 .NET 版本依赖，但默认 `XmlDocument` 不是安全基线）。

**修复方向：** 统一用带 secure settings 的 `XmlReader` 加载，或 `XmlDocument` 显式禁用 DTD/resolver。`XplChapterImporter` 的 `XDocument.LoadAsync` 也建议统一审查。

### 3.2 二进制解析可能分配失控 — **应加固**

`BinaryReadExtensions.ReadExactBytes(int length)` 对 length **无上限**：

```csharp
var bytes = new byte[length];
```

`MplsPlaylistFile` 等从文件读长度再 `ReadExactBytes`。恶意/损坏 MPLS 可导致大分配 / OOM。

**修复：** 对 playlist mark / extension data 等字段加合理上限，超限 `InvalidDataException`。

### 3.3 Lua 表达式沙箱 — **可接受，但别自欺**

优点：

- 只 `OpenMathLibrary` / `OpenStringLibrary` / `OpenTableLibrary`（无 io/os/package）；
- 默认 **500ms** 超时 + CTS；
- 返回值校验有限数值。

残留风险：

- 仍是 **进程内** 执行，非 OS 级隔离；
- `string`/`table` 仍可能被用来做重计算/内存压力；
- `DoStringAsync(...).GetAwaiter().GetResult()` 在调用线程同步阻塞。

对“用户主动写表达式”的桌面场景通常可接受；不要宣传为“不可逃逸沙箱”。若未来支持远程/分享表达式，应再收紧。

### 3.4 外部工具路径 = 任意进程执行 — **产品预期，需明示**

`ExternalToolLocator` + `ProcessRunner` 会执行设置里的 `mkvextract` / `eac3to` / `ffprobe` 路径。  
用户（或被篡改的 `settings.json`）可指向任意可执行文件。

对这类工具链应用是常态，但应：

- 设置页明确“将执行该路径”；
- 可选：保存前校验文件名/扩展名/可执行位（已有 Unix execute bit 检查，Windows 较松）；
- 损坏/可写 settings 目录的威胁纳入隐私/安全说明。

`ProcessRunner` 本身实现质量好，问题在 **信任边界是用户设置文件**。

### 3.5 Shell `OpenAsync` + `UseShellExecute = true`

用于打开相关媒体、设置目录、GitHub URL。桌面合理。  
`RevealInFolderAsync` 用 `ArgumentList` 是好的；`OpenAsync` 直接把 target 当 `FileName` 依赖 Shell 关联。

相关媒体解析：`Path.GetFullPath(Path.Combine(base, RelativePath))` — 若 playlist 引用含 `..`，可能解析到盘上其他文件再被打开。威胁低（本地文件），但属于“导入数据驱动的本地打开”。

### 3.6 Sentry 默认行为 — **隐私/策略问题**

`SentryStartupConfiguration`：

- 内置 **DefaultDsn**；
- 默认在有 DSN 时启用；
- **`SendDefaultPii` 默认 true**。

桌面应用处理用户路径与文件名时，默认 PII + 硬编码 DSN 意味着：

- 发行构建可能无显式同意就上报；
- 路径/异常可能含用户目录信息。

**审查建议：** 默认 opt-in（或仅非 DEBUG 且显式 env 启用）；`SendDefaultPii` 默认 false；DSN 不硬编码进开源仓库（或至少文档写清关闭方式）。这是产品/合规问题，不是“小 nit”。

**产品决定（2026-07-12）：** 保持 Sentry 默认启用。本项建议已明确排除在 `src-code-review-remediation` 之外；后续不得将其作为该变更的未完成任务重新引入。若产品策略变更，应创建独立 OpenSpec 变更。

### 3.7 保存路径竞态

`AllocateUniqueFilePath` 是 check-then-write，非原子；并发两个 save 可能撞车。单用户 UI 风险低；CLI 并行调用时可能覆盖失败后重试，应知悉。

---

## 4. 设计与领域模型（中高优先级）

### 4.1 Session 提取是正确方向 — 还没走完

`ChapterWorkspace` / `ClipSession` / `ProjectionState` / `ExportPreferences` 是好的 code judo 半成品：

- 状态所有权在 workspace；
- 提交规则有测试钩子。

缺口：VM 仍是唯一编排者，port 直接钉在 God VM 上。下一步应是 **工作流对象消费 workspace**，而不是再给 VM 加 partial。

### 4.2 导入体系整体健康

| 区域 | 评价 |
|---|---|
| Text/CUE/VTT/XML 导入器 | 职责单一，失败变 diagnostics |
| Disc MPLS/IFO/XPL | 逻辑集中；`MplsPlaylistFile` 909 行可按 record 拆文件，但不必强行“业务拆” |
| Media + ffprobe/ATL | Infrastructure 边界正确 |
| BDMV + eac3to | 编排长（413 行），可拆 “list playlists / export chapters / parse OGM” 流水线函数 |

`RuntimeChapterImporterRegistry` 的扩展名 switch 清晰；新增格式时这里是正确扩展点。

### 4.3 编辑 / 导出

- `ChapterEditingService`：不可变 `record` + 新列表，风格一致。
- `ChapterExportService`：format switch 直接可读；与 projection 组合清楚。
- 时间/帧边界有 clamp 与 diagnostic，领域味对。

无明显“错误层放错”的业务逻辑；主要债务在壳层编排。

### 4.4 CLI 与 GUI 共享有进步，范围故意收窄

CLI 故意无 expression engine — 产品范围清晰。  
`ChapterToolCliApplication`（~523 行）仍可按 inspect/convert/formats 拆，但优先级低于 VM God Object。

---

## 5. 并发与状态一致性

**亮点：** workspace revision + append session id 防陈旧提交，设计认真。

**风险点：**

- UI 属性与 workspace 双向 facade：漏 `OnPropertyChanged` 会导致绑定漂移（已有大量手动通知，脆弱）。
- `NotifyCommandStates` 广播式刷新全部命令 — 正确但粗；拆协调器后可局部化。
- `UiCommand.Execute` 吞异常只留 `ExecutionError` — 若 View 不观察，失败会静默。需保证绑定/状态条消费 `ExecutionError`。

---

## 6. 可测试性与可观测性

**好的：**

- Headless 与 unit 进程隔离（与 AGENTS 一致）；
- Fake shell / scripted dialog / recording window；
- Session 纯转换可单测。

**拖累：**

- God VM 导致测试巨型 setup（测试里已有大段 fake 构造）；
- ExpressionEditor 自建服务，难测 UI 与引擎一致性；
- 死 Settings modules 不增加测试面，只增加噪音。

---

## 7. 文件体量与分解清单

| 单元 | 约行数 | 判定 |
|---|---:|---|
| `MainWindowViewModel*` | 2160 | **必须继续拆（编排层）** |
| Settings 栈 | 1242 | **模块要么接线要么删除** |
| Expression 控件栈 | 1029 | **减重 + 注入 authoring** |
| `MplsPlaylistFile.cs` | 909 | 可文件级拆分，非架构危机 |
| `SettingsToolViewModel.cs` | 878 | 与死 module 矛盾 |
| `ExpressionEditor.axaml.cs` | 728 | 控件过重 |
| `MainWindow.axaml.cs` | 606 | 双命令面问题 |
| CLI | 745 | 次优先 |

### Core 按目录体量（约）

| 目录 | 约行数 |
|---|---:|
| Importing | 3383 |
| Transform | 1569 |
| Exporting | 1187 |
| Editing | 470 |
| Diagnostics | 321 |
| Models | 240 |

---

## 8. 建议路线图（按杠杆排序）

### P0 — 安全 / 隐私（小改大收益）

1. 加固 XML 加载（禁 DTD / null resolver / 安全 XmlReader）。
2. 二进制 `ReadExactBytes` 长度上限。
3. Sentry：默认 opt-in、`SendDefaultPii=false`、评估硬编码 DSN。（当前产品决定不采纳；不属于 `src-code-review-remediation`。）

### P1 — 结构（真正的 code judo）

1. **完成** Session 方向：从 VM 抽出 `LoadSaveWorkflow` / `ProjectionFacade` 等，而不是再加 partial。
2. Settings：**删除未使用 modules** 或 **真正迁入状态与 save/dirty**。
3. 收敛 MainWindow 双命令面。
4. ExpressionEditor 注入 `IExpressionAuthoringService`，与组合根共用引擎策略。

### P2 — 边界整洁

1. 去掉生产路径上不必要的 `?` 依赖。
2. 明确 composition 生命周期（单例 formatter/locator/export）。
3. 新 Core API 优先 stream/content，少直接路径 I/O。

### P3 — 局部整洁

1. `MplsPlaylistFile` 按类型拆文件。
2. BDMV importer 流水线函数拆分。
3. CLI 按子命令拆类型。

---

## 9. 审批栏（按严格可维护性标准）

| 标准 | 结果 |
|---|---|
| 无清晰结构回归 / God Object | **未通过** — 壳层仍以巨型 VM 为中心 |
| 无明显可删除复杂度的错过机会 | **未通过** — 死 Settings modules、双命令面、partial 假拆分 |
| 无不当文件膨胀 | **警戒** — 主 VM 已贴 1k，Settings/Expression 紧随 |
| 无特殊分支意大利面扩散 | **部分通过** — Core 较好；壳层 flag/可空依赖偏多 |
| 无错误层逻辑 | **基本通过** |
| 安全基线 | **未通过** — XML/二进制/Sentry 需处理 |

## 10. 结论

作为章节工具，**领域与基础设施质量高于平均桌面 .NET 项目**；`ProcessRunner`、settings 原子性、clip session 反陈旧、导入 fallback 是应保留的骨架。

但 **Avalonia 壳层尚未完成从“巨型 ViewModel 应用”到“workspace + 工作流对象”的跃迁**。继续在 partial / 空 module 上堆功能，会把已经付过的结构化成本浪费掉。

### 最高杠杆的下一步

1. **结构：** 别再扩大 `MainWindowViewModel`；把下一个功能做成独立 workflow/coordinator，消费 `ChapterWorkspace`。
2. **安全：** 修 XML 加载安全配置 + 二进制长度上限。

---

## 相关入口

- 代码导航：`docs/code-map/`
- 组合根：`src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`
- 会话状态：`src/ChapterTool.Avalonia/Session/`
- 主壳 VM：`src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel*.cs`
- 设置：`src/ChapterTool.Avalonia/ViewModels/SettingsToolViewModel.cs`
- 进程执行：`src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs`
- XML 导入：`src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`
- Lua 引擎：`src/ChapterTool.Core/Transform/Expressions/Lua/LuaExpressionScriptService.cs`
- Sentry：`src/ChapterTool.Avalonia/Diagnostics/SentryStartupConfiguration.cs`
