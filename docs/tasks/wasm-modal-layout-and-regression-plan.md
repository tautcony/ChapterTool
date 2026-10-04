# 网页弹窗布局调整与回归测试方案

实施 change：[`fix-wasm-modal-layout`](../../openspec/changes/fix-wasm-modal-layout/proposal.md)。本文保留变更前的分析基线。当前实现和验证结果见[弹窗布局验收记录](../testing/wasm-modal-layout-acceptance.md)。

## 1. 结论和范围

历史导航和表达式编辑必须改为独立弹窗。表达式预览、错误、差异列表及 Apply/Cancel 必须放在表达式弹窗内。本次导出的高级选项应放入单独的选项弹窗。主界面保留加载、保存、撤销、重做、片段选择、章节表格、紧凑选项和状态栏。

所有视口使用相同的弹窗归属。不得在桌面显示内嵌面板、在手机显示另一套面板。弹窗打开后，主界面的网格行和章节区尺寸必须保持稳定。

必须复用现有 Playwright 套件。布局行为断言必须进入常规 PR 测试。截图比较必须覆盖工具打开、错误和长内容状态。

初次分析只运行了临时布局采集脚本。采集成功不表示布局通过验收。后续实现必须使用正式浏览器断言和视觉比较验证。

分析日期：2026-10-04。源码基线：`90bb423`。运行环境：Windows、本地当前源码、Blazor 开发服务器、Playwright 1.63.0、Chromium 和 WebKit。公开部署站点和真实 iPhone Safari 尚未验证。

## 2. 截图现象和证据边界

用户截图显示以下问题：

- Edit history 插在章节表格和导出选项之间。
- Expression preview、错误列表和 Apply/Cancel 出现在底部选项区。
- 表达式预设和输入框变成窄条。
- 在手机上，辅助功能占用了大量章节编辑空间。

当前源码确认了前两项的结构来源。本地浏览器还确认了表格挤压和短视口遮挡。

当前 390px 宽的本地测试中，表达式输入框宽约 273px，没有复现截图中的窄条。因此，不能仅凭源码将窄条归因于某个 Safari 缺陷。后续必须核对部署提交、实际 CSS 响应、缓存、CSS 视口尺寸、文字缩放和真实设备行为。

不得将截图物理像素直接当成 CSS 视口尺寸。不得把桌面 WebKit 或设备模拟测试当成真实 iPhone 验收。[Playwright 设备模拟](https://playwright.dev/docs/emulation)。

## 3. 当前结构的根因

| 问题 | 当前证据 | 影响 |
| --- | --- | --- |
| 主布局行数随工具状态变化 | [`Home.razor`](../../src/ChapterTool.Wasm/Pages/Home.razor) 在 `.main` 内插入 `.history-panel`；[`app.css`](../../src/ChapterTool.Wasm/wwwroot/css/app.css) 的 `.main` 只有三条显式行：`auto minmax(0, 1fr) auto` | 历史打开后出现第四条隐式行。导出区移动到隐式行。章节区被压缩 |
| 子项最小高度超过分配空间 | `.grid-zone` 和 `.grid-wrap` 都有最小高度；手机规则仅降低 `.grid-wrap` 的最小高度 | 在短视口中，网格行可以接近 0，但表格仍保留最小高度。表格越过行边界并遮挡后续内容 |
| 表达式任务嵌入选项单元格 | `.option-expression` 同时承载输入、异步计算状态、诊断、差异列表和操作按钮 | 一项输入变成完整任务面板。错误出现后，底部区域继续增长 |
| 表单列与内容面板耦合 | `.option-expression` 使用标签/控件双列；`.expression-preview` 固定在 `grid-column: 2 / -1` | 诊断受标签宽度和窄屏规则影响。增加 CSS 特例不能解决任务归属问题 |
| 弹窗已有样式，行为不完整 | 设置、日志、导出预览已有 fixed 遮罩和 `role="dialog"`；多个布尔值分别控制它们 | 现有视觉样式可复用，但角色属性本身不负责焦点约束或背景交互隔离 |
| Escape 分发顺序存在缺口 | `OnKeyDownAsync` 先检查可编辑目标并返回，再检查模态窗口 | 输入框聚焦时，现有页面级 Escape 关闭逻辑不会执行。新弹窗必须统一处理取消和关闭 |

### 3.1 本地测量结果

复现步骤：加载 OGM sample，打开 Edit history，输入 `t + 1`，等待超出已知时长的错误。随后测量区域尺寸，并检查历史关闭按钮是否能接收点击。

| 环境 | 普通状态的章节网格行高度 | 历史和错误同时出现后的行高度 | 结果 |
| --- | ---: | ---: | --- |
| Chromium，390×844 | 431.89px | 232.16px | 章节表格实际高度从 413.00px 降至 213.27px |
| WebKit，390×844 | 421.08px | 221.34px | 同样压缩主章节区 |
| Chromium，390×640 | 227.89px | 28.16px | 表格实际底边约 358.02px，历史顶边约 211.86px。历史被覆盖，关闭按钮无法接收点击 |
| WebKit，390×640 | 217.08px | 17.34px | 表格实际底边约 367.42px，历史顶边约 210.45px。关闭按钮同样被覆盖 |
| Chromium，844×390 | 187.88px | 3.63px | 横屏关闭按钮被覆盖 |
| WebKit，844×390 | 179.88px | 0px | 网格行归零，表格仍有最小高度。横屏关闭按钮被覆盖 |

390×640 表示用于验证布局的短视口。它不是对用户截图视口尺寸的反推。两个浏览器的这些状态均没有触发 `pageerror`。页面没有 JavaScript 异常，仍然可以出现无法操作的布局。

点击检查日志明确记录：关闭按钮可见、已启用且稳定，但 `.grid-zone` 内的章节输入框或单元格拦截了指针事件。这是实际遮挡证据，不能用 `toBeVisible()` 的成功结果替代。

证据保存在 `artifacts/wasm-layout-analysis/`：

- [默认尺寸截图](../../artifacts/wasm-layout-analysis/chromium-default-history-expression-error.png)
- [宽尺寸截图](../../artifacts/wasm-layout-analysis/chromium-wide-history-expression-error.png)
- [390×844 截图](../../artifacts/wasm-layout-analysis/chromium-narrow-history-expression-error.png)
- [390×640 遮挡截图](../../artifacts/wasm-layout-analysis/chromium-narrow-short-history-expression-error.png)
- [WebKit 横屏截图](../../artifacts/wasm-layout-analysis/webkit-landscape-history-expression-error.png)
- [Chromium 短视口测量](../../artifacts/wasm-layout-analysis/chromium-narrow-short-measurements.json)
- [WebKit 短视口测量](../../artifacts/wasm-layout-analysis/webkit-narrow-short-measurements.json)

这些本地产物不会进入版本控制。重新分析时可以用下述复现步骤重新采集。测量使用的是当前源码开发服务器；修复验收还必须使用准备后的 Release 静态产物。

## 4. 为什么现有测试没有检出问题

当前仓库已有真实浏览器测试。缺口是状态覆盖和门禁范围，而不是缺少测试工具。

| 当前测试或配置 | 已验证内容 | 本次遗漏 |
| --- | --- | --- |
| [`unified-editing.spec.ts`](../../tests/ChapterTool.Wasm.E2E/specs/unified-editing.spec.ts) B19 | 历史分支导航和 Redo | 默认宽视口；没有区域尺寸、遮挡或弹窗归属断言 |
| 同文件 B20–B23 | 表达式只读预览、提交、取消、错误和最新草稿 | 没有短视口及历史同时打开的布局状态。内嵌面板本身满足这些用例 |
| [`layout.spec.ts`](../../tests/ChapterTool.Wasm.E2E/specs/layout.spec.ts) B18 | 普通页面、设置、导出预览和下载；默认、宽、390×844 截图 | 没有历史弹窗、表达式有效/无效预览、长差异、短视口或横屏 |
| [`playwright.config.ts`](../../tests/ChapterTool.Wasm.E2E/playwright.config.ts) | 常规行为测试 | `testIgnore: '**/layout.spec.ts'` 排除了现有布局用例 |
| [`wasm-browser-e2e.yml`](../../.github/workflows/wasm-browser-e2e.yml) | PR 的 Chromium 行为测试；定时和手动的三引擎与视觉测试 | fixed-Linux 视觉步骤仅在定时和手动运行。PR 不执行它 |
| [`WasmWorkspaceTests.cs`](../../tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs) | 工作区、候选、历史与导出契约 | 不渲染 DOM，不能检出 CSS 覆盖 |

`toBeVisible()` 不能证明控件有足够宽度、未被遮挡或在可见视口内。无参数的 `toBeInViewport()` 也不能保证控件完全可见。测试必须结合区域关系、完整可见性和真实点击。[可操作性检查](https://playwright.dev/docs/actionability)、[视口断言](https://playwright.dev/docs/api/class-locatorassertions#locator-assertions-to-be-in-viewport)。

`fullPage: true` 的截图可以记录整个文档。它不能证明操作按钮位于用户当前可见区域。仅检查外框横向溢出，也不能发现纵向覆盖。

## 5. 功能归属与主界面调整

| 功能 | 调整后的归属 | 关键行为 |
| --- | --- | --- |
| Load、Save、Undo、Redo | 顶部工具栏 | 保持主操作直接可用。窄屏可以使用有可访问名称的图标按钮 |
| Clip、当前 FPS、Round frames | 顶部紧凑区域 | 保留当前选择与读数。变换操作通过明确入口执行 |
| 章节编辑 | 中央表格 | 作为主要空间占用者；表格内部滚动 |
| 格式、章节名称模式 | 底部紧凑选项 | 保留常用快速选择 |
| Order +、XML lang、模板选择、本次编码/BOM | “导出选项”弹窗 | 编辑本次导出参数；Apply 后生效，Cancel 恢复打开前的值 |
| 表达式预设、自定义脚本、计算状态、错误、差异、Apply/Cancel | “时间表达式”弹窗 | 在同一个弹窗完成草稿与候选确认。主界面只保留入口及一行简短摘要 |
| Edit history | “编辑历史”弹窗 | 保留分支列表、当前节点、导航与内部滚动 |
| 导出文本预览 | 现有“导出预览”弹窗 | 读取已提交的文档；继续提供复制、下载和关闭 |
| 设置、日志、平移、关联媒体 | 现有弹窗 | 接入相同的弹窗生命周期和尺寸规则 |
| 成功状态与一般进度 | 底部状态栏 | 保持短文本；不在主布局新增详情面板 |

“本次导出选项”与“设置中的默认输出值”必须在入口和标签中区分。Cancel 不能写入持久化设置。Apply 必须保留当前底部选项的应用和持久化契约。若要进一步改变本次参数与默认值的保存规则，必须作为独立行为变更处理。

主布局的目标结构如下：

```text
主界面
┌────────────────────────────────────┐
│ 加载 保存 撤销 重做 更多            │
│ 片段选择 / 帧率 / 帧显示            │
├────────────────────────────────────┤
│                                    │
│          章节表格                  │
│                                    │
├────────────────────────────────────┤
│ 格式  名称模式  导出选项… 表达式…   │
├────────────────────────────────────┤
│ 状态 / 进度                    日志 │
└────────────────────────────────────┘

弹窗层，位于主布局之外
┌────────────────────────────────────┐
│ 标题                            ×  │
├────────────────────────────────────┤
│ 输入、诊断、差异或历史列表          │
│ 内容过长时仅此区域滚动              │
├────────────────────────────────────┤
│                      Cancel  Apply │
└────────────────────────────────────┘
```

手机上的“更多”入口可以收纳历史等辅助操作。不得隐藏加载、保存或编辑能力。触控按钮以至少 44×44 CSS px 作为本方案设计目标。标签必须允许合理换行。表达式输入必须占用独立完整行，不能挤在长标签剩余的列内。

## 6. 弹窗实现与交互契约

### 6.1 最小实现结构

建议新增 `src/ChapterTool.Wasm/Components/WasmDialog.razor`，负责公共标题、内容、页脚、关闭和焦点生命周期。建议分别提取 `HistoryDialog.razor`、`ExpressionDialog.razor` 和 `ExportOptionsDialog.razor`，避免继续扩大 `Home.razor`。

`Home.razor` 保留工具入口和页面协调。使用一个页面内 `ActiveDialog` 状态表示当前弹窗。不得继续为新增弹窗叠加独立布尔值。无需建立全局窗口管理服务。

公共弹窗优先使用原生 `<dialog>` 和 `showModal()`。它提供顶层渲染和背景交互隔离。不能用 `<dialog open>` 代替模态打开。视觉样式可沿用当前标题、内容和页脚结构。[HTML dialog 标准](https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element)。

若保留自定义遮罩实现，必须补齐与原生弹窗相同的焦点约束、背景隔离和关闭行为。仅添加 `aria-modal="true"` 不满足这些要求。

### 6.2 通用契约

- 同一时刻只打开一个弹窗。主菜单、行菜单和拖放提示在弹窗打开时关闭或暂停。
- 打开后将焦点移入弹窗。Tab/Shift+Tab 必须留在弹窗内。
- Escape 从输入框内也能执行该弹窗的取消或关闭逻辑。
- 关闭后将焦点返回触发按钮。触发按钮失效时，返回明确的页面入口。
- 遮罩后的 Save、Undo、表格编辑、拖入替换和全局应用快捷键不能修改文档。
- 只读弹窗允许点遮罩关闭。表达式、设置和导出选项不因误触遮罩丢失草稿。
- 表达式的 Cancel、Escape 和关闭按钮采用相同的候选取消路径。
- 关闭和组件销毁必须取消待执行预览、移除事件监听并释放相关引用。

焦点与关闭要求依据 [WAI-ARIA 模态弹窗模式](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/)。必须通过浏览器行为验收，不能只检查角色属性。

### 6.3 表达式弹窗

必须保留工作区已有的 Prepare/Apply/Cancel 候选机制。输入不能直接提交章节数据。有效候选显示变更前后值。非法候选显示完整诊断并禁用 Apply。无变化候选不创建无意义历史记录。

打开时建立草稿。Cancel 必须恢复打开前的表达式选择和草稿基准，并丢弃候选。Apply 成功后产生一个事务，关闭弹窗，返回章节表格。Undo 必须恢复原内容。预览和保存只读取已提交内容。

表达式防抖、取消和修订号由表达式组件负责。快速输入和关闭后到达的异步结果不得重新打开弹窗。候选必须继续验证工作区版本。失效候选不得强行提交。

导出预览不能叠在表达式弹窗之上。现有 B20 应改成“取消草稿后导出仍为原内容，再重新打开并 Apply 后导出新内容”。用例仍验证原有文档契约，但不能继续点击模态背景上的按钮。

### 6.4 历史弹窗

保留当前点击节点即导航的行为。点击后更新当前节点指示，并保留列表滚动位置。关闭不撤销已完成的历史导航。因此，历史页脚使用 Close，不使用含糊的 Cancel。

Undo/Redo 可以在历史弹窗中提供同语义操作。主界面按钮在模态期间不可操作。现有 B19 必须限定查询范围，或关闭弹窗后再使用主界面 Redo。

保留 `Virtualize`，为节点使用稳定 ID。长分支列表只在内容区滚动。标题、当前状态和关闭按钮不能滚出视口。

### 6.5 响应式尺寸和滚动

主界面必须给工具栏、章节区、底部选项和状态栏明确的布局归属。禁止以新增隐式网格行承载任务面板。

必须同时调整 `.grid-zone` 和 `.grid-wrap` 的最小尺寸规则。不能只修改其中一个。空间充足时由章节区伸展。空间不足时使用明确的滚动容器，不能让子项越过分配区域盖住下一行。

弹窗使用 `header / minmax(0, 1fr) body / footer` 或等效 Flex 结构。内容区设置 `min-height: 0` 和内部滚动。标题与页脚不收缩。诊断列表不与 Apply/Cancel 共用一个高度有限的滚动容器。

尺寸上限必须考虑动态视口和安全区域。CSS 可使用 `100dvh`，并保留必要回退。[动态视口单位](https://www.w3.org/TR/css-values-4/#viewport-relative-lengths)。不能假定 `100dvh` 自动解决所有软键盘覆盖情况。真实 iPhone 键盘测试若发现遮挡，再用 `visualViewport` 提供可见高度约束。

长脚本、长名称和差异文本必须有内部换行或滚动策略。窄屏标签与输入采用单列。普通页面允许表格内部横向滚动，禁止整个页面意外横向溢出。

## 7. 必须补齐的测试

### 7.1 分离布局契约与像素基线

新增 `modal-layout.spec.ts`，放入默认 `test:e2e` 范围。它验证弹窗行为、区域关系和可操作性，不调用 `toHaveScreenshot()`。因此 Windows 本地和 Linux PR 都能运行，不依赖操作系统像素基线。

扩展现有 `layout.spec.ts` 或新增专用视觉文件，并修改视觉配置的匹配规则。截图用例在固定 Linux 环境运行。它们必须覆盖打开状态和诊断状态，不只截图普通页面。

不得为了让布局契约进入 PR，直接把 Linux 像素基线加入所有 Windows 行为测试。也不得继续把行为布局断言全部排除在默认配置之外。

### 7.2 回归用例

| ID | 场景 | 必须断言 |
| --- | --- | --- |
| L01 | 打开/关闭历史弹窗 | 存在唯一命名 dialog；章节区、选项区、状态栏的几何位置不改变；关闭可真实点击；焦点返回入口 |
| L02 | 长历史和分支导航 | 列表内部滚动；当前节点标记正确；头部和 Close 完整可见；关闭后表格与导出反映所选节点 |
| L03 | 表达式 pending → 有效差异 | 输入框有可用宽度；结果只在弹窗内；Apply 完整可见、可点击；提交一次；Undo 和导出正确 |
| L04 | `t + 1` 时长错误、Lua 错误和长诊断 | 错误完整呈现；Apply 禁用；Cancel 可点击；章节、历史和下载内容不变 |
| L05 | 无变化、快速输入、计算期间关闭 | 只显示最新草稿结果；关闭后不复活候选；不产生额外事务 |
| L06 | 弹窗输入聚焦，Tab/Shift+Tab、Escape | 焦点不离开弹窗；Escape 正确取消；后台 Save/Undo/编辑不会执行 |
| L07 | 历史、表达式、设置、导出预览依次开关 | 始终只有一个模态面；不残留遮罩或焦点约束；已关闭任务不再占据主布局 |
| L08 | 导出选项 Apply/Cancel 与默认设置 | 本次参数正确生效；取消恢复原值且不写设置；Apply 的持久化结果符合当前契约 |
| L09 | 390×640、844×390 和尺寸动态切换 | 各弹窗的关闭与提交按钮在可见区域；内部滚动可以到达内容；表格不遮挡其他区域 |
| L10 | en-US、zh-CN、ja-JP，长字段和明暗主题 | 标签、输入、诊断与按钮不重叠；主流程可完成；对应视觉状态有基线 |
| L11 | 真机 iPhone Safari：工具栏变化、键盘、文字缩放、旋转 | 输入与页脚可达；关闭不会误触背景；无截图中的控件窄条；主页面状态和滚动恢复 |

L01、L03、L04、L06、L07、L09 是本次 P0。必须在 Chromium 和 WebKit 的目标视口执行。首次完整验收再执行 Firefox。L11 属于人工补充验收，必须记录设备、系统、浏览器、实际 CSS 视口、部署 SHA 和截图。

最低视口集：1280×800、1920×1080、390×844、390×640、844×390。补充 320px 宽及 519/520/521、759/760/761px 断点邻域检查。不得只用“手机宽度 + 大高度”代替短视口测试。

原来的“历史与错误同时内嵌”作为修复前反例保留截图。修复后的用例应验证单弹窗约束，并依次打开历史和表达式；不能要求新设计继续允许两个内嵌任务同时出现。

### 7.3 断言要求

- 在固定视口和相同滚动位置测量工具栏、章节区、选项区和状态栏。弹窗开关前后允许约 2 CSS px 的舍入差异。
- 验证相邻主区域边界不重叠。对滚动容器验证视口边界，不将滚动内容超出容器误判为溢出。
- 对页脚的 Close、Cancel 和有效 Apply 使用 `toBeInViewport({ ratio: 1 })`，并完成真实点击。必要时使用 `click({ trial: true })` 辅助定位遮挡。
- 输入框要有显式可读宽度目标。手机表达式输入独占完整行。不能仅断言宽度大于 0。
- 点击不得使用 `force: true`。不得调用工作区方法、直接触发 DOM handler 或修改 CSS 来绕过遮挡。
- 禁用 Apply 必须断言禁用状态，不对它要求点击成功。
- 视觉用例同时保存 viewport 截图和必要的完整内容截图。等待字体和实际预览状态，不使用固定延迟。

像素比较必须在相同浏览器、系统和字体环境执行。新增基线之前，必须先证明布局契约通过，再人工审阅图片。[Playwright 视觉比较](https://playwright.dev/docs/test-snapshots)。

## 8. CI 和实施顺序

1. 先加入 P0 回归断言，记录当前源码失败。尤其记录短视口遮挡和没有 dialog 的失败。不得先接受当前错误布局作为新基线。
2. 实现历史、表达式和公共弹窗生命周期。调整主网格最小尺寸与滚动。保留现有 Core 候选和历史契约。
3. 接入导出选项弹窗，并统一已有模态窗口。更新 B19–B23 的操作路径和查询范围。
4. 将 `modal-layout.spec.ts` 放入常规 Chromium PR 门禁。为相关 UI 变化增加 WebKit 的目标回归集。
5. 在 `.github/workflows/wasm-browser-e2e.yml` 增加 PR 的固定 Linux 视觉检查。使用该 workflow 的既有环境；不要把像素比较只留在定时运行。
6. 行为测试和视觉测试分别使用 results、report 和 JUnit 目录。当前视觉配置继承默认输出目录，实施时必须避免覆盖前一轮报告。
7. Pages 部署 smoke 至少加入一次打开/关闭辅助弹窗的短视口流程。测试必须继续使用准备后即将上传的 Release 产物。
8. 完成真机验收，更新代码地图、WASM README 和实际执行记录。

实施时的主要命令如下。`modal-layout.spec.ts` 尚未创建；对应命令必须在实施后运行。

```text
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish
npm --prefix tests/ChapterTool.Wasm.E2E run prepare:site
npm --prefix tests/ChapterTool.Wasm.E2E run typecheck
npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e -- modal-layout.spec.ts --project=chromium --project=webkit
npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e
```

`npm run test:visual` 必须在现有 fixed-Linux 环境运行。依赖或项目资产变化时先恢复依赖。修改共享翻译时，必须修改 AXAML 源并运行生成和检查命令，不得手工修改生成的 WASM JSON。

## 9. 本次分析验证与最终验收

本次临时采集覆盖两种引擎、五种视口和四类状态。普通、宽和 390×844 的 6 个采集场景完成。短视口与横屏的 4 个采集场景也完成，并记录了历史关闭按钮的点击检查失败。

首次短视口尝试正常关闭历史时发生超时。后续采集将该遮挡记录为诊断，再通过顶部历史开关继续采集。没有使用强制点击。最终采集脚本的通过结果仅表示证据已保存。

本次复现命令：

```text
node tests/ChapterTool.Wasm.E2E/node_modules/@playwright/test/cli.js test --config=artifacts/wasm-layout-analysis/playwright.probe.config.mjs
```

该脚本和配置均位于被忽略的 artifacts 目录。本次没有运行完整正式 E2E 套件，也没有生成修复后的截图基线。

调整完成后，必须同时满足以下条件：

- 历史和表达式任务不再插入主网格或底部选项单元格。
- 所有目标视口中，弹窗开关不压缩章节区。主区域不互相遮挡。
- 长内容仅在弹窗内容区滚动。关闭、取消和提交按钮始终可达。
- 键盘焦点、背景隔离、快捷键和候选取消契约通过真实浏览器测试。
- 章节编辑、历史分支、表达式单次提交和下载结果继续正确。
- 状态布局回归在 PR 必须执行。视觉基线不能只覆盖默认状态。
- 真机 Safari 的未验证项必须明确记录；不得用模拟器结果替代。

相关基础方案：[浏览器 E2E 测试方案](../testing/wasm-browser-e2e-plan.md)。本方案补充其布局状态、模态行为和 PR 门禁要求。
