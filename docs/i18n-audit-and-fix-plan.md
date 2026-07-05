# 国际化（i18n）排查与修复计划

> 生成日期：2026-07-04
> 范围：`ChapterTool.Avalonia` / `ChapterTool.Core` / `ChapterTool.Infrastructure`
> 现象：选择中文（或日文）后，部分日志、状态、诊断信息仍显示英文。

## 执行结果（截至 2026-07-04）

| 任务 | 状态 | 说明 |
|------|------|------|
| Task 1 — Diagnostic.* 本地化 | ✅ 已完成 | 新增 73 + 18 个 `Diagnostic.*` 键（三语）；`ChapterDiagnostic` 增加 `Arguments`；`LocalizeDiagnostic` 支持 `{message}` 自动回填与命名占位符；`LogDiagnostics` 改用本地化消息。 |
| Task 2 — operation/action 本地化 | ✅ 已完成 | 新增 `Action.*`(7)/`EditKind.*`(3)/`Operation.*`(6) 键；`ApplyEdit`、`LogDiagnostics` 全部调用点改用本地化字符串。 |
| Task 3 — ExpressionService 错误本地化 | ✅ 已完成 | 引入 `ExpressionException(code,message,args)`，18 个错误码 `InvalidExpression.*` 三语；测试断言改为 `StartsWith`。 |
| Task 4 — 外部工具/依赖缺失消息 | ✅ 已随 Task 1 完成 | `MissingDependency`/`MatroskaMissingDependency`/`FfprobeMissingDependency` 为静态本地化键（内嵌工具名），显示时覆盖定位器英文消息。 |
| Task 5 — 清理废弃本地化抽象 | ✅ 已完成 | 删除 `ILocalizationService`/`LocalizationService`（Core/Infrastructure）；从 `PlatformServiceTests` 移除对应测试块。无悬空引用。 |

**资源键总数**：每语言由 126 → **233** 键（三语严格对齐，占位符一致性测试守护）。
**测试**：Core 276 / Infrastructure 88 / Avalonia.Localization 11 全绿；Avalonia 全量（含 headless）在 Task 1-2 阶段已全绿（Task 3/5 仅触及 Core/Infrastructure）。

---

## 一、现状架构概览

| 维度 | 说明 |
|------|------|
| 资源文件 | `src/ChapterTool.Avalonia/Localization/Resources/Strings.{zh-CN,en-US,ja-JP}.resx`（各 126 键，键/占位符一致性有测试守护） |
| 默认/回退文化 | **zh-CN**（`AppLanguage.DefaultCultureName = "zh-CN"`，无中性 `Strings.resx`） |
| 活跃本地化服务 | `IAppLocalizer` / `AppLocalizationManager`（Avalonia 层），`SetCulture` 同时写入 `Application.Current.Resources` 供 `{DynamicResource}` 使用 |
| XAML | 全量使用 `{DynamicResource Key}`，UI 文本无硬编码英文 ✅ |
| 日志通道 | `MainWindowViewModel.Log(key,...)` 注入 `MessageKey`；显示时由 `FormatLogEntry` 经 `Localizer.Format` 重新本地化。**只有传入真实 `Log.*`/`Status.*` 键才会本地化**，传字面量英文则绕过本地化 |

### 文化切换链路（已正确）
`AppSettings.Language` → 启动 `MainWindowViewModel.LoadSettings` → `Localizer.SetCulture` → `CultureChanged` 事件 → 各 ViewModel 刷新 + XAML `{DynamicResource}` 自动更新 + 持久化。

## 二、缺陷清单（按优先级）

### P0 — 诊断信息（Diagnostic）完全不翻译 ⭐ 最大问题
- **位置**：`MainWindowViewModel.LocalizeDiagnostic`（`MainWindowViewModel.cs:1365`）
- **机制**：代码已构造 `Diagnostic.{Code}` 键查找，但 **三个 resx 文件中 `Diagnostic.*` 键数为 0**。
- **后果**：状态栏、日志面板对所有诊断一律回退到 `diagnostic.Message`（硬编码英文）。
- **涉及诊断码（约 60 个）**：`InvalidFrameRate`、`NoSegments`、`UnsupportedCombineSource`、`InvalidChapterIndex`、`InvalidExpression`、`PartialParse`、`EmptyCueFile`、`MalformedCueSyntax`、`MatroskaProcessFailed`、`FfprobeProcessFailed`、`Mp4ReadFailed`、`MissingDependency`、`Stdout` 等（完整清单见附录 A）。
- **来源文件**：`src/ChapterTool.Core/{Editing,Transform,Exporting,Importing}/...`、`src/ChapterTool.Infrastructure/{Platform,Importing,Tools}/...`

### P1 — 日志 operation/action 参数硬编码英文
- **A. `ApplyEdit(result, action)`**（`MainWindowViewModel.cs:890`）
  - `action` 作为本地化模板 `Log.EditChapters` 的 `{action}` 占位符，但调用方传入全英文：`$"Delete rows: indexes=..."`、`$"Insert row: index=..."`、`$"Shift frames forward: ..."`、`$"Edit {kind}: row=..."`、`$"Combine segments: ..."`。
  - → 模板被翻译，但内嵌的 `{action}` 片段恒为英文。
- **B. `LogDiagnostics(operation, ...)`**（`MainWindowViewModel.cs:1516`）
  - 多处调用传入英文字面量：`"Load"`、`"Save"`、`"Create zones"`、`"Append load"`、`"Append edit"`、`"Output projection"` 等（`{operation}` 占位符）。
  - 同时该函数把 `diagnostic.Message`（英文）直接塞入 `Log.Diagnostic` 的 `{message}` 占位符，**未走 `LocalizeDiagnostic`**。

### P2 — ExpressionService 异常消息全英文
- **位置**：`src/ChapterTool.Core/Transform/ExpressionService.cs:140,261,271,283,293,303,320,338,348`
- 抛出的英文消息（`"Misplaced comma."`、`"Unbalanced parentheses."`、`"Expression did not reduce to one value."` 等）经 `InvalidExpression` 诊断原样显示给用户。

### P3 — 外部工具/原生依赖缺失消息全英文
- `src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs:77`：`$"External tool '{toolId}' was not found."`
- `src/ChapterTool.Infrastructure/Platform/FileSystemNativeDependencyService.cs:21`：`$"Native dependency '{dependencyId}' was not found."`
- 被各 importer 原样透传为 `ChapterDiagnostic.Message`（code=`MissingDependency`）。

### P4 — 重复/废弃的本地化抽象（清理项）
- `src/ChapterTool.Core/Services/ILocalizationService.cs` + `src/ChapterTool.Infrastructure/Platform/LocalizationService.cs`
- 构造时资源字典为空，**未被 `AppCompositionRoot` 接入**，仅出现在测试中。与 `IAppLocalizer` 重复，易致混淆。

### 已验证为正常（无需改动）
- 所有 `.axaml` 用户文本均走 `{DynamicResource}`。
- `SetStatus` / `SetProgressStatus` 一律使用 `Status.*` 键。
- `SettingsToolViewModel`、`AvaloniaSettingsCloseConfirmationService` 全本地化。
- 无 `Console/Debug/Trace.WriteLine`。

## 三、修复任务（subagent 顺序执行，避免并发改 resx 冲突）

> ⚠️ 所有任务共享 `Strings.*.resx`，**必须串行**；每个 subagent 完成后由主控做构建/测试验证再启动下一个。

### Task 1 — 诊断信息本地化（P0）⭐ 核心
**目标**：状态栏与日志面板的诊断消息随当前语言切换。

**改动**：
1. `ChapterDiagnostic` 增加 `IReadOnlyDictionary<string, object?>? Arguments = null`（可选，向后兼容）。
2. 三个 resx 新增全部 `Diagnostic.{Code}` 键（含 `{占位符}`，见附录 A 的带参清单）。
3. `LocalizeDiagnostic` 改为 `Localizer.Format(diagnosticKey, diagnostic.Arguments)`。
4. `LogDiagnostics` 中 `{message}` 改用 `LocalizeDiagnostic(diagnostic)`，`{code}` 保留原始码（技术标识）。
5. 对带插值的诊断生产点（`InvalidChapterIndex`、`PartialParse(line)`、`OrderShiftNormalized` 等）改为传 `Arguments`，`Message` 保留英文作技术回退。
6. `LocalizationTests` 增加 `Diagnostic.*` 三语键/占位符一致性校验。

**验收**：`dotnet test ChapterTool.Avalonia.slnx --no-restore` 通过；切换语言后诊断消息随之变化。

### Task 2 — 日志 operation/action 参数本地化（P1）
**目标**：消除日志行中内嵌的英文片段。

**改动**：
1. 新增 `Operation.*` 键（`Operation.Load/Save/CreateZones/AppendLoad/AppendEdit/OutputProjection/AppendLoadEdit` 等）三语。
2. 新增 `Log.EditChapters.*` 子键或 `Action.*` 键（`DeleteRows/InsertRow/ShiftFramesForward/EditCell/CombineSegments/EditChapters`）三语，带 `{indexes}`/`{index}`/`{frames}`/`{kind}`/`{row}`/`{value}`/`{count}`/`{sourceType}` 占位符。
3. `ApplyEdit` 签名改为接收 `LocalizedMessage` 或 `(string key, args)` 而非原始英文字符串；更新全部调用点。
4. `LogDiagnostics` 的 `operation` 参数改为本地化键路径；更新全部调用点。

**验收**：构建通过；日志面板在中文/日文下不再出现 "Delete rows"、"Load" 等英文片段。

### Task 3 — ExpressionService 错误消息本地化（P2）
**目标**：表达式解析错误随语言切换。

**改动**：
1. 为 `ExpressionService` 中每种解析失败新增 `Expression.*` 键三语（或扩展 `Diagnostic.InvalidExpression.*`）。
2. 改造抛错路径：以「错误码 + 参数」形式携带，最终在 `InvalidExpression` 诊断处本地化；或令 `ExpressionService` 接收本地化查表回调。
3. 同步附录 A 的 `InvalidExpression` 处理。

**验收**：构造错误表达式，中文/日文下提示为对应语言。

### Task 4 — 外部工具/依赖缺失消息本地化（P3）
**目标**：`MissingDependency` 诊断消息本地化。

**改动**：
1. `ExternalToolLocator` / `FileSystemNativeDependencyService` 返回结构化位置（`ToolId`/`DependencyId`）而非预格式化英文 `Message`。
2. 由 importer 在生成 `MissingDependency` 诊断时携带 `Arguments={toolId/dependencyId}`，复用 Task 1 的 `Diagnostic.MissingDependency` 键。
3. 校验 `BdmvChapterImporter`、`MatroskaChapterImporter`、`FfprobeMediaChapterReader` 的透传链路。

**验收**：缺依赖场景下提示为当前语言。

### Task 5 — 清理废弃本地化抽象（P4）
**目标**：消除 `ILocalizationService`/`LocalizationService` 死代码与潜在误用。

**改动**：
1. 确认仅 `tests/ChapterTool.Infrastructure.Tests/PlatformServiceTests.cs` 使用。
2. 删除接口与实现 + 测试引用，或统一并入 `IAppLocalizer`（视依赖最小化原则择一）。

**验收**：构建+全量测试通过，无悬空引用。

## 四、执行顺序与验证
1. Task 1（P0）→ `dotnet build` + `dotnet test ChapterTool.Avalonia.slnx --no-restore`
2. Task 2（P1）→ 同上
3. Task 3（P2）→ 同上
4. Task 4（P3）→ 同上
5. Task 5（P4）→ 同上
6. 终验：`openspec validate --all`（如涉及 spec）+ 全量测试

## 附录 A — 诊断码完整清单（待补 Diagnostic.* 键）

### 静态消息（无占位符）
```
InvalidFrameRate, InvalidFrameText, NoRowsSelected, NoSegments,
UnsupportedCombineSource, UnsupportedAppendSource, UnsupportedExportFormat,
NoChapters, NoChaptersFound, EmptyCueFile, MalformedCueSyntax,
InvalidContainerHeader, FlacEmbeddedCueNotFound, EmbeddedCueNotFound,
InvalidMpls, InvalidStructure, InvalidIfo, InvalidXml, InvalidEntryElement,
InvalidChapterPair, EmptyXml, XmlInvalidRoot, XmlNoChapters, XplNoChapters,
XplParseFailed, EmptyChapters, OgmInvalidFirstLine, PremiereMarkerListInvalid,
WebVttInvalidHeader, WebVttMalformedCue, InvalidTimeText, InvalidTimecodeText,
InvalidChapterText, InvalidExpressionTime, UnsupportedPlatform,
DependencyExecutionCancelled, DependencyExecutionFailed, DependencyExecutionTimedOut,
DependencyOutputUnrecognized, DependencyOutputMissing, DependencyOutputEmpty,
MatroskaCannotStart, MatroskaProcessCancelled, MatroskaProcessFailed,
MatroskaProcessTimedOut, FfprobeEmptyOutput, FfprobeParseFailed,
FfprobeProcessCancelled, FfprobeProcessFailed, FfprobeProcessTimedOut,
Mp4FileInaccessible, Mp4FileNotFound, Mp4InvalidPath, Mp4MalformedMetadata,
Mp4ReadFailed, Mp4UnsupportedMetadata, Stdout, MissingDependency
```

### 带占位符消息
| Code | 占位符 | 示例 |
|------|--------|------|
| `InvalidChapterIndex` | `{index}` | Chapter index {index} is out of range. |
| `PartialParse` | `{line}` | Parsing stopped at line: {line} |
| `OrderShiftNormalized` | （待核） | 订单位移已归一化 |
| `MissingDependency` | `{toolId}` / `{dependencyId}` | External tool '{toolId}' was not found. |
| `Stdout` | `{output}` | （原样透传命令输出） |
