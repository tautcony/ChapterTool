## Why

当前 Avalonia 可执行文件的命令行参数只支持临时拼接的 `--help` / `--version` 和一个启动文件路径，既不稳定也无法承载真正的批处理工作流。仓库已经有成熟的 Core/Infrastructure 导入导出能力，但缺少一个正式的 CLI 外壳把这些能力暴露给脚本、终端和 CI。

## What Changes

- 以 `DotMake.CommandLine` 替换 `Program.cs` 中手写的参数分支，建立可维护的根命令和子命令结构。
- 新增 `convert` 命令，用于读取章节源文件并导出为目标格式，支持写入文件或直接输出到 stdout。
- 新增 `inspect` 命令，用于在终端输出导入结果、可选 source/edition、章节概要和诊断信息。
- 新增 `formats` 命令，用于列出 CLI 支持的输入/输出格式与基础选项。
- 为 CLI 行为补充规范与自动化测试，明确本阶段不实现 expression 等高阶转换能力。

## Capabilities

### New Capabilities
- `command-line-conversion-workflows`: 定义 ChapterTool 的正式命令行工作流，包括格式发现、基础文件转换、终端输出和结构化退出行为。

### Modified Capabilities
- `tests-build-distribution-assets`: 为 CLI 命令解析、基础转换和控制台输出增加自动化测试与构建约束。

## Impact

- 影响 `src/ChapterTool.Avalonia` 的程序入口、命令定义和运行时组合。
- 复用 `src/ChapterTool.Core` 与 `src/ChapterTool.Infrastructure` 的导入导出、外部工具定位和诊断能力。
- 引入 `DotMake.CommandLine` NuGet 依赖。
- 需要为 `tests/ChapterTool.Avalonia.Tests` 增加 CLI 服务与命令层覆盖。
