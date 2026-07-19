# Avalonia / Infrastructure / WASM 功能映射

本文是 Avalonia 桌面端、Infrastructure 基础设施层和 Blazor WebAssembly 浏览器端之间的功能对照表。它用于功能修改前定位所有受影响入口，并在修改后检查两端是否仍然保持预期一致。

最后核对日期：2026-07-19

## 1. 先看结论

当前的依赖关系如下：

```text
ChapterTool.Avalonia
  ├─ ChapterTool.Core       共享章节模型、解析、编辑、变换、投影、导出
  └─ ChapterTool.Infrastructure
       ├─ 外部工具发现与进程执行
       ├─ 本地文件/平台服务
       ├─ 设置文件持久化
       └─ 媒体、Matroska、BDMV 导入适配器

ChapterTool.Wasm
  └─ ChapterTool.Core       通过浏览器字节流导入，通过 JS 下载导出
```

关键判断：

- Core 是两端应优先共享的行为源。时间解析、章节模型、编辑、片段合并、帧率变换、投影和导出应尽量修改 Core，而不是在两个宿主中各自修复。
- Avalonia 使用 `AppCompositionRoot` 组装完整桌面能力；WASM 使用 `WasmChapterService` 和 `WasmWorkspace` 重新组装浏览器安全范围内的能力。
- WASM 当前不会引用 Infrastructure，因此不能直接获得本地路径、外部进程、桌面设置文件、系统字体枚举或 Shell 打开等能力。
- 两端目前存在独立的状态编排和界面选项实现。即使底层 Core 逻辑相同，新增功能也可能只在一端出现，必须同时检查宿主入口。

## 2. 状态标记

| 标记 | 含义 |
| --- | --- |
| `[一致]` | 两端都能完成该功能，核心行为由同一 Core 类型提供 |
| `[部分一致]` | 主要行为一致，但入口、选项、宿主服务或交互有差异 |
| `[桌面独有]` | Avalonia + Infrastructure 提供，浏览器端受平台限制未提供 |
| `[WASM独有]` | 浏览器端提供，桌面端没有对应同一入口 |
| `[缺口]` | Core 或某一宿主已有基础能力，但当前目标端没有接入 |

标记描述的是当前代码状态，不代表产品需求上的优先级。

## 3. 运行时映射

### 3.1 主工作区

| 责任 | Avalonia 桌面端 | WASM 浏览器端 | 维护要点 |
| --- | --- | --- | --- |
| UI 壳 | `src/ChapterTool.Avalonia/Views/MainWindow.axaml(.cs)` | `src/ChapterTool.Wasm/Pages/Home.razor` | 两端都保留“顶部操作、中央章节表格、底部输出选项、状态条”四个工作区，但不是同一 UI 代码 |
| 会话状态 | `src/ChapterTool.Avalonia/Session/ChapterWorkspace.cs`、`ClipSession.cs`、`ProjectionState.cs`、`ExportPreferences.cs` | `src/ChapterTool.Wasm/Services/WasmWorkspace.cs` | 两边都维护源、片段、行、投影和导出状态；修改状态字段时要分别检查绑定和刷新逻辑 |
| 主工作流 | `src/ChapterTool.Avalonia/Workflows/LoadSaveWorkflow.cs`、`ClipEditingCoordinator.cs`、`ProjectionFacade.cs` | `WasmWorkspace` 内的 `LoadAsync`、`AppendMplsAsync`、`RefreshDisplay`、`Preview`、`Save` | Avalonia 偏组合式协作者，WASM 仍集中在一个工作区服务中 |
| 本地化 | `src/ChapterTool.Avalonia/Localization/AppLocalizationManager.cs` + `.resx` | `src/ChapterTool.Wasm/Services/WasmLocalizer.cs` 内的字典 | 语言代码都包含 `en-US`、`zh-CN`、`ja-JP`，但资源不是共享文件；新增用户可见文本需同步两处 |
| 测试入口 | Avalonia unit/headless 两个测试项目 | `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs` | Headless 只验证桌面渲染/交互；WASM 目前以工作区单元测试为主 |

### 3.2 Infrastructure 的位置

Infrastructure 只在桌面组合根接入：

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs` 创建 `ChapterToolSettingsStore`、`ExternalToolLocator`、`ProcessRunner`、`FfprobeMediaChapterReader` 和 `AtlMp4ChapterReader`。
- `src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs` 把 Core 导入器和 Infrastructure 导入器按路径扩展名/目录形态注册在一起。
- `src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs` 负责从真实路径读取并转交注册表；`RuntimeChapterSaveService.cs` 负责本地输出目录、文件名分配和落盘前的导出结果处理。
- WASM 不经过这些类型。浏览器文件由 `InputFile`/拖放事件读成 `byte[]`，由 `src/ChapterTool.Wasm/Services/WasmChapterService.cs` 包成 `ChapterImportRequest.Content`；导出内容由 `src/ChapterTool.Wasm/wwwroot/js/download.js` 触发浏览器下载。

## 4. 功能对照总表

### 4.1 输入与导入

| 功能 | Avalonia + Infrastructure | WASM | 状态 | 主要入口 |
| --- | --- | --- | --- | --- |
| OGM/TXT 章节 | 本地 `.txt`，Core `TextChapterImporter` 内部区分 OGM/Premiere | `.txt` 或无扩展名文本，字节流交给同一 Core `TextChapterImporter` | `[一致]` | `src/ChapterTool.Core/Importing/Text/TextChapterImporter.cs` |
| Premiere marker CSV | `.csv` 由桌面注册表直接选择 `PremiereMarkerListImporter` | `.csv` 会落入 WASM text importer，由内容识别 Premiere 格式；显式支持范围较弱 | `[部分一致]` | `RuntimeChapterImporterRegistry.cs`、`WasmChapterService.cs` |
| Matroska XML | `.xml` 走 Core `XmlChapterImporter` | `.xml` 走同一 Core importer | `[一致]` | `src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs` |
| WebVTT | `.vtt` 走 Core `WebVttChapterImporter` | `.vtt` 走同一 Core importer | `[一致]` | `src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs` |
| CUE 文本 | `.cue` 走 Core `CueChapterImporter` | `.cue` 走同一 Core importer | `[一致]` | `src/ChapterTool.Core/Importing/Cue/CueChapterImporter.cs` |
| MPLS | 本地路径由 Core `MplsChapterImporter` 读取 | 文件字节由同一 Core importer 读取 | `[一致]` | `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs` |
| DVD IFO | 本地路径/字节均由 Core `IfoChapterImporter` 支持 | 文件字节由同一 Core importer 读取 | `[一致]` | `src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs` |
| HD-DVD XPL | 桌面注册表支持 `.xpl` | WASM resolver 未显式接入，通常会回退到文本 importer | `[桌面独有]` / `[缺口]` | `src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs`、`src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs` |
| FLAC 内嵌 CUE | 桌面注册表支持 `.flac`，Core `FlacCueImporter` | WASM resolver 未接入 `.flac` | `[桌面独有]` / `[缺口]` | `src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs` |
| TAK 内嵌 CUE | 桌面注册表支持 `.tak`，Core `TakCueImporter` | WASM resolver 未接入 `.tak` | `[桌面独有]` / `[缺口]` | `src/ChapterTool.Core/Importing/Cue/TakCueImporter.cs` |
| BDMV 目录 | 识别 `BDMV/PLAYLIST` 目录并调用 Infrastructure `BdmvChapterImporter` | 浏览器只能选择文件，不能读取本地目录结构和运行 eac3to | `[桌面独有]` | `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvChapterImporter.cs` |
| Matroska/媒体文件 | `.mkv/.mka/.webm` 由 mkvextract 适配器处理；音视频格式由 ffprobe 处理；MP4 有 ATL fallback | 未接入 Infrastructure，也没有浏览器媒体元数据适配器 | `[桌面独有]` | `src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs`、`src/ChapterTool.Infrastructure/Importing/Media/` |
| 导入回退策略 | ffprobe -> ATL MP4、Matroska -> ffprobe、FLAC -> ffprobe | 无外部依赖回退链 | `[桌面独有]` | `RuntimeChapterImporterRegistry.ResolveFallback` |
| 重新加载 | 保留源路径，由桌面工作流重新读取 | 保留最近一次 `FileName + byte[]` 快照重新导入 | `[部分一致]` | `LoadSaveWorkflow.cs`、`WasmWorkspace.ReloadAsync` |
| MPLS 追加 | 支持已有 MPLS 会话追加并由 Core `ChapterSegmentService.Append` 合并 | 同样调用 Core 追加，但只接受浏览器再次选择的 MPLS 文件 | `[部分一致]` | `Session/ClipSession.cs`、`WasmWorkspace.AppendMplsAsync` |
| 文件拖放 | Avalonia 文件拖放到窗口后交给路径加载 | 浏览器拖放读取字节并受 64 MB 限制 | `[部分一致]` | `Views/MainWindow.axaml.cs`、`Pages/Home.razor` |

### 4.2 章节会话与编辑

| 功能 | Avalonia | WASM | 状态 | 主要入口 |
| --- | --- | --- | --- | --- |
| 多组/多条目选择 | `ClipSession` + `DisplayOptionCoordinator` | `WasmWorkspace.ClipOptions` | `[部分一致]` | `src/ChapterTool.Avalonia/Session/ClipSession.cs`、`src/ChapterTool.Wasm/Services/WasmWorkspace.cs` |
| MPLS/IFO 片段合并与恢复 | `ChapterSegmentService.Combine`，状态保存于 `ClipSession` | 同一 Core service，状态保存于 `WasmWorkspace` | `[一致]` |
| 时间编辑 | `ChapterEditingService` + DataGrid cell edit | `ChapterEditingService` + HTML input | `[一致]` |
| 名称编辑 | 同上 | 同上 | `[一致]` |
| 插入/删除 | `InsertCommand`、`DeleteCommand` | `InsertBefore`、`DeleteSelectedRows` | `[一致]` |
| 多选 | DataGrid 选中行同步到 ViewModel | Ctrl/Shift 选择由 `Home.razor` + `WasmWorkspace.SelectRow` 处理 | `[部分一致]` |
| 复制行/名称/zones | Avalonia 通过剪贴板服务和工具窗口提供 | 浏览器通过 JS Clipboard API 或下载/复制行为提供 | `[部分一致]` |
| `--zones` | `MainWindowViewModel.CreateZonesText` + `TextToolView` | `CreateZonesForSelection` + 上下文菜单 | `[一致]` |
| 帧前移 | Avalonia `ForwardShiftToolView` / `IChapterEditPort` | 浏览器前移弹窗调用 `ShiftFramesForward` | `[一致]` |
| 关联媒体 | Avalonia 可通过 `IShellService` 打开本地路径 | WASM 只展示相对/绝对路径，不能打开本地绝对路径 | `[部分一致]` |
| 诊断与日志 | Core diagnostics + `ApplicationLogPanelProvider` + UI 日志窗口 | `WasmWorkspace.Diagnostics` + 内存日志模态框 | `[部分一致]` |
| 进度 | Core `IChapterImportProgressReporter` 经桌面 workflow 转 UI | WASM 只在读入/解析阶段更新粗粒度进度值 | `[部分一致]` |

### 4.3 帧率、投影和导出

| 功能 | Avalonia | WASM | 状态 | 主要入口 |
| --- | --- | --- | --- | --- |
| 自动/固定帧率检测 | Core `FrameRateService`，桌面 `ClipEditingCoordinator` 编排 | 同一 Core `FrameRateService`，`WasmWorkspace` 编排 | `[一致]` |
| 帧信息、取整、精度标记 | Core `FrameRateService.UpdateFrames` | 同一 Core 方法 | `[一致]` |
| 章节名：保持原名 | `ProjectionState` / `ProjectionFacade` | `ChapterNameModeIndex = 0` | `[一致]` |
| 章节名：自动生成 | 同一 Core `ChapterOutputProjectionService` | 同一 Core service | `[一致]` |
| 章节名：模板 | 桌面 `ChapterNameTemplateReader` 读取本地文本 | 浏览器通过 `InputFile` 读取文本 | `[部分一致]` |
| 序号偏移 | Core projection，两个宿主均有选项 | 同上 | `[一致]` |
| Lua 表达式执行 | Avalonia 共享 `LuaExpressionScriptService`，并提供编辑器、补全、诊断 | WASM 执行受支持的 Core/Lua 表达式，但只有普通文本输入 | `[部分一致]` |
| 表达式编辑辅助 | `ExpressionEditor`、`ExpressionAuthoringService`、completion/diagnostic presentation | 未提供编辑器、补全和实时诊断 | `[桌面独有]` / `[缺口]` |
| 导出格式 | TXT、XML、QPFile、TimeCodes、tsMuxeR、CUE、JSON、WebVTT、Celltimes | 通过同一 Core `ChapterExportFormats.All` 暴露全量格式 | `[一致]` |
| XML language | Core catalog，桌面本地化显示 | Core catalog，WASM 显示语言代码 | `[部分一致]` |
| 文本编码/BOM | Core export options + 桌面落盘服务 | Core export options + 浏览器下载 | `[部分一致]` |
| 预览 | 桌面 `BuildPreview` / export service | `WasmWorkspace.Preview`，与 Save 使用相同投影导出路径 | `[一致]` |
| 保存 | 生成不冲突的本地文件路径并写盘 | 生成下载文件名并交给 JS 下载 | `[部分一致]` |

### 4.4 设置、外观和平台能力

| 功能 | Avalonia + Infrastructure | WASM | 状态 | 主要入口 |
| --- | --- | --- | --- | --- |
| 设置聚合模型 | `ChapterToolSettings`，包含 `Application`、`Theme`、`Font` | `WasmSettings`，结构基本对齐 | `[部分一致]` |
| 持久化载体 | `ChapterToolSettingsStore` 写版本化 `settings.json`，含锁、规范化、迁移和损坏处理 | `localStorage` 保存 JSON 快照，当前逻辑在 `Home.razor` | `[部分一致]` |
| 默认导出格式/XML/编码/BOM/精度容差 | 可持久化并接入启动/设置窗口 | 可持久化并接入浏览器设置弹窗 | `[部分一致]` |
| 保存目录 | 桌面目录选择器 + 本地文件落盘 | 浏览器默认下载目录，不能选择应用控制的目录 | `[桌面独有]` |
| 外部工具路径与校验 | mkvtoolnix、eac3to、ffprobe、ffmpeg 的路径配置、发现、校验 | 控件保留为不可用说明，不执行校验 | `[桌面独有]` |
| 主题预设 | Infrastructure `ThemePresetCatalog` + Avalonia 主题应用服务 | WASM 自己维护主题预设和 CSS 颜色 | `[部分一致]` |
| 系统字体枚举 | Avalonia `AvaloniaFontFamilyCatalog` 读取系统字体 | 只提供 CSS 字体栈选项 | `[部分一致]` |
| 语言切换 | `.resx` + `AppLocalizationManager`，动态刷新控件资源 | `WasmLocalizer` 字典，动态刷新工作区状态和页面 | `[部分一致]` |
| 设置目录/窗口位置 | 桌面可打开设置目录，并可持久化窗口位置 | 不适用；浏览器不开放本地 Shell | `[桌面独有]` |
| 剪贴板 | Infrastructure `IClipboardService`，Avalonia adapter | JS Clipboard API，受浏览器权限限制 | `[部分一致]` |
| Shell/打开关联媒体 | Infrastructure `IShellService` | 不提供本地 Shell | `[桌面独有]` |
| CLI | Avalonia 项目内 DotMake.CommandLine 入口 | 无 CLI | `[桌面独有]` |

## 5. Core 共享行为清单

下面这些类型是跨宿主保持一致的优先修改点：

- 模型与诊断：`Core/Models/`、`Core/Diagnostics/`。
- 导入协议：`ChapterImportRequest`、`IChapterImporter`、`ChapterImportResult` 和 `ChapterImportProgress`。
- 文本/磁盘格式导入：`Core/Importing/Text/`、`Core/Importing/Cue/`、`Core/Importing/Disc/`。
- 编辑与片段：`Core/Editing/ChapterEditingService.cs`、`ChapterSegmentService.cs`。
- 帧处理：`Core/Transform/FrameRateService.cs`、`ChapterFpsTransformService.cs`、`ChapterRounding.cs`。
- 投影与表达式：`Core/Exporting/ChapterOutputProjectionService.cs`、`Core/Transform/ChapterExpressionService.cs`、`Core/Transform/Expressions/`。
- 导出：`Core/Exporting/ChapterExportService.cs`、`ChapterExportFormats.cs`、`ChapterExportOptions.cs`、`OutputTextEncoding.cs`。

只要需求是“章节语义、解析规则、编辑结果或导出内容改变”，默认检查 Core 测试，并确认两端都仍然调用该 Core 能力。不要先在 `WasmWorkspace` 或 `MainWindowViewModel` 中复制一份算法。

## 6. 当前最需要留意的差异

1. **导入器注册不对称**：WASM `WasmChapterService.ResolveImporter` 当前显式处理 `.vtt/.xml/.cue/.mpls/.ifo`，其余回退到 text；桌面注册表另外支持 `.csv/.flac/.tak/.xpl`、媒体文件、Matroska 和 BDMV。新增 Core importer 后，必须决定是否也要加到 WASM resolver。
2. **表达式只有执行一致**：WASM 可以应用表达式，但没有 Avalonia 的 `ExpressionEditor`、补全、诊断展示和编辑器键盘行为。若修改表达式语法或可用函数，要同时验证执行端和编辑辅助端。
3. **设置模型相似但不是同一持久化实现**：桌面是 `settings.json`，WASM 是 `localStorage`。新增设置字段要同步两种快照、默认值、规范化、版本迁移和 UI 应用逻辑。
4. **主题/字体没有共享渲染层**：桌面依赖 Avalonia 资源和系统字体；WASM 依赖 CSS。新增主题色或字体语义时，不应只修改一端的颜色名，还要同步另一端的视觉映射。
5. **相关媒体不是同等能力**：桌面可按相对路径解析并交给 Shell；WASM 只能展示信息或链接。浏览器端不应声称可以打开本地绝对路径。
6. **保存语义不同**：桌面保存到用户选择/配置的目录并分配实际路径；WASM 只能生成下载。新增文件名、扩展名或编码规则必须同时检查 Core export result、桌面路径处理和 JS 下载。
7. **WASM 端有独立的 64 MB 输入上限**：桌面没有同一限制。调整上限时要同步浏览器内存/DoS 风险、错误提示和相关测试。

## 7. 修改功能时的跟踪流程

### 7.1 需求分类

先判断变更属于哪一类：

- 章节语义/格式解析/编辑/帧率/投影/导出：先查 `ChapterTool.Core`。
- 本地文件、目录、外部工具、进程、设置文件、Shell、系统字体：查 `Infrastructure`，再查 Avalonia composition/service。
- 桌面交互、快捷键、DataGrid、工具窗口、资源本地化：查 `ChapterTool.Avalonia`。
- 浏览器文件输入、拖放、下载、localStorage、CSS、浏览器限制：查 `ChapterTool.Wasm`。
- 主工作区命令或输出选项：必须同时检查 `MainWindowViewModel*.cs` 和 `WasmWorkspace.cs`/`Home.razor`。

### 7.2 修改前检查点

- 是否已有 Core service 或 importer 可以复用？
- Avalonia `RuntimeChapterImporterRegistry` 是否需要新扩展名或 fallback？
- WASM `WasmChapterService.ResolveImporter` 是否需要接入同一 Core importer？
- 两端导出选项是否都有对应字段、默认值和刷新路径？
- 桌面 `ChapterToolSettings` 与 WASM `WasmSettings` 是否需要同步字段和版本处理？
- 是否需要同步英文、中文、日文文本？
- 是否需要更新 `docs/code-map/` 中的 ownership、entry point 或测试入口？

### 7.3 修改后验证矩阵

| 变更类型 | 最小验证 |
| --- | --- |
| Core 解析/编辑/投影/导出 | `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`；WASM 和 Avalonia 端至少各跑一条宿主回归路径 |
| Infrastructure 导入/工具/设置 | `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`；再验证 Avalonia 组合/设置入口 |
| Avalonia ViewModel/服务/CLI | `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore` |
| Avalonia XAML/交互 | `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore` |
| WASM 工作区/浏览器入口 | `dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore`；必要时 `dotnet build src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-restore` |
| 多层共享行为或发布前变更 | 按仓库规定顺序运行全套测试：不要并行运行多个测试项目 |

UI 变更还应在 `artifacts/` 保存默认、宽窗口和窄窗口截图；截图是人工布局证据，不替代行为测试。

## 8. 维护规则

- 新增或修改 Core 功能时，在本表补充“共享行为”和两端入口。
- 新增桌面 Infrastructure 能力时，明确标注 WASM 是“不可用”“待实现”还是“有浏览器替代方案”，不要只写“WASM 不支持”。
- 新增 WASM 能力时，明确它是浏览器特有交互还是应反向抽到 Core/共享协议。
- 功能完成后同步更新本文件的“最后核对日期”、状态标记和测试入口。
- 如果入口、模块 ownership、组合根或主测试文件改变，同时更新 `docs/code-map/README.md`、`core.md`、`infrastructure.md`、`avalonia.md` 或 `testing.md` 中对应条目。
- 本文是架构/功能追踪文档，不记录一次性实现过程、临时 workaround 或已归档变更。
