# .NET 单元测试覆盖率分析报告

> 生成日期：2026-07-30
> 工具：coverlet (XPlat Code Coverage) + ReportGenerator
> 数据来源：所有 6 个测试项目的联合覆盖率

---

## 总体概览

| 指标 | 数值 |
|------|------|
| **行覆盖率 (Line)** | **83.7%** (10052 / 12009) |
| **分支覆盖率 (Branch)** | **68.6%** (2954 / 4301) |
| 可覆盖行 | 12,009 |
| 未覆盖行 | 1,957 |
| 总行数 | 20,935 |
| 程序集 | 6 |
| 类 | 249 |
| 源文件 | 207 |

**小结：** 整体行覆盖率 83.7% 属良好水平，但分支覆盖率 68.6% 偏低，说明大量条件逻辑（if/switch/三元）缺少足够的测试覆盖。以下按模块逐一分析。

---

## 1. ChapterTool.Core — 行覆盖率 **89.9%** / 分支 **79.1%**

综合评价：✅ **优秀**，是项目中测试覆盖最充分的模块。但仍有 421 行未覆盖。

### 低覆盖率类

| 类 | 行覆盖率 | 分支覆盖率 | 未覆盖行 | 风险 |
|----|---------|-----------|---------|------|
| `MplsUOMaskTable` | **3.1%** | 0% | 31 | 🔴 |
| `ChapterImportFormats` | **32.1%** | 26.9% | 19 | 🔴 |
| `MplsContainerReadExtensions` | **0%** | 0% | 7 | 🟡 |
| `BufferedChapterSource` | **50%** | — | 1 | 🟢 |
| `MplsClipName` | **50%** | — | 1 | 🟢 |
| `ChapterRounding` | **66.6%** | — | 1 | 🟢 |
| `MplsSubPlayItem` | **76%** | 25% | 12 | 🟡 |
| `BinaryReadExtensions` | **76.4%** | 72.7% | 8 | 🟢 |
| `MplsBoundedStream` | **76.9%** | 58.8% | 15 | 🟡 |
| `MplsAppInfoPlayList` | **77.2%** | — | 5 | 🟢 |
| `ChapterEditingService` | **78.2%** | 56.2% | 34 | 🟡 |
| `ExpressionCompletion` | **77.7%** | 50% | 4 | 🟢 |
| `ChapterTimeFormatter` | **81.6%** | 95% | 11 | 🟢 |

### 📈 提升建议

1. **MplsUOMaskTable (3.1%)** — Blu-ray 解析中的 UO (User Operation) mask 表解析逻辑。需要构造包含 UO mask 的测试用 MPLS 二进制数据来覆盖。
2. **ChapterImportFormats (32.1%)** — 格式枚举元数据（DisplayName/Code 等反射辅助方法）。可通过参数化测试覆盖所有枚举值的映射。
3. **MplsContainerReadExtensions / MplsBoundedStream (58.8% 分支)** — MPLS 容器格式的扩展读取方法。需要更多异常路径测试（流截断、无效标记）。
4. **MplsSubPlayItem (25% 分支)** — 子播放项解析中的条件分支。需要构造带/不带子路径的 MPLS 文件测试。
5. **ChapterEditingService (56.2% 分支)** — 编辑服务的条件逻辑。需要覆盖编辑冲突、边界条件等场景。
6. **ChapterContentService (64.2% 分支)** — 导入器调度逻辑。对 `ResolveImporter` 中的 format 映射分支补充测试。

---

## 2. ChapterTool.Infrastructure — 行覆盖率 **79.3%** / 分支 **63.0%**

综合评价：🟡 **中等**。基础设施层（平台服务、配置、进程管理）覆盖不足，分支覆盖率偏低的主要拖累项。

### 低覆盖率类

| 类 | 行覆盖率 | 分支覆盖率 | 未覆盖行 | 风险 |
|----|---------|-----------|---------|------|
| `DotNetHost` | **0%** | 0% | 1 | 🟢 |
| `WindowsRegistryInstallProbe` | **0%** | 0% | 32 | 🔴 |
| `ShellService` | **45.1%** | 25% | 57 | 🔴 |
| `ExternalToolPathResolver` | **45%** | 35% | 33 | 🔴 |
| `CorruptSettingsFile` | **50.5%** | 16.6% | 42 | 🔴 |
| `FileSystemNativeDependencyService` | **56.2%** | 25% | 7 | 🟡 |
| `MkvToolNixInstallProbe` | **66.6%** | 50% | 3 | 🟢 |
| `MacMkvToolNixInstallProbe` | **77.7%** | 78.5% | 4 | 🟢 |
| `ExternalToolLocator` | **80.7%** | 77% | 21 | 🟡 |

### 📈 提升建议

1. **ShellService (45.1%, 25% 分支)** — 打开终端/执行 Shell 命令的平台服务。
   - **问题：** 依赖实际的 Shell 环境，难以在隔离环境中测试。
   - **建议：** 提取 `IProcessRunner` 接口，对 Shell 命令构建逻辑单独做单元测试；`OpenTerminalAsync` 中的平台判断分支（macOS/Linux/Windows）通过 mock 测试覆盖。

2. **ExternalToolPathResolver (45%, 35% 分支)** — 外部工具路径搜索。
   - **问题：** 搜索逻辑依赖文件系统存在性。
   - **建议：** 将文件系统操作抽象为 `IFileSystem` 接口；用 mock 覆盖各种搜索路径存在/缺失的组合。`DefaultCandidateDirectories` 和 `WindowsProgramRoots` 的迭代器目前几乎完全未测试。

3. **CorruptSettingsFile (50.5%, 16.6% 分支)** — 损坏配置文件的修复逻辑。
   - **问题：** 文件损坏的并发写回逻辑复杂，条件分支多（16.6% 分支覆盖率）。
   - **建议：** 参数化测试覆盖：正常 JSON / 空文件 / 非法 JSON / 版本不匹配 / 并发冲突等场景。`TryGetConcurrentPreservation` 和 `ExistingBackupPath` 条件分支需全覆盖。

4. **WindowsRegistryInstallProbe (0%)** — Windows 注册表探测（平台特有）。
   - **建议：** 引入 `IRegistry` 抽象进行 mock 测试。若仅在 Windows 运行 CI，可添加条件测试直接验证。

5. **FileSystemNativeDependencyService (56.2%, 25% 分支)** — 文件系统原生依赖探测。
   - **建议：** 同时 Mock 文件系统和运行时标识，覆盖 macOS/Linux/Windows 三平台路径。

6. **BdmvImporter** — BDMV 目录结构解析。
   - **问题：** 仍有 42 行未覆盖，主要在异常路径。
   - **建议：** 构造不完整/无效的 BDMV 目录结构测试异常处理。

---

## 3. ChapterTool (CommandLine) — 行覆盖率 **76.5%** / 分支 **64.6%**

综合评价：🟡 **中等偏下**。CLI 应用的主体逻辑基本覆盖，但子命令和辅助工具覆盖不足。

### 低覆盖率类

| 类 | 行覆盖率 | 分支覆盖率 | 未覆盖行 |
|----|---------|-----------|---------|
| `InspectCliCommand` | **0%** | 0% | 6 |
| `SystemCliConsole` | **50%** | — | 2 |
| `CliInputResolver` | **100%** | 50% | 0 |
| `CliLocalizationResources` | **100%** | 50% | 0 |

### 主要问题

| 方法 | Crap Score | 圈复杂度 |
|------|-----------|---------|
| `ChapterToolCliApplication.ImportAsync()` | 84 | 14 |
| `ChapterToolCliApplication.ConvertAsync()` | 26 | 24 |
| `ChapterToolCliApplication.ResolveDefaultOutputDirectoryAsync()` | 42 | 6 |

### 📈 提升建议

1. **InspectCliCommand (0%)** — `inspect` 子命令完全未测试。
   - **建议：** 添加参数化测试，验证不同格式的 inspect 输出。

2. **ChapterToolCliApplication (71%)** — 111 行未覆盖主要是 `ImportAsync` / `ConvertAsync` 中的交互选择逻辑。
   - **建议：** 对 `ImportAsync` 中的格式选择、确认提示等交互路径补充测试，Mock 控制台输入。

3. **SystemCliConsole (50%)** — 控制台包装器。

4. **分支覆盖缺口：** `CliInputResolver`, `CliLocalizationResources`, `ConvertCliCommand` 的行覆盖 100% 但分支覆盖率仅 50%，说明有条件分支未测到。

---

## 4. ChapterTool.Avalonia — 行覆盖率 **75.4%** / 分支 **56.4%**

综合评价：🟡 **中等**。分支覆盖率 56.4% 是全部模块中最低的。大量平台服务依赖 Avalonia 运行时，增加了测试难度。

### 低覆盖率类

| 类 | 行覆盖率 | 分支覆盖率 | 未覆盖行 | 风险 |
|----|---------|-----------|---------|------|
| `Program` | **0%** | 0% | 50 | 🔴 |
| `AvaloniaSettingsCloseConfirmationService` | **1.4%** | — | 67 | 🔴 |
| `AvaloniaClipboardService` | **8.3%** | 0% | 11 | 🔴 |
| `AvaloniaFilePickerService` | **51.5%** | 0% | 46 | 🔴 |
| `AvaloniaSettingsPickerService` | **50%** | 16.6% | 14 | 🟡 |
| `App` | **61.5%** | 50% | 5 | 🟢 |
| `AvaloniaFontFamilyCatalog` | **73.7%** | 56.2% | 16 | 🟡 |

### 📈 提升建议

1. **Program (0%) / App (61.5%)** — 应用入口点。
   - **建议：** 这两个类的未覆盖代码主要是启动配置逻辑，低优先级（通常不需测试入口点）。`App.OnFrameworkInitializationCompleted()` 可提取配置逻辑到可测试的服务。

2. **AvaloniaSettingsCloseConfirmationService (1.4%)** — 设置修改确认弹窗（67 行未覆盖）。
   - **建议：** 通过 Avalonia Headless 测试框架验证弹窗交互逻辑。Mock `IStorageProvider`。

3. **AvaloniaClipboardService (8.3%, 0% 分支)** — 剪贴板操作。
   - **建议：** 在 Headless 测试中用 `Window.Clipboard` 进行集成测试，或者提取 `IClipboard` 接口做单元测试。

4. **AvaloniaFilePickerService (51.5%, 0% 分支)** — 文件选择器。
   - **问题：** 很多异步方法（`PickSourceAsync`, `PickMplsAsync` 等）完全未覆盖，因为依赖文件对话框交互。
   - **建议：** `Pick*Async` 方法提取为 `IFilePickerService` 接口，将业务逻辑与 UI 解耦。

5. **AvaloniaSettingsPickerService (50%, 16.6% 分支)** — 设置目录/可执行文件选择器。

6. **AvaloniaThemeApplicationService (96.9% 行，但 91.6% 分支)** — 分支未覆盖的 `ApplyImportedThemeColors` (Crap 22)。

### 🧪 测试策略建议

Avalonia 模块的建议采用分层测试策略：
- **纯逻辑类**（如 `ToolWindowRegistry`, `ToolWindowRegistration`）：已有 100% 覆盖，继续保持。
- **服务类**（Picker / Clipboard / Shell）：提取接口 + Mock 框架覆盖业务逻辑。
- **UI 绑定类**（WindowService 等）：通过 Headless 测试框架（已有 `ChapterTool.Avalonia.Headless.Tests` 项目）集成测试。

---

## 5. ChapterTool.Avalonia.UI — 行覆盖率 **81.8%** / 分支 **62.1%**

综合评价：🟢 **良好**。ViewModels 覆盖较充分，但 Views（XAML 绑定层）仍有较大的覆盖盲区。

### 低覆盖率类

| 类 | 行覆盖率 | 分支覆盖率 | 未覆盖行 | 风险 |
|----|---------|-----------|---------|------|
| `LocalizerExtensions` | **0%** | — | 13 | 🟡 |
| `ForwardShiftToolView` | **0%** | — | 4 | 🟢 |
| `LanguageToolView` | **0%** | — | 4 | 🟢 |
| `TemplateNamesToolView` | **0%** | — | 4 | 🟢 |
| `UiOperationBoundary` | **46.1%** | — | 7 | 🟡 |
| `IFilePickerService` | **44.4%** | 25% | 5 | 🟢 |
| `LanguageToolViewModel` | **54.5%** | 20% | 30 | 🟡 |
| `XmlLanguageDisplay` | **56.4%** | 50% | 17 | 🟡 |
| `AppLocalizationResources` | **60.7%** | 35.7% | 20 | 🟡 |
| `BrowserChapterSourcePicker` | **65.1%** | 57.1% | 15 | 🟡 |
| `MainView` | **65.6%** | 35.6% | 127 | 🔴 |
| `TextToolView` | **67.2%** | 47.3% | 36 | 🟡 |
| `ExpressionEditor` | **68.8%** | 57.4% | 155 | 🔴 |
| `LoadSaveWorkflow` | **68.4%** | 32.3% | 23 | 🟡 |
| `BrowserSettingsCodec` | **82%** | 50% | 7 | 🟢 |

### 高风险热点

| 方法 | Crap Score | 圈复杂度 |
|------|-----------|---------|
| `MainView.Gesture()` | **918** | **68** |
| `MainView.CommitCellEditAsync()` | **272** | 16 |
| `ExpressionEditor.HandleCompletionKeys()` | **204** | 34 |
| `ExpressionEditor.OnTextViewPointerMoved()` | **156** | 12 |
| `ShortcutRouter.RouteAsync()` | **138** | 36 |
| `AppendSourceAsync()` | **95** | 12 |
| `MainWindowViewModel.OpenRelatedMediaAsync()` | **106** | 28 |

### 📈 提升建议

1. **MainView (65.6%, 35.6% 分支)** — 主视图代码后置逻辑。
   - **问题：** `Gesture()` 方法圈复杂度 68，Crap Score 918（全项目最高），包含大量手势/交互处理。
   - **建议：** 将手势逻辑拆分为独立的策略类；通过 Headless 测试框架发送模拟手势覆盖不同交互路径。优先覆盖 `CommitCellEditAsync`、`IsTextInputVisual` 等常见操作。

2. **ExpressionEditor (68.8%, 57.4% 分支)** — 表达式编辑器控件（155 行未覆盖）。
   - **问题：** `HandleCompletionKeys`（圈复杂度 34）、`OnTextViewPointerMoved`（圈复杂度 12）交互逻辑复杂。
   - **建议：** 将自动补全/悬停提示等逻辑提取到独立的 ViewModel 服务，用单元测试覆盖；复杂 UI 交互通过 Headless 测试覆盖。

3. **LanguageToolViewModel (54.5%, 20% 分支)** — 语言工具 VM，`SelectedLanguageIndex` setter 的 Crap Score 42。
   - **问题：** 语言切换的选择逻辑和状态同步未充分测试。
   - **建议：** 覆盖语言列表为空、切换语言、索引越界等场景。

4. **LoadSaveWorkflow (68.4%, 32.3% 分支)** — 加载/保存工作流。
   - **建议：** 覆盖各 Append 阶段（来源追加、文件选择、导入失败等）的完整工作流路径。

5. **AppLocalizationResources (60.7%, 35.7% 分支)** — 本地化资源加载。
   - **建议：** 覆盖文化回退链（`LoadCulture`），无效文化名等边缘情况。

6. **ShortcutRouter.RouteAsync (Crap 138, 圈复杂度 36)** — 快捷键路由。
   - **建议：** 参数化测试覆盖所有注册快捷键的按键组合。

7. **Views/Tools 中的 0% 文件（4 个 Tool View）** — XAML Code-behind 的 4 行构造函数和初始化。
   - **建议：** 低优先级，但如果实现功能测试，可纳入 Headless 测试。

---

## 6. ChapterTool.Contracts — 行覆盖率 **98.4%** / 分支 **83.3%**

综合评价：✅ **优秀**。接口、配置模型和数据结构定义，仅 2 行未覆盖。分支覆盖率缺口在 `ChapterToolSettings` 的 getter/setter 条件逻辑中。

### 📈 提升建议
- 对 `ChapterToolSettings` 中 50% 分支覆盖的验证属性补充参数化测试。

---

## 7. 未被覆盖率统计覆盖的项目

### ChapterTool.Node
- **状态：** ❌ 没有测试项目
- **影响：** 该项目的代码完全未被测试覆盖
- **建议：** 若项目有实际业务逻辑（非仅仅是 Node.js 互操作包装器），应添加基本的集成测试

### ChapterTool.Wasm
- **状态：** ⚠️ 有测试项目 (ChapterTool.Wasm.Tests) 但覆盖率未采集
- **说明：** 20 个测试全部通过，但由于 Wasm 是 Blazor WebAssembly 项目，coverlet 无法正常收集覆盖率
- **建议：** 如果 Wasm 项目包含重要业务逻辑，考虑将其提取到共享库（如 ChapterTool.Core）测试，或使用 bUnit 等专用框架进行 Blazor 组件测试

---

## 整体提升优先级排序

| 优先级 | 模块 | 目标 | 预期收益 | 估算工作量 |
|--------|------|------|---------|-----------|
| P0 🔴 | `ChapterTool.Infrastructure.ShellService` | ≥80% | 平台兼容性保障 | 小 |
| P0 🔴 | `ChapterTool.Infrastructure.CorruptSettingsFile` | ≥80% | 配置文件鲁棒性 | 中 |
| P0 🔴 | `ChapterTool.Avalonia.UI.MainView.Gesture` | 拆解+覆盖 | 降低主交互风险 | 大 |
| P1 🟡 | `ChapterTool.Avalonia.UI.MainView` | ≥70% | 提升主视图可靠性 | 大 |
| P1 🟡 | `ChapterTool.Avalonia.UI.ExpressionEditor` | ≥70% | 提升编辑器可靠性 | 大 |
| P1 🟡 | `ChapterTool.Avalonia.FilePickerService` | ≥70% | 服务层可测试性 | 中 |
| P1 🟡 | `ChapterTool.Infrastructure.ExternalToolPathResolver` | ≥80% | 工具发现可靠性 | 中 |
| P2 🟢 | `ChapterTool.Core.Mpls*` 系列 | ≥85% | 解析器鲁棒性 | 中 |
| P2 🟢 | `ChapterTool.CommandLine.InspectCliCommand` | ≥80% | CLI 完整覆盖 | 小 |
| P3 ⚪ | `ChapterTool.Node` | 新建测试项目 | — | 视规模而定 |
| P3 ⚪ | `ChapterTool.Avalonia.Program/App` | 低优先级 | — | 小 |

---

## 测试基础设施建议

1. **统一覆盖率采集配置**
   - 在仓库根目录创建 `coverlet.runsettings`，统一 `ExcludeByAttribute`、`Exclude` 等配置
   - 某些测试项目（Core.Tests / Infra.Tests）使用 MSTest，其覆盖率采集方式与 xUnit + coverlet 不同。建议统一为 xUnit + coverlet

2. **生成可视化报告**
   - CI 中使用 `reportgenerator` 生成 HTML 报告并发布
   - 设置覆盖率门禁（如 Branch ≥ 60%，Line ≥ 75%）

3. **分模块覆盖率阈值**
   - Core/Contracts：Line ≥ 90%
   - Infrastructure/CommandLine：Line ≥ 80%
   - Avalonia/Avalonia.UI：Line ≥ 75%

4. **平台相关代码的测试策略**
   - Windows 注册表 (`WindowsRegistryInstallProbe`)：提取 `IRegistry` 接口 mock
   - Shell 命令 (`ShellService.OpenTerminalAsync`)：提取 `IProcessRunner` 接口 mock
   - 文件系统 (`ExternalToolPathResolver`, `FileSystemNativeDependencyService`)：提取 `IFileSystem` 接口 mock
   - 跨平台路径逻辑：参数化测试分别测试 macOS/Linux/Windows 风格路径

5. **UI 测试增强**
   - 已有 Headless 测试框架（`ChapterTool.Avalonia.Headless.Tests`），可以大幅扩展对 View/ViewModel 交互的测试
   - 对 `Gesture()` 等复杂交互方法，考虑提取策略模式，将业务逻辑与 UI 事件解耦

---

## 总结

```
Module                Lines       Coverage    Branch      Key Risk
─────────────────────────────────────────────────────────────────
ChapterTool.Contracts   129         98.4%      83.3%       ─
ChapterTool.Core      4,198         89.9%      79.1%       MplsUOMaskTable, ImportFormats
ChapterTool.Avalonia.UI 4,544       81.8%      62.1%       MainView, ExpressionEditor
ChapterTool.Infrastructure 1,465    79.3%      63.0%       ShellService, CorruptSettingsFile
ChapterTool (CLI)       534         76.5%      64.6%       InspectCliCommand
ChapterTool.Avalonia  1,139         75.4%      56.4%       Clipboard, FilePicker, CloseConfirm
─────────────────────────────────────────────────────────────────
Total               12,009         83.7%      68.6%
```

**关键发现：**
- 行覆盖率 83.7% 整体良好，**分支覆盖率 68.6% 是核心薄弱点**，大量 `if/switch` 条件未经充分测试
- **`MainView.Gesture()`（圈复杂度 68）** 和 **`ExpressionEditor`（155 行未覆盖）** 是 UI 层最大的测试盲区，也是用户交互最多的地方
- 平台相关代码（Shell/注册表/文件系统）因缺乏抽象层导致测试困难，提取接口 + Mock 是解决之道
- `ChapterTool.Node` 和 `ChapterTool.Wasm` 两个项目的测试覆盖为零或缺失
