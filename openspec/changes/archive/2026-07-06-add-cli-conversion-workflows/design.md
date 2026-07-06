## Context

当前桌面程序和命令行入口复用同一个 `src/ChapterTool.Avalonia` 可执行文件，但入口逻辑只做了最简陋的参数判断。与此同时，`RuntimeChapterImporterRegistry`、`ChapterExportService` 和 `ChapterConversionService` 已经具备绝大多数基础转换能力，缺的是一个稳定的命令树、可测试的 CLI 应用服务，以及把多 source/edition 导入结果映射到终端语义的策略。

这个改动是横跨入口、组合根、Core 导入导出复用和测试覆盖的交叉变更，并且会引入新的外部依赖 `DotMake.CommandLine`，因此需要单独设计。

## Goals / Non-Goals

**Goals:**
- 提供正式的根命令与子命令结构，替代手写参数解析。
- 允许用户通过 CLI 完成基础章节文件转换，不启动 GUI。
- 支持把转换结果写入文件或输出到 stdout，便于 shell 管道和脚本调用。
- 允许用户检查输入文件的 source/edition 结构和诊断信息。
- 复用现有 Core/Infrastructure 能力，避免复制转换逻辑。

**Non-Goals:**
- 不实现 expression、自定义公式、复杂批量模板或交互式编辑。
- 不把 GUI 的全部保存选项逐一暴露给 CLI。
- 不新增独立控制台项目；本阶段继续复用 Avalonia 可执行文件，在命中 CLI 子命令时短路退出。

## Decisions

### 1. 在现有 Avalonia 可执行文件中嵌入正式 CLI 命令树

采用 `DotMake.CommandLine` 的 class-based 模型，在 `src/ChapterTool.Avalonia` 中定义根命令和 `convert` / `inspect` / `formats` 子命令。`Program.Main` 先判断是否命中了 CLI 命令，如果命中则运行 CLI 并返回退出码；否则保持现有 GUI 启动路径。

这样做的原因：
- 现有发布物已经是单一桌面可执行文件，额外拆 console 项目会增加发布和 spec 成本。
- CLI 只需要复用现有服务，不需要独立 UI 或宿主。
- `DotMake.CommandLine` 提供帮助、版本、子命令和退出码，能直接取代现有“太儿戏”的解析。

备选方案：
- 新建独立 `ChapterTool.Cli` 项目。优点是宿主更纯粹；缺点是发布与测试面扩大，本阶段收益不足。
- 直接用 `System.CommandLine` 手写。缺点是样板过多，与用户要求不符。

### 2. 把 CLI 业务收敛到可测试的应用服务，而不是把逻辑堆在命令类里

新增一个 CLI 运行时服务，负责：
- 解析输入路径并选择 importer。
- 在导入结果中选择 group 和 option。
- 构造基础 `ChapterExportOptions`。
- 决定输出到 stdout 还是文件。
- 渲染 inspect / diagnostics / formats 的文本输出。

命令类只负责参数绑定和调用服务。这样测试可以直接覆盖服务逻辑，而不必通过真实进程或 Avalonia 生命周期驱动。

备选方案：
- 在命令类中直接拼 importer/exporter 调用。缺点是难测、难复用、入口继续臃肿。

### 3. CLI 只暴露“基础转换”选项，并显式禁用高阶能力

`convert` 只支持基础导入导出和少量稳定选项：
- 输入文件
- 输出格式
- 输出路径或 stdout
- source/edition 选择
- XML language
- CUE source file name

内部会把 `ApplyExpression` 固定为 `false`，避免把尚未准备好的高级表达式能力暴露到 CLI。

备选方案：
- 把 GUI 的所有导出开关一次性搬到 CLI。缺点是参数面膨胀，且与“暂时不需要 expression 等高阶功能”的范围冲突。

### 4. 多 source/edition 选择采用显式索引或 option id

导入结果可能包含多个 `ChapterInfoGroup` 或 `ChapterSourceOption`。CLI 采用两层显式选择：
- `--group-index`
- `--option-id` 或 `--option-index`

默认行为是当且仅当结果可唯一确定时自动选择，否则返回非零退出码并输出可选项列表。这样能避免脚本环境里出现隐式猜测。

### 5. 退出码与诊断分级分离

CLI 服务统一返回：
- `0` 成功
- `1` 用户输入错误、选择歧义、导入/导出失败
- `2` 未处理异常

同时把 Core/Infrastructure 的 `ChapterDiagnostic` 原样降级渲染到控制台，保留外部工具缺失、解析失败等结构化信息。

## Risks / Trade-offs

- [同一可执行文件同时承担 GUI 和 CLI] → 通过“先识别 CLI，再决定是否进入 Avalonia 生命周期”隔离启动路径，避免无谓创建桌面宿主。
- [多个入口共享组合逻辑，后续容易漂移] → 抽出 CLI 专用应用服务与 importer/exporter 组合方法，避免在 `Program.cs` 里散落依赖拼装。
- [导入结果可能多组且含外部工具依赖] → 对歧义和依赖失败使用结构化错误与可选项列表，不做隐式 fallback 之外的猜测。
- [测试如果走真实进程会很脆弱] → 优先测试 CLI 服务、命令解析与帮助输出，避免依赖完整桌面生命周期。

## Migration Plan

1. 引入 `DotMake.CommandLine` 并新增 CLI 命令定义与运行时服务。
2. 调整 `Program.Main`：命中 CLI 子命令时执行 CLI，否则继续 GUI。
3. 为 CLI 服务与入口增加测试。
4. 运行 solution 测试，确认桌面路径未回归。

回滚方式：
- 若 CLI 集成产生桌面启动回归，可移除 `Program.Main` 的 CLI 分支并保留底层服务代码，恢复原有 GUI 启动路径。

## Open Questions

- 暂无必须阻塞实现的开放问题。本阶段默认不支持批量多文件转换；后续如有需要，再在同一命令树下增加新子命令。
