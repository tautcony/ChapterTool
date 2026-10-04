# 网页版浏览器 E2E 测试补齐方案

## 1. 结论与交付范围

选择 **Playwright Test + TypeScript**。在 `tests/ChapterTool.Wasm.E2E/` 建立独立 npm 测试项目。测试必须启动真实浏览器，操作 Blazor WebAssembly 页面，并验证用户可见结果和实际下载文件。

默认测试目标是经过 GitHub Pages 子路径处理的 Release 静态产物。开发服务器模式用于本地排错。PR 首先以 Chromium 阻断回归。完整验收覆盖 Chromium、Firefox 和 WebKit。

本文初版记录 2026-10-03 的实施方案和当时未执行 E2E 的交付范围。截至 2026-10-04，仓库已加入 `tests/ChapterTool.Wasm.E2E/` 和浏览器 CI。本文的目录与代码示例保留原始设计；当前执行入口以测试项目 README 和实际配置为准。

初次核查发现套件缺少历史、表达式错误及短视口的布局门禁。`fix-wasm-modal-layout` 按[网页弹窗布局与回归测试方案](../tasks/wasm-modal-layout-and-regression-plan.md)补齐了常规布局断言和视觉状态。验证结果见[弹窗布局验收记录](wasm-modal-layout-acceptance.md)。

## 2. 仓库现状与缺口

以下结论来自当前仓库代码，核查日期为 2026-10-03。

| 现有入口 | 当前职责 | 浏览器 E2E 缺口 |
| --- | --- | --- |
| [`ChapterTool.Wasm.csproj`](../../src/ChapterTool.Wasm/ChapterTool.Wasm.csproj)、[`Program.cs`](../../src/ChapterTool.Wasm/Program.cs) | .NET 10 Blazor WebAssembly 宿主 | 没有真实浏览器启动与断言 |
| [`Home.razor`](../../src/ChapterTool.Wasm/Pages/Home.razor) | HTML 按钮、文件输入、章节表格、选项、状态栏和模态窗口 | Razor 事件、焦点和浏览器行为没有端到端覆盖 |
| [`download.js`](../../src/ChapterTool.Wasm/wwwroot/js/download.js) | Blob 下载、文本编码、剪贴板、localStorage、拖放和快捷键拦截 | JavaScript 与 .NET 互操作没有真实浏览器验证 |
| [`WasmWorkspaceTests.cs`](../../tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs) | 导入、编辑、历史、预览、导出和字节限制的 xUnit 测试 | 直接调用工作区，没有操作页面 |
| [`WasmBrowserShortcutGuardTests.cs`](../../tests/ChapterTool.Wasm.Tests/WasmBrowserShortcutGuardTests.cs) | 快捷键规则测试 | 没有覆盖 DOM 事件和输入框焦点 |
| `tests/ChapterTool.Avalonia.Headless.Tests/` | Avalonia UI 交互 | 不运行 Blazor 或浏览器 |
| [`packages/chaptertool/package.json`](../../packages/chaptertool/package.json) | Node.js 包构建与 Vitest 测试 | 不覆盖网页版图形界面 |
| [`.github/workflows/dotnet-ci.yml`](../../.github/workflows/dotnet-ci.yml) | .NET、Node.js 测试与打包；已有 Node.js 22.x | 没有浏览器 E2E 作业 |
| [`.github/workflows/github-pages.yml`](../../.github/workflows/github-pages.yml) | Release 发布、base href 改写、静态文件检查与部署 | 没有在部署前运行页面交互测试 |

网页版是 Blazor HTML 页面。它不是 Avalonia Browser 的 Canvas 渲染页面。测试可以直接使用 DOM、角色和标签定位。

现有静态文件检查可以发现缺失文件。它不能证明 WASM 启动成功、按钮可用、下载正确或设置在刷新后恢复。

## 3. 工具选择

| 候选 | 适用性 | 本方案决策 |
| --- | --- | --- |
| Playwright Test + TypeScript | 内置浏览器项目、隔离上下文、断言、服务启动、追踪和报告；可沿用现有 Node.js 工具链 | 采用 |
| Playwright .NET | 可用 C# 编写浏览器测试，并接入 .NET 测试运行器 | 保留为替代方案；本次不增加 .NET 浏览器测试项目与宿主生命周期管理 |
| Cypress | 适合交互式网页测试；多浏览器能力需要按其当前支持范围评估 | 本次不采用；Playwright 的项目配置更直接满足本方案的三引擎测试目标 |
| Selenium WebDriver | 适合已有 WebDriver/Grid 基础设施的团队 | 本仓库没有该基础设施，本次不引入 |
| bUnit、现有 xUnit、Vitest | 适合组件、工作区和包级测试 | 保留原有测试；不能替代真实浏览器验收 |

Playwright 支持通过 projects 配置 Chromium、Firefox 和 WebKit。它提供文件选择和下载事件 API。以上功能符合本仓库的主要浏览器边界。[浏览器项目](https://playwright.dev/docs/test-projects)、[文件上传](https://playwright.dev/docs/input)、[下载](https://playwright.dev/docs/downloads)。

候选能力依据官方资料核查：[Playwright .NET 运行器](https://playwright.dev/dotnet/docs/test-runners)、[Cypress 浏览器支持](https://docs.cypress.io/app/guides/cross-browser-testing)、[Selenium WebDriver](https://www.selenium.dev/documentation/webdriver/)。表中的采用结论是针对本仓库的工程判断。

实施时必须选择支持 Node.js 22 的稳定版本。必须固定 `@playwright/test`、TypeScript 和类型依赖版本，并提交 `package-lock.json`。CI 必须使用 `npm ci` 和锁定版本对应的浏览器二进制。版本升级时必须重新安装浏览器，并执行三引擎验收。[浏览器安装与版本关系](https://playwright.dev/docs/browsers)。

## 4. 目录与改动范围

```text
tests/ChapterTool.Wasm.E2E/                 # 新建；不加入 .NET solution
  package.json
  package-lock.json
  tsconfig.json
  playwright.config.ts                    # 默认测试 Release 静态产物
  playwright.dev.config.ts                # 本地 Blazor DevServer
  playwright.visual.config.ts             # 固定环境下的截图比较
  README.md                               # 安装、执行和故障定位
  scripts/
    prepare-site.mjs                      # 处理发布目录；不重新编译
    serve-published.mjs                   # 仅提供本地静态 HTTP 服务
  support/
    fixtures.ts                           # 自动诊断与页面就绪检查
    chapter-workspace.ts                  # 用户操作和语义定位
  fixtures/
    minimal-ogm.txt
    unicode-ogm.txt
    invalid.xml
    expected/                            # 人工核对的预期下载内容
  specs/
    smoke.spec.ts
    import-export.spec.ts
    editing-history.spec.ts
    settings-localization.spec.ts
    browser-boundaries.spec.ts
    disc-workflows.spec.ts
    layout.spec.ts
```

必须在 `Home.razor` 补充必要的可访问标签和少量稳定定位属性。必须在 `.github/workflows/dotnet-ci.yml` 接入独立 E2E 作业。必须在 `.github/workflows/github-pages.yml` 增加部署前验收。

实施后必须更新 `docs/code-map/testing.md` 和 `src/ChapterTool.Wasm/README.md`，说明测试入口和命令。测试产物放在已忽略的 `artifacts/wasm-e2e/`。视觉基线放在测试目录并提交版本控制。

不把 E2E 依赖加入 `packages/chaptertool`。该目录仍只负责 Node.js 包。业务解析和转换算法继续由现有 .NET 测试负责。

## 5. 被测站点与运行配置

### 5.1 默认：Release 静态产物

1. 在仓库根目录恢复 WASM 项目依赖。发布一次 Release 产物到 `artifacts/wasm-e2e/publish/`。
2. `prepare-site.mjs` 将其中的 `wwwroot/` 复制到 `artifacts/wasm-e2e/site/ChapterTool/`。
3. 准备脚本按 Pages 工作流生成 `.nojekyll`、`404.html` 和部署信息。它将两个 HTML 入口的 base href 改为 `/ChapterTool/`。
4. 静态服务器从 `artifacts/wasm-e2e/site/` 提供 HTTP 服务。浏览器访问 `http://127.0.0.1:5261/ChapterTool/`。
5. 同一份准备后的静态目录必须用于测试和后续 Pages artifact 上传。测试通过后不得再次发布或改写入口。

静态服务器必须提供正确的 JavaScript、JSON、CSS 和 `application/wasm` MIME 类型。资源缺失必须返回 404。服务器不得用 `index.html` 吞掉 `_framework` 或脚本的资源错误。先直接提供未压缩资源；若后续模拟预压缩资源，必须同时验证 `Content-Encoding`。

服务只能监听本机地址。路径解析必须限制在明确的静态根目录内。准备脚本必须验证目标目录位于 `artifacts/wasm-e2e/`，然后才能清理旧产物。

Pages 作业可以继续使用其现有发布目录。它应直接服务准备后的目录，并挂载到 `/${repositoryName}/`。准备规则应收敛为同一个脚本，避免测试与部署各维护一套不同规则。

### 5.2 本地排错：开发服务器

开发配置使用下面的服务启动命令。相对路径以仓库根目录为基准。

```text
dotnet run --project src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-launch-profile --no-restore --urls http://127.0.0.1:5261
```

开发配置必须设置 `ASPNETCORE_ENVIRONMENT=Development`。它使用根路径 `/`，并保持与默认配置相同的用例和断言。`--no-launch-profile` 避免 launch profile 自动打开额外浏览器。

两个配置均由 Playwright `webServer` 管理服务启动与退出。默认禁止复用占用端口的服务，避免误测旧进程。[服务生命周期配置](https://playwright.dev/docs/test-webserver)。

### 5.3 默认配置示例

以下配置是待实施模板。`serve-published.mjs` 必须先完成，才能执行它。

```typescript
import { defineConfig } from '@playwright/test';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const suiteDir = dirname(fileURLToPath(import.meta.url));
const baseURL = 'http://127.0.0.1:5261/ChapterTool/';

export default defineConfig({
  testDir: './specs',
  testIgnore: '**/layout.spec.ts',
  outputDir: '../../artifacts/wasm-e2e/results',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  failOnFlakyTests: Boolean(process.env.CI),
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [
    ['list'],
    ['html', { outputFolder: '../../artifacts/wasm-e2e/report', open: 'never' }],
    ['junit', { outputFile: '../../artifacts/wasm-e2e/junit.xml' }],
  ],
  use: {
    baseURL,
    locale: 'en-US',
    viewport: { width: 1280, height: 800 },
    acceptDownloads: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  webServer: {
    command: 'node scripts/serve-published.mjs --root ../../artifacts/wasm-e2e/site --port 5261',
    cwd: suiteDir,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 30_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } },
  ],
});
```

90 秒是 WASM 冷启动用例的初始预算。实施后必须根据 CI 实测调整。不得用增加固定等待来修复失败。初期使用一个 worker，控制多个 WASM 实例的内存开销；这不是 Avalonia Headless 的进程隔离要求。[配置](https://playwright.dev/docs/test-configuration)、[不稳定测试门禁](https://playwright.dev/docs/api/class-testconfig#test-config-fail-on-flaky-tests)。

## 6. 定位、就绪与浏览器边界

### 6.1 最小可测性改动

| 页面对象 | 当前可用入口 | 计划补齐 |
| --- | --- | --- |
| 页面根 | `#chaptertool-shell` | `data-app-ready` 和 `aria-busy` |
| Load、Save、Undo、Redo | 按钮可访问名称 | 沿用 `getByRole`；在模态窗口内限定查询范围 |
| 导入与附加 | `#loadFileInput`、`#appendMplsInput` | 沿用；主流程通过按钮与 `filechooser` 验证 |
| 保存格式与语言 | `#saveFormat`、`#xmlLang`，已有 label | 优先 `getByLabel` |
| 章节表格 | 原生 table 和各列 | 表格补充可访问名称；时间、名称输入框补充“行号 + 字段”的可访问标签 |
| 设置与预览 | `role="dialog"`、`aria-labelledby` | 沿用 dialog 范围查询；为预览正文增加稳定定位属性 |
| 状态栏 | `.status-text` | 增加 `role="status"` 或等效稳定语义 |

必须先使用角色和标签。必要时使用 `data-testid`。不得依赖长 CSS 路径、屏幕坐标或跨窗口的模糊文本查询。[定位策略](https://playwright.dev/docs/locators)。

`data-app-ready="true"` 必须在首次 `OnAfterRenderAsync` 完成快捷键和拖放注册、设置读取及恢复后设置。它只表示初始化完成。烟测仍必须实际导入文件，以证明 JavaScript 互操作可用。

操作完成必须等待具体结果，例如行数、输入值、按钮状态、dialog 关闭或下载事件。页面编辑使用 `@onchange`。测试执行 `fill()` 后必须通过 Tab 或其他真实失焦动作提交编辑，再验证 Undo 和导出结果。不得只验证输入框刚填入的值。

### 6.2 状态隔离与诊断

每个测试使用 Playwright 默认的新 BrowserContext。设置恢复测试在同一 context 内刷新。标签页隔离测试在同一 context 内打开两个 page，验证章节会话互不影响；共享 localStorage 不应被误判为章节会话泄漏。[上下文隔离](https://playwright.dev/docs/browser-contexts)。

必须在导航前监听 `pageerror`、`console.error`、`requestfailed` 和同源关键资源的非成功响应。自动 fixture 必须在测试结束时附加日志，并断言没有意外错误。主动注入故障的测试必须逐项声明预期错误。不得全局忽略资源加载错误。[网络事件](https://playwright.dev/docs/network)。

不得调用 `window.chapterToolWasm.downloadText` 或直接调用工作区方法来替代用户操作。不得 mock WASM/Core 返回值。JavaScript 注入仅用于浏览器边界，例如构造拖放 `DataTransfer`、预置损坏设置或模拟存储异常。

文件选择和下载监听必须先于按钮点击。拖放必须将 `File` 放入真实 DOM `DataTransfer` 并发送事件，随后断言表格结果。取消替换与确认替换必须分别处理真实 `window.confirm`。不得对所有 dialog 自动确认。

跨浏览器测试不得假定剪贴板权限一致。主要验收通过预览和下载验证内容。剪贴板可在明确支持的项目中单独验证。WebKit 测试验证引擎行为，不等同于已验证所有 macOS/iOS Safari 环境。

## 7. 用例矩阵

P0 是第一阶段必须完成的门禁。P1 在第二阶段补齐。每个用例必须能单独运行。

| ID | 优先级 | 用户操作 | 必须断言的结果 |
| --- | --- | --- | --- |
| B01 | P0 | 在 `/ChapterTool/` 冷启动；点击 Load OGM sample | 就绪标记完成；空表时 Save 禁用；样例产生 3 行；Save 可用；无意外启动错误 |
| B02 | P0 | 点击 Load；选择 2 行 OGM UTF-8 文件 | 两行时间、名称与已知输入一致；来源名称正确；选择事件真正接入 .NET |
| B03 | P0 | 修改名称和时间；失焦；Undo；Redo | 表格恢复和重做正确；导出反映提交后的内容；历史按钮状态正确 |
| B04 | P0 | 打开 Preview；关闭后 Save | 预览正文与实际下载内容一致；下载名称和扩展名正确；下载未失败 |
| B05 | P0 | 导出 TXT、XML、QPFile、TimeCodes 的代表样例 | 每种格式的独立预期内容正确；XML 语言生效；重复保存不重复应用表达式 |
| B06 | P0 | 修改章节后取消替换；再确认替换 | 取消保留章节和历史；确认结束旧会话并显示新文档 |
| B07 | P0 | 导入损坏 XML；再导入正常文件 | 失败有可见诊断；原文档和历史保留；后续操作恢复可用 |
| B08 | P0 | 设置语言、主题、默认格式后 Save；刷新；修改后 Cancel | 保存值在刷新后恢复；取消草稿不改变已应用值；新增 context 保持默认设置 |
| B09 | P0 | 用 en-US、zh-CN、ja-JP 分别导入中文和日文章节并保存 | 按钮、表头和 dialog 显示对应语言；已应用语言下 `html.lang` 正确；UTF-8 下载可正确解码名称 |
| B10 | P1 | Ctrl/Shift 选中多行；右键删除；Undo | 仅删除目标行；撤销恢复内容；选择状态符合当前工作流 |
| B11 | P1 | 使用 Ctrl/Meta+Z、Ctrl+S、F5；在文本框内编辑 | 非编辑焦点触发应用操作；输入框内不触发工作区撤销；应用 Reload 不造成页面重新导航 |
| B12 | P1 | 选择固定 FPS；切换帧显示；应用合法和非法表达式 | 指定时间和帧数符合已知结果；合法表达式只提交一次；非法表达式不产生部分修改 |
| B13 | P1 | 拖入正常文件、空拖放和大于 64 MiB 的文件 | 正常导入；空输入和超限有诊断；原文档保留；控件可继续操作 |
| B14 | P1 | 在同一 context 打开两个标签页并分别编辑 | 两个页面的章节和历史独立；刷新清除章节会话但保留已保存设置 |
| B15 | P1 | 预置损坏/旧版设置；模拟 localStorage 拒绝访问 | 不阻止启动；损坏设置回退；旧键按当前迁移契约处理；仍可导入和下载 |
| B16 | P1 | 导入多片段 MPLS；切换、合并、恢复、Append；导入代表 IFO/XPL/CUE/FLAC/TAK | selector、章节数量和已知关键时间正确；附加仅接受 MPLS；不依赖桌面外部工具 |
| B17 | P1 | 设置 UTF-8/UTF-16LE/UTF-16BE、BOM 后下载 | 直接核对字节序、BOM 和中文/日文内容；不能只比较解码后的字符串 |
| B18 | P1 | 在默认、宽、窄视口编辑、打开设置、预览并保存 | 控件可点击且无遮挡；章节表允许内部滚动；页面外框无意外水平溢出；截图基线符合预期 |

B11 的快捷键验证必须观察下载、表格和导航事件。仅测试快捷键判断函数不足以完成验收。模态窗口 Escape 测试必须设置明确的非编辑焦点，再验证关闭和焦点行为。

必须将 B01 和包含导入、编辑、下载的核心流程标记为 `@smoke`。Pages 门禁使用 `--grep @smoke`。P0/P1 表示实施优先级，不代表自动过滤规则。

64 MiB 上限适用于 Load、Reload 和 MPLS Append。模板上传目前另有 2 MiB 上限。模板超限和字节临界值必须在后续边界测试中分别覆盖。大文件用例按需生成临时文件，不提交大二进制 fixture。跨浏览器 DOM 拖放不能证明操作系统级文件拖拽，后者可保留少量人工验收。

复杂光盘数据可复用 `tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/`。例如 `Mpls/00001_Hidan_no_Aria_AA.mpls` 已用于工作区多片段测试。不得复制整个大型 fixture 目录或依赖本地未跟踪数据。简单 OGM、错误 XML 和 Unicode 输入应使用小型专用 fixture。

预期导出内容必须是预先核对的固定结果。不得通过调用同一 Core 导出实现动态生成“期望值”。允许统一平台换行；编码测试必须保留原始字节。

## 8. 核心流程示例

下面示例展示 B02、B03 和下载验证。它依赖第 6 节计划新增的可访问标签和 fixture。`support/fixtures.ts` 必须先完成就绪检查和自动诊断。此处的测试代码尚未落地。

```typescript
import { expect, test } from '../support/fixtures';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

test('导入后编辑、撤销、重做并下载 @smoke', async ({ page }, testInfo) => {
  // './' 保留 baseURL 中的 /ChapterTool/；使用 '/' 会跳到站点根目录。
  await page.goto('./');
  await expect(page.locator('#chaptertool-shell'))
    .toHaveAttribute('data-app-ready', 'true');

  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  await chooser.setFiles(fileURLToPath(new URL('../fixtures/minimal-ogm.txt', import.meta.url)));

  const grid = page.getByRole('table', { name: 'Chapters', exact: true });
  await expect(grid.locator('tbody tr')).toHaveCount(2);
  const name = grid.getByRole('textbox', { name: 'Chapter 1 name', exact: true });
  await expect(name).toHaveValue('Opening');
  await name.fill('开场');
  await name.press('Tab');
  await expect(page.getByRole('button', { name: 'Undo', exact: true })).toBeEnabled();

  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(name).toHaveValue('Opening');
  await page.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(name).toHaveValue('开场');

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  const download = await downloadPromise;
  expect(await download.failure()).toBeNull();
  expect(download.suggestedFilename()).toMatch(/\.txt$/);
  const output = testInfo.outputPath('actual.txt');
  await download.saveAs(output);
  const actual = await readFile(output, 'utf8');
  const expected = await readFile(
    new URL('../fixtures/expected/edited-ogm.txt', import.meta.url), 'utf8');
  expect(actual.replace(/\r\n/g, '\n'))
    .toBe(expected.replace(/\r\n/g, '\n'));
});
```

此测试必须从默认英文的新 context 开始。多语言测试必须通过设置界面切换应用语言。Playwright `locale` 只设置浏览器环境，不能代替应用设置操作。

## 9. 本地执行与 CI 接入

### 9.1 待实施的执行入口

新增 npm scripts 必须包含 `prepare:site`、`typecheck`、`test:e2e`、`test:e2e:dev`、`test:e2e:headed`、`test:e2e:ui`、`test:visual` 和 `report`。开发命令使用开发配置。有界面命令使用 `--headed`。交互排错命令使用 `--ui`。报告命令使用 `playwright show-report`。脚本必须直接调用固定版本工具，不使用全局 Playwright。

默认配置排除 `layout.spec.ts`。视觉配置继承默认配置，清除 `testIgnore`，仅选择 `layout.spec.ts` 和 Chromium 项目。`test:visual` 使用视觉配置。这样，Windows 本地行为测试不依赖 Linux 视觉基线。

实施完成后，在仓库根目录依次执行：

```text
dotnet restore src/ChapterTool.Wasm/ChapterTool.Wasm.csproj
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish
npm --prefix tests/ChapterTool.Wasm.E2E ci
npm --prefix tests/ChapterTool.Wasm.E2E exec -- playwright install chromium
npm --prefix tests/ChapterTool.Wasm.E2E run prepare:site
npm --prefix tests/ChapterTool.Wasm.E2E run typecheck
npm --prefix tests/ChapterTool.Wasm.E2E run test:e2e -- --project=chromium
```

三引擎验收时，安装命令不指定单个引擎，并运行不带 `--project` 的 `test:e2e`。Windows 本地不要求安装 Linux 系统依赖。Linux CI 使用 `playwright install --with-deps` 安装对应引擎和系统依赖。[CI 安装](https://playwright.dev/docs/ci-intro)。

Playwright 转译 TypeScript 不代替类型检查。`typecheck` 必须执行 `tsc --noEmit`。修改共享本地化资源时，必须遵循仓库 AXAML 到 JSON 的生成与检查流程，不能手工修改生成的 WASM JSON。

### 9.2 自动运行矩阵

| 触发 | 测试目标 | 浏览器与用例 | 门禁 |
| --- | --- | --- | --- |
| PR / push：WASM、Core、Contracts、测试或构建配置变化 | 本次提交的 Release 静态产物，挂载 `/ChapterTool/` | Chromium 全部已实现行为用例 | 必须通过 |
| 定时运行或手动运行 | 同样的静态产物 | Chromium、Firefox、WebKit 完整行为集 | 记录兼容性回归；发布前必须通过 |
| Pages 部署前 | 本次准备并即将上传的 Pages 目录 | Chromium `@smoke`；至少覆盖启动、导入、编辑和实际下载 | 失败停止上传与部署 |
| 视觉回归 | 固定 Linux 环境、浏览器、字体和主题 | Chromium 专用 `@visual` 用例 | 基线必须人工审阅 |

E2E 作业使用 `ubuntu-24.04`、.NET 10 和 Node.js 22.x。运行顺序是 checkout、工具安装、依赖恢复、Release 发布、站点准备、类型检查、浏览器安装、测试、报告上传。

必须将新 lockfile 加入 Node 缓存键。缓存 npm 和 NuGet 下载即可。首期不缓存 Playwright 浏览器，减少版本不匹配的排错成本。只在需要 AOT 时安装 `wasm-tools`，不得把 Node.js 包的构建要求直接套到普通 Blazor 发布上。

必须补齐 workflow 的 paths：`src/ChapterTool.Wasm/**`、`src/ChapterTool.Core/**`、`src/ChapterTool.Contracts/**`、`tests/ChapterTool.Wasm.E2E/**`、相关 workflow、共享构建配置及本地化源。Pages 现有 paths 缺少 Contracts 和共享翻译源，接入时必须一并补齐相关依赖触发。

`actions/upload-artifact` 必须使用 `if: always()`，上传 `artifacts/wasm-e2e/` 下的报告、trace、失败截图、日志和下载文件。保留期建议 14 天。部署 smoke 失败必须阻断发布。不得使用 `continue-on-error` 把门禁变成提示。

E2E 作业与现有 .NET 测试保持独立进程和工作目录。不得并行启动多个 `dotnet publish` 写入同一目录。浏览器项目可顺序运行；后续增加 worker 必须先证明状态与产物隔离。

## 10. 分阶段实施与验收

| 阶段 | 实施内容 | 完成条件 |
| --- | --- | --- |
| A：可运行基础 | 新建独立项目、锁依赖、静态服务、Pages 处理、就绪与定位契约、自动诊断 | 一条命令执行 B01；Windows 本地和 Linux CI 均可运行 |
| B：核心门禁 | 完成 B02–B09；加入 PR 和部署前 smoke | 所有 P0 在 Chromium 无重试通过；实际下载与预期内容一致 |
| C：浏览器边界 | 完成 B10–B17；扩展 Firefox、WebKit | 完整行为集三引擎通过；不把兼容性失败长期标记为 skip |
| D：视觉回归 | 完成 B18；固定字体、语言、视口和主题；审阅基线 | 默认 1280×800、宽 1920×1080、窄 390×844 均可完成主流程；几何断言与截图比较通过 |

布局测试必须验证点击和工作流结果。几何断言可检查选项区与状态栏位置、关键控件可见范围和外框溢出。章节表内部水平滚动可以保留。截图保存本身不构成自动断言。

使用 `toHaveScreenshot()` 比较已审阅的基线。必须等待字体就绪并屏蔽动态日志时间等非目标内容。不得通过大面积 mask 或放宽全局阈值隐藏布局变化。视觉基线在固定 Linux 环境生成；Windows 本地截图只用于诊断，不直接替换 Linux 基线。[视觉比较](https://playwright.dev/docs/test-snapshots)。

实施者必须在首次验收报告中记录提交 SHA、依赖锁版本、浏览器版本、操作系统、完整命令、各项目通过/失败/跳过数量、耗时及 artifacts 路径。必须区分计划覆盖与已通过覆盖。

首次稳定性验收必须执行 Chromium P0 用例 `--repeat-each=3 --retries=0`。三引擎至少完成一次无重试验收。重复运行只用于首次稳定性验证或调查失败。不得在常规修改中反复运行已成功且不受影响的测试。

可以保留一次 CI 重试以收集诊断，但 `failOnFlakyTests` 必须使重试后通过的用例仍阻断门禁。失败时先看 trace、控制台和资源请求，再修复代码或同步条件。不得通过固定延迟、删除断言或自动更新视觉基线消除失败。[Trace Viewer](https://playwright.dev/docs/trace-viewer)。

可测性改动涉及 Razor 或浏览器互操作时，必须顺序运行受影响的 .NET 测试项目，再运行 E2E。已有业务测试不能因新增 E2E 被删除。

最终完成标准：新环境可按 README 安装并运行；P0 和三引擎验收通过；部署测试使用实际上传产物；失败报告可定位问题；测试入口写入代码地图；所有未完成项均在验收报告中明确列出。
