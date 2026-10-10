# 网页弹窗布局验收记录

日期：2026-10-04。变更：[`fix-wasm-modal-layout`](../../openspec/changes/fix-wasm-modal-layout/proposal.md)。变更前基线：`90bb423`。本记录验证本地工作树的 Release 发布产物。

本文保留首次验收证据。固定 Linux 像素基线与比较命令已移除。当前布局由 E2E 与 Avalonia Headless 行为断言验证。

## 实现结果

- 历史、表达式编辑及预览、高级导出选项使用独立弹窗。
- 主页面保留工具栏、章节表格、紧凑导出控件和状态栏。
- 所有工具共用原生 `dialog` 和 `showModal()`。页面同时只打开一个弹窗。
- 弹窗正文独立滚动。标题和操作按钮保持可达。
- Escape 在输入框中生效。关闭后焦点返回触发控件。WebKit 的指针点击触发目标单独记录。
- 弹窗打开时，背景快捷键和文件拖放不能修改文档。
- 表达式取消会停止待完成验证，丢弃候选结果，并恢复先前表达式和预设。
- 表达式应用保留一次提交、撤销、重做和导出语义。
- 高级选项暂存序号偏移、XML 语言、编码、BOM 和模板。应用使用现有投影和设置持久化路径。
- 新增中文、英文和日文标题。WASM JSON 由共享 AXAML 资源生成。

## 自动化覆盖

采用仓库现有的 Playwright Test + TypeScript。没有新增浏览器测试框架依赖。

| 用例 | 验证结果 |
| --- | --- |
| M01 | 390×640 历史弹窗打开、真实点击关闭、原生模态状态和焦点返回。此用例属于 Pages `@smoke`。 |
| M02 | 12 个视口的独立弹窗、主表格几何稳定、错误输入宽度、操作可达、44px 目标和外层水平溢出。 |
| M03 | 正反向 Tab、输入框 Escape、快速取消与重开、背景撤销和拖放隔离、取消后导出字节不变。 |
| M04 | 模板和高级值暂存、取消后设置及字节不变、应用后模板生效、编码/BOM 保存和刷新恢复。 |
| M05 | 80 个章节的长差异列表滚动。页脚始终可达。取消不修改章节数量。 |
| M06 | 设置、预览、日志和帧平移共用关闭行为。草稿不接受遮罩点击关闭。预设取消恢复。 |
| M07 | 虚拟化历史滚动、当前项、导航时滚动保持和关闭后的重做。 |
| M08 | 取消恢复已应用的预设。无变化候选不能再次应用或增加历史。 |
| B19–B23 | 兄弟历史分支、表达式应用、导出及撤销、无效表达式、后续编辑和快速输入。 |

视口为 1280×800、1920×1080、390×844、390×640、844×390，以及宽度 320、519、520、521、759、760、761，高度 640。

Firefox 的 DOMRect 可能将 44 CSS 像素表示为 43.999969。目标断言只允许 0.001 像素的舍入误差。

## 视觉证据

固定 Linux 环境使用 `node:22-slim@sha256:7af03b14a13c8cdd38e45058fd957bf00a72bbe17feac43b1c15a689c029c732` 和锁定的 Playwright 1.63.0。

共检查 32 张基线。五种主要尺寸分别覆盖闲置、历史、表达式有效结果、表达式错误、高级导出选项和设置。短屏和横屏另覆盖长表达式结果。

记录中的 32 张像素基线已移除。`layout-behavior.spec.ts` 保留布局行为断言。供人工复核的副本和六张汇总图在 `artifacts/wasm-modal-layout/screenshots/`。逐组复核确认标题、输入框、错误列表和页脚没有相互覆盖。长正文需要滚动。截图本身不替代 M01–M08 的行为断言。

代表性图片：

- [`390×640 历史`](../../artifacts/wasm-modal-layout/screenshots/wasm-narrow-short-history-chromium-linux.png)
- [`390×844 表达式错误`](../../artifacts/wasm-modal-layout/screenshots/wasm-narrow-expression-error-chromium-linux.png)
- [`844×390 长表达式结果`](../../artifacts/wasm-modal-layout/screenshots/wasm-landscape-expression-long-chromium-linux.png)

## 执行记录

| 检查 | 结果 |
| --- | --- |
| 变更前回归 | 旧源码的 M01 因缺少历史 dialog 失败。旧发布产物的 M02 因缺少表达式入口失败。 |
| WASM 单元测试 | 42/42 通过。 |
| Release 发布 | 成功。准备为 `/ChapterTool/` 子路径静态站点。 |
| TypeScript 类型检查 | 通过。 |
| 弹窗专项三引擎检查 | 24/24 通过。没有重试。 |
| 最终三引擎完整套件 | 93/93 通过，耗时 4.8 分钟。Chromium、Firefox、WebKit 各 31 项。禁止重试。 |
| Linux 视觉比较 | 2/2 通过。比较运行不使用 `--update-snapshots`。32 张基线已复核。 |
| 翻译生成检查 | 通过。 |
| OpenSpec 严格校验 | 通过。 |
| `git diff --check` | 通过。 |
| 文档链接及 CI YAML | 32 个本地链接和工作流 YAML 校验通过。 |

主验证命令：

```text
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish
uv run --project scripts scripts/axaml-to-json.py --check
```

在 `tests/ChapterTool.Wasm.E2E/` 执行：

```text
npm run prepare:site
npm run typecheck
npm run test:e2e
npm run test:e2e -- modal-layout.spec.ts
```

设置 `CI=1`、`E2E_NO_RETRY=1`、`E2E_RUN_NAME=acceptance` 后执行最终完整套件。报告为 `artifacts/wasm-e2e/report-acceptance/` 和 `junit-acceptance.xml`。专项报告使用后缀 `modal-verification`。Linux 视觉报告为 `artifacts/wasm-e2e/modal-visual-context/artifacts/wasm-e2e/report-visual/`。

## CI 与未验证项

PR 配置加入了 WebKit 弹窗和编辑回归，以及固定 Linux 视觉检查。Chromium 继续运行完整行为套件。每周和手动验收继续使用三种引擎。视觉、Chromium 和 WebKit 报告使用独立路径。

本次没有在 GitHub Actions 上运行工作流。真实 iPhone Safari 的地址栏、软键盘、文字缩放和已部署缓存仍需设备验证。本地 WebKit 和视口模拟不能替代该验证。原始截图中的极窄输入框也不能据此归因于特定 Safari 缺陷。

本次没有部署网页，也没有归档或改写已有的完成 change。
