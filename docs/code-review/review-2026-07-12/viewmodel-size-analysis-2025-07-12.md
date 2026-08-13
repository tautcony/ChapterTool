# ViewModel 代码量分析与进一步拆分方案

**Date:** 2025-07-12  
**Context:** `feat/avalonia-workflow-modernization` 分支已将编排逻辑提取为 4 个 Workflow 类型 + 4 个 Session 类型，但 MainWindowViewModel 仍余 ~1959 行。

---

## 一、代码量变化对比

| 位置 | 提取前（估算） | 提取后 |
|------|:---------:|:-----:|
| MainWindowViewModel（6 个 partial 文件） | **~3,129** | **1,959** |
| Workflows/ (4 个新文件) | 0 | 442 |
| Session/ (4 个新文件 + 1 个 Ports) | 0 | 728 |
| **总计** | **3,129** | **3,129** |

> 实际等价：**编排逻辑从 VM 移出，但 VM 自身数量未减少**，因为移出的同时 VM 必须持有新类型的引用并编写桥接代码。

---

## 二、当前 VM 代码分类

| 类别 | 行数 | 占比 | 可提取性 |
|------|:---:|:---:|:---:|
| **A. 绑定属性（proxy 到 workspace）** | ~350 | 18% | ❌ Avalonia 要求 VM 暴露 `INotifyPropertyChanged` 属性 |
| **B. UiCommand 声明 + 初始化** | ~110 | 6% | ⚠️ 命令必须暴露在 VM 上，初始化可移出为 partial |
| **C. UI 显示选项同步**（ComboIndexFor、Refresh*DisplayOptions、SyncClip*） | ~210 | 11% | ✅ 可提取到 `DisplayOptionCoordinator` |
| **D. Port 接口实现**（IExpressionSessionPort 等 5 个接口） | ~100 | 5% | ✅ 可提取到独立 adapter 对象 |
| **E. 设置加载/应用**（LoadSettingsAsync、ApplyPreferences） | ~80 | 4% | ✅ 可提取到 `SettingsCoordinator` |
| **F. 跨切面 UI 协调**（ApplyClipSessionUi、ApplyEdit、NotifyStateChanged） | ~200 | 10% | ⚠️ 涉及 VM 状态变更，需保留在 VM 内 |
| **G. 编排委托 + 字段声明**（委托给 4 个 Workflow） | ~550 | 28% | ⚠️ LoadPathAsync/SaveAsync/CombineSegments 等已精简但仍有状态桥接 |
| **H. 辅助类型**（ChapterCellEdit、SelectorDisplayOption 等） | ~43 | 2% | ✅ 应移出到独立文件 |
| **I. 构造函数 + DI + 初始化** | ~65 | 3% | ❌ 必须保留 |
| **J. 其他**（BuildPreview、OpenRelatedMediaAsync、NormalizeFrameAccuracyTolerance） | ~250 | 13% | ⚠️ 部分可移出 |

---

## 三、当前提取的实际效果评估

### ✅ 真正移出的逻辑（4 个 Workflow）

| 类型 | 行数 | 移出了什么 |
|------|:---:|-----------|
| `LoadSaveWorkflow` | 134 | 异步加载/追加/保存的状态机、revision 令牌校验、反陈旧守卫 |
| `ClipEditingCoordinator` | 100 | SelectClip、ToggleCombine、Edit、Delete、InsertBefore、UpdateFrames 的纯编排逻辑 |
| `ProjectionFacade` | 64 | 投影计算、行物化、导出选项构建、投影缓存管理 |
| `StatusDiagnosticsPresenter` | 144 | 状态文本/进度格式化、诊断本地化、结构化日志构造 |

### ⚠️ 留在 VM 的桥接代码（已被精简但仍需存在）

以 `LoadPathAsync` 为例（ImportExport.cs 第 27-82 行），提取前整个加载流程内联在 VM 中，提取后：
- 状态机流转和 revision 检查 → `LoadSaveWorkflow.LoadAsync()` ✅ 已移出
- 进度更新回调 → `LoadSaveWorkflow` 接受 `Action<ChapterImportProgress>` ✅ 已移出
- 结果状态枚举解读（Stale / EmptyPath / Failed） → **仍在 VM**，55 行
- 状态栏 / 日志 / 进度 UI 更新 → **仍在 VM**

这是合理的：**状态机归 Workflow，UI 反应归 VM**。但问题是 LoadPathAsync / AppendMplsAsync 的状态枚举分支（各自 ~50 行）是高度重复的模式。

---

## 四、进一步拆分方案

### 方案 A：提取 Port 接口实现（收益 ~100 行）

当前 VM 直接实现 5 个接口：`IExpressionSessionPort`、`IPreferenceSink`、`IExportPreferencePort`、`INamingPreferencePort`、`IChapterEditPort`。

```csharp
// 新建: src/ChapterTool.Avalonia/Session/Ports/ExpressionSessionPortAdapter.cs
internal sealed class ExpressionSessionPortAdapter : IExpressionSessionPort
{
    private readonly ChapterWorkspace workspace;
    private readonly ProjectionFacade projectionFacade;
    private readonly IChapterExpressionEngine expressionEngine;
    private readonly Action refreshAction;
    // ... 实现所有 IExpressionSessionPort 成员
}

// VM 中变成:
private readonly ExpressionSessionPortAdapter expressionPort;
public IExpressionSessionPort ExpressionPort => expressionPort;
```

**收益：** VM 中移除 `LoadLuaExpressionScriptAsync`、`ApplyLuaExpressionSettings`、`ValidateLuaExpressionScript`、`ApplyExpressionState`、`FormatDiagnosticForDisplay`（Expression.cs 大部分）和 Settings.cs 中的 `ApplyPreferences` 逻辑。

### 方案 B：提取显示选项协调器（收益 ~210 行）

```csharp
// 新建: src/ChapterTool.Avalonia/Workflows/DisplayOptionCoordinator.cs
internal sealed class DisplayOptionCoordinator
{
    // ClipDisplayOptions 同步
    // FrameRateDisplayOptions 同步 + ComboIndexFor / FrameRateOptionForComboIndex
    // XmlLanguageDisplayOptions 同步
    // ChapterNameModeOptions 同步
}
```

**收益：** Editing.cs 中的 `OnClipOptionsChanged` / `SyncClipDisplayOptions` / `RebuildClipDisplayOptions` / `ToClipDisplayOption` / `ComboIndexFor` / `FrameRateOptionForComboIndex`；StatusLog.cs 中的 `RefreshFrameRateDisplayOptions` / `RefreshXmlLanguageDisplayOptions` / `RefreshChapterNameModeOptions`。

### 方案 C：移出辅助类型（收益 ~43 行）

`ChapterCellEdit`、`ChapterGridColumnIds`、`SelectorDisplayOption` 应从 VM 文件中移出到独立文件。

### 方案 D：统一 Load/Append 结果处理模式（收益 ~80 行）

`LoadPathAsync` 和 `AppendMplsAsync` 共享相同的状态→UI 处理模式。可提取一个私有方法 `ApplyWorkflowOutcome<T>(T outcome, ...)`。

### 汇总

| 方案 | 预估收益（行） | 复杂度 | 推荐优先级 |
|------|:-----:|:---:|:---:|
| C - 移出辅助类型 | 43 | 低 | 🔴 高 |
| B - 显示选项协调器 | 210 | 中 | 🟡 中 |
| A - Port 接口适配器 | 100 | 中 | 🟡 中 |
| D - 统一状态处理 | 80 | 低 | 🟢 低 |

> **实施全部方案后预估 VM 行数：~1,520 行**（从 1,959 减少约 22%）

---

## 五、关于「目的没达到」的诚实评估

用户的直觉是对的：**提取后 VM 仍有 ~1,959 行，单从代码行数看，目的确实没完全达到。**

但区分两个维度：

1. **架构目的 ✅ 已达成**：4 个 Workflow + 4 个 Session 类型使得：
   - `LoadSaveWorkflow` 可被单元测试而不需构造整个 VM
   - `ClipEditingCoordinator` 的编辑逻辑独立于 Avalonia 绑定
   - `StatusDiagnosticsPresenter` 可替换为不同实现（如静默模式）
   - 反陈旧（anti-stale）revision 逻辑集中在 `LoadSaveWorkflow` 一处

2. **行数目的 ❌ 未达成**：VM 仍然过大，因为：
   - 每个提取都产生了桥接代码（构造函数注入 + 委托调用）
   - 20+ 个绑定属性和 20+ 个 UiCommand 的声明无法消除（Avalonia MVVM 固有复杂度）
   - 5 个 Port 接口的实现仍在 VM 内部

**结论：当前提取是合理的中间状态，但不应视为终点。** 方案 A+B+C 应作为下一个 slice 执行，目标是将 VM 降至 ~1,500 行。

---

## 六、建议的下一步

1. **立即做**：方案 C（移出辅助类型），零风险
2. **下一个 slice**：方案 B（显示选项协调器）+ 方案 A（Port 适配器）
3. **长期考虑**：将 Port 接口从 VM 彻底解耦，工具窗口直接持有 adapter 引用而非通过 VM
