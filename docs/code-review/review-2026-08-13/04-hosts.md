# ChapterTool 宿主项目代码审查（CommandLine / Wasm / Node / npm）

- 审查日期：2026-08-13
- 审查范围：`src/ChapterTool.CommandLine`、`src/ChapterTool.Wasm`、`src/ChapterTool.Node`、`packages/chaptertool`（不含生成的 `dist/`）
- 审查结论摘要：高 0 项，中 5 项，低 11 项。

## 审查过程

1. 阅读 `docs/code-map/README.md`，确认宿主模块的入口与归属。
2. 通读 CLI 全部 19 个文件：`Program.cs`、`ChapterToolCliHost.cs`、`Cli/` 下的命令定义、Convert/Inspect/Selection/Import/Paths 工作流、`CliConsole.cs`、`CliLocalizationManager.cs`、3 份 locale JSON、csproj 与 README。
3. 通读 Wasm 宿主：`Program.cs`、`Home.razor`（约 1570 行）、`WasmWorkspace.cs`、`WasmChapterService.cs`、`WasmLocalizer.cs`、`WasmModels.cs`、`wwwroot/js/download.js`、`index.html`、布局与路由文件、csproj。
4. 通读 Node 宿主：`Program.cs`、`NodeApi.cs`、`NodeCoreApi.cs`、csproj。
5. 通读 npm 包源码：`src/index.ts`、`types.ts`、`api-loader.ts`、`utils/*`、`scripts/build.mjs`、`check-environment.mjs`、`verify-pack.mjs`、`package.json`、`vitest.config.mjs`、全部测试文件、`README.md`。
6. 交叉验证 Core 边界：`ChapterExportFormats`、`OutputTextEncoding(s)`、`PortableInputPolicy`、`ChapterContentService`、`ChapterOutputProjectionService`，用于确认宿主层假设是否成立。

## 模块概览（简短）

- `src/ChapterTool.CommandLine`：DotMake.CommandLine 驱动的 `chaptertool` NuGet Tool。根命令 + `convert` / `inspect` / `formats` 三个子命令。命令类只做声明与绑定，工作流集中在 `ChapterToolCliApplication` 的 partial 文件里。内嵌 en-US / zh-CN / ja-JP 三份 JSON 资源，键集合一致（各 128 键）。
- `src/ChapterTool.Wasm`：Blazor WebAssembly 单页应用。`Home.razor` 承载全部 UI；`WasmWorkspace` 复用 Core 的会话内核（`ChapterWorkspace` / `ClipSessionTransitions`）；JS 互操作走 `window.chapterToolWasm` 全局对象（非 ES module，无 `IJSObjectReference`）。
- `src/ChapterTool.Node`：纯 .NET WASM 宿主，`[JSExport]` 静态方法 + System.Text.Json 源生成序列化，所有复杂类型经 JSON 字符串跨界。
- `packages/chaptertool`（npm 包 `@chaptertool/node`）：TypeScript 封装层，输入校验在 JS 侧完成，运行时经 `createRetryableLoader` 懒加载并全进程共享；构建脚本先 `dotnet publish` 再用 tsdown 打包。

## 发现（按严重级别分组）

### 高

（无）

### 中

#### HOST-01（中）CLI 未设置控制台输出编码，Windows 下中文/日文输出与 `--stdout` 管道内容会乱码

- 证据：`src/ChapterTool.CommandLine/Program.cs:1-11`（入口无任何 `Console.OutputEncoding` 设置）；`src/ChapterTool.CommandLine/Cli/CliConsole.cs:16-22`：

```16:22:src/ChapterTool.CommandLine/Cli/CliConsole.cs
    public void Write(string text) => Console.Out.Write(text);

    public void WriteLine(string text = "") => Console.Out.WriteLine(text);

    public void WriteError(string text) => Console.Error.Write(text);

    public void WriteErrorLine(string text = "") => Console.Error.WriteLine(text);
```

- 问题：CLI 内置 zh-CN / ja-JP 界面语言（`--language`），且 `convert --stdout` 会把导出内容（可能含 Unicode 章节名）直接写到 `Console.Out`（`ChapterToolCliApplication.Convert.cs:145-149`）。在 Windows 上，控制台默认使用 OEM 代码页（如 437/850），重定向到管道时也用该编码；zh-CN/ja-JP 消息与非 ASCII 章节名会变成 `?` 或乱码，`chaptertool convert x.xml --format txt --stdout > out.txt` 得到的文件内容会损坏。
- 触发条件：Windows 上运行且代码页不是 UTF-8（默认情况）；使用 `--language zh-CN`/`ja-JP` 或章节名含非 ASCII 字符。
- 修复方向：在入口处设置 `Console.OutputEncoding = new UTF8Encoding(false)`（stderr 同理），并考虑对不支持的场景做 try/catch 容错。与仓库“控制台输出中文须为合法 UTF-8”的要求一致。

#### HOST-02（中）Wasm 键盘快捷键未阻止浏览器默认行为，Ctrl+S / Ctrl+O / F5 / Ctrl+R 会触发浏览器动作

- 证据：`src/ChapterTool.Wasm/Pages/Home.razor:11-15`（shell 上只有 `@onkeydown="OnKeyDownAsync"`，没有 `@onkeydown:preventDefault`）与 `Home.razor:1532-1554`：

```1538:1548:src/ChapterTool.Wasm/Pages/Home.razor
        if (ctrl && string.Equals(key, "s", StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync();
            return;
        }

        if ((ctrl && string.Equals(key, "r", StringComparison.OrdinalIgnoreCase)) || string.Equals(key, "F5", StringComparison.OrdinalIgnoreCase))
        {
            await ReloadAsync();
            return;
        }
```

- 问题：Blazor 事件处理不会自动 `preventDefault`，`wwwroot/js/download.js` 中也没有任何 keydown 拦截。实际行为：Ctrl+S 同时弹出浏览器“保存网页”对话框并触发应用下载；Ctrl+O 同时打开浏览器文件对话框；F5 / Ctrl+R 直接整页刷新——`ReloadAsync()`（重新导入文件）根本来不及生效，且未保存的章节编辑全部丢失，用户却以为这是应用内“重新加载文件”的快捷键。
- 触发条件：焦点在 shell 内按下上述组合键。
- 修复方向：无法用 Blazor 的无条件 `@onkeydown:preventDefault`（会破坏输入框）。应在 JS 侧注册 keydown 监听，对命中的快捷键组合调用 `event.preventDefault()` 后再回调 .NET；或至少移除 F5/Ctrl+R 的应用内映射。

#### HOST-03（中）Node 宿主静态字段初始化依赖跨 partial 文件的未定义顺序，存在潜在 `TypeInitializationException`

- 证据：`src/ChapterTool.Node/NodeCoreApi.cs:16`：

```16:22:src/ChapterTool.Node/NodeCoreApi.cs
    private static readonly ChapterEditingService EditingService = new(ChapterService!.TimeFormatter);
    private static readonly FrameRateService FrameRateService = new();
    private static readonly ChapterExpressionService ExpressionService = new();
    private static readonly ExpressionAuthoringService ExpressionAuthoringService = new();
    private static readonly LuaExpressionScriptService ExpressionEngine = new();
    private static readonly ChapterOutputProjectionService ProjectionService = new();
    private static readonly ChapterConversionService ConversionService = new(ChapterService.TimeFormatter);
```

  `ChapterService` 定义在另一个 partial 文件 `src/ChapterTool.Node/NodeApi.cs:17`。
- 问题：C# 规范只保证同一声明内静态字段初始化器按文本顺序执行；partial 类跨文件的初始化顺序是未定义的（取决于编译时的文件顺序）。当前 MSBuild 按字母序把 `NodeApi.cs` 排在 `NodeCoreApi.cs` 之前所以能工作；`ChapterService!` 上的 null 抑制符表明编译器已对此发出过警告并被压制。一旦文件重命名/重组导致顺序变化，`EditingService` 初始化时 `ChapterService` 为 null，类型初始化抛 `TypeInitializationException`，npm 包所有 API 首次调用即失败。
- 触发条件：编译单元顺序变化（重命名文件、显式 Compile 列表等）。
- 修复方向：把 `ChapterService` 与依赖它的服务集中到同一个文件的同一声明中，或改用 `static` 构造函数 / `Lazy<T>` 显式控制顺序，去掉 `!`。

#### HOST-04（中）Node/npm：`GetOutputEncodings` 返回的编码 id 与 `export`/`project` 接受的 `textEncoding` 值不一致

- 证据：`src/ChapterTool.Node/NodeApi.cs:179`（导出选项解析）：

```179:179:src/ChapterTool.Node/NodeApi.cs
            Enum.Parse<OutputTextEncoding>(options.TextEncoding, ignoreCase: true),
```

  而 `src/ChapterTool.Node/NodeCoreApi.cs:270-279` 的 `GetOutputEncodings` 返回 `OutputTextEncodings.Id(...)`，即 `"utf8" / "utf16le" / "utf16be" / "utf32le" / "utf32be"`（`src/ChapterTool.Core/Exporting/OutputTextEncoding.cs:60-67`）。TS 类型 `packages/chaptertool/src/types.ts:138` 要求的是枚举名：

```138:138:packages/chaptertool/src/types.ts
  textEncoding?: "Utf8" | "Utf16LittleEndian" | "Utf16BigEndian" | "Utf32LittleEndian" | "Utf32BigEndian";
```

- 问题：`outputEncodings()` 是包公开的“可用编码枚举”API（`types.ts:352-359` 注释称 id 为 “Encoding identifier used by the .NET Core”），但把它返回的 id 回填到 `export({ textEncoding: id })` 时，除 `"utf8"`（恰好大小写不敏感匹配 `Utf8`）外全部抛 `ArgumentException`（`Enum.Parse` 无法把 `"utf16le"` 解析为 `Utf16LittleEndian`）。纯 JS 用户没有 TS 类型保护，极易踩中。同一 id 体系在 Wasm host 中（`downloadText` 的 `encodingId`）却是标准用法，跨宿主语义割裂。
- 触发条件：JS 调用方把 `outputEncodings()[n].id` 传给 `export`/`project` 的 `textEncoding`。
- 修复方向：在 `NodeApi.ToExportOptions` 中先经 `OutputTextEncodings.ParseOrDefault` 尝试 id 解析，再回落到枚举名解析（或统一只接受 id 并同步修改 TS 类型与文档）。

#### HOST-05（中）Node `Import` 对空内容抛裸 .NET 异常，破坏结构化错误契约

- 证据：`src/ChapterTool.Node/NodeApi.cs:20-39`（`INVALID_BASE64` / `INPUT_TOO_LARGE` 返回结构化 `NodeImportResponse`，但空字节数组直接透传到 Core）；`src/ChapterTool.Core/Importing/ChapterContentService.cs:119-123`：

```119:123:src/ChapterTool.Core/Importing/ChapterContentService.cs
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Content is empty.", nameof(content));
        }
```

- 问题：npm 侧 `toBytes` 允许空字符串/空 `Uint8Array`（`packages/chaptertool/src/utils/input.ts:20-37` 只校验上限，不校验下限），因此 `tool.import("")` 会走到 Core 并以被封送的 .NET `ArgumentException` 形式 reject。这与同函数内 `INVALID_BASE64` / `INPUT_TOO_LARGE` 的“返回 `success:false` + 诊断”契约不一致，也与 `index.ts:115-131` TSDoc（只声明 `TypeError` / `RangeError`）不符。Wasm 宿主在 `WasmWorkspace.LoadAsync:512-517` 对空内容做了前置检查，只有 Node 通道漏了。
- 触发条件：JS 调用方传入空字符串或空字节数组（例如读入了 0 字节文件）。
- 修复方向：在 `NodeApi.Import` 中对解码后长度为 0 提前返回 `SerializeImportFailure("EMPTY_INPUT", ...)`，或在 npm 的 `toBytes` 中拒绝空输入并更新 TSDoc。

### 低

#### HOST-06（低）CLI `--output` 显式路径静默覆盖已存在文件

- 证据：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Convert.cs:165`（`File.WriteAllTextAsync(targetPath, ...)` 无存在性检查）；对比默认路径分支 `ChapterToolCliApplication.Paths.cs:29-31` 使用 `ChapterSavePath.AllocateUniqueFilePath` 主动避让重名。
- 问题：同一命令的两条输出路径策略不一致；显式 `--output` 覆盖无任何提示，也没有 `--force`/`--no-overwrite` 开关。CLI 覆盖显式目标可以算约定俗成，但与默认分支的“绝不覆盖”并存时容易让用户误判。
- 修复方向：为覆盖行为补充说明（README/`--help`），或增加 `--force` 并在目标存在时报错退出。

#### HOST-07（低）CLI `--group-index` 越界时报“存在多个组”的误导性错误

- 证据：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Selection.cs:37-45`：

```37:44:src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Selection.cs
        var groupIndex = request.GroupIndex ?? (groups.Count == 1 ? 0 : null);
        if (groupIndex is null || groupIndex < 0 || groupIndex >= groups.Count)
        {
            group = null;
            failure = CliSelectionResult.Failure(
                localizer.GetString("Cli.Error.MultipleGroups"),
                AmbiguousSelectionDiagnostics(groups));
        }
```

- 问题：用户显式传入越界索引（如单组文件传 `--group-index 5`）时，错误消息是 `Cli.Error.MultipleGroups`（“源包含多个组，请指定索引”），与实际原因（索引越界）不符。
- 修复方向：区分“未指定且有多组”与“索引越界”两种失败，分别使用不同的资源键。

#### HOST-08（低）CLI `--frame-rate NaN` 绕过正数校验

- 证据：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Convert.cs:73`：

```73:79:src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Convert.cs
        if (request.FrameRate is <= 0)
        {
            console.WriteErrorLine(localizer.GetString("Cli.Error.FrameRatePositive"));
            format = null!;
            errorCode = 1;
            return false;
        }
```

- 问题：`double.TryParse("NaN")` 成功，而 `NaN <= 0` 为 false，因此 `--frame-rate NaN` 通过校验并作为 `FramesPerSecond` 进入帧相关导出（qpf/celltimes），产出无意义结果。`Infinity` 同理。
- 修复方向：改为 `is not (> 0)` 或显式 `double.IsFinite(x) && x > 0`。

#### HOST-09（低）CLI 与 Wasm 的语言代码归一化规则不一致；CLI 本地化器有进程级 Culture 副作用

- 证据：`src/ChapterTool.CommandLine/Cli/CliLocalizationManager.cs:88-98`（仅精确匹配 `en-US`/`zh-CN`/`ja-JP`，否则静默回落 en-US；`ApplyCulture` 无条件改写 `CultureInfo.CurrentCulture/CurrentUICulture`）；`src/ChapterTool.Wasm/Services/WasmLocalizer.cs:62-76`（额外接受 `zh`、`zh-Hans`、`ja`）。
- 问题：`chaptertool convert x --language zh` 在 CLI 得到英文输出且无任何警告；同样的 `zh` 在浏览器端是合法值。另外每次构造 `CliLocalizationManager`（每条命令都会构造）都会把进程 Culture 改成 en-US（未传 `--language` 时），覆盖用户系统区域设置——目前无实际输出受害者（导出格式化用 Invariant），属于隐蔽副作用。
- 修复方向：CLI 复用与 Wasm 相同的归一化逻辑；对无法识别的 `--language` 输出一条 stderr 警告；避免在构造函数里改全局 Culture。

#### HOST-10（低）Wasm 主界面允许负 OrderShift，但导出投影固定归一化为 0，设置面板 Apply 又静默清零

- 证据：`src/ChapterTool.Wasm/Pages/Home.razor:261-269`（`orderShift` 输入 `min="-1000"`）；`Home.razor:1337`（`orderShift = Math.Max(0, draftOrderShift);`，打开设置面板后点 Apply 即把负值清零）；`src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs:63-76`（Core 将负 shift 归一化为 0 并给 Warning 诊断）。
- 问题：UI 提供了一个 Core 永远拒绝的取值范围（-1000..-1），用户输入负值后每次刷新都收到告警诊断；而“打开设置→Apply”这个与 OrderShift 无关的操作会静默改掉用户在主界面输入的值。三处行为互相矛盾。
- 修复方向：将主界面输入下限改为 0（与 Core 对齐），或让 Core/UI 支持负 shift 并去掉 `Math.Max(0, ...)`。

#### HOST-11（低）Wasm 启动时 localStorage 访问无异常保护，隐私模式/禁用存储的浏览器会命中全局错误 UI

- 证据：`src/ChapterTool.Wasm/Pages/Home.razor:759-800`（`OnAfterRenderAsync` 中多次 `chapterToolWasm.getLocalStorage/setLocalStorage/removeLocalStorage`，仅对 `JsonException` 有 catch）；`wwwroot/js/download.js:33-41`（直接访问 `window.localStorage`，无 try/catch）。
- 问题：在浏览器阻止站点数据（如 Chrome “阻止所有 Cookie”）时，访问 `window.localStorage` 抛 `SecurityError`，该异常沿 JS 互操作传回 `OnAfterRenderAsync` 成为未处理异常，触发 `#blazor-error-ui`，应用首屏即“An unhandled error has occurred”。
- 修复方向：JS 侧包 try/catch 返回 null/忽略写入，或 .NET 侧 catch `JSException` 并降级为“设置不持久化”。

#### HOST-12（低）`Home.razor` 中存在条件重复的死代码块

- 证据：`src/ChapterTool.Wasm/Pages/Home.razor:916-927`：

```919:924:src/ChapterTool.Wasm/Pages/Home.razor
        if (chapterNameModeIndex != 2 && !string.IsNullOrEmpty(Workspace.ChapterNameTemplateText) && chapterNameModeIndex != 2)
        {
            // Switching away from Template keeps stored text until a new template is loaded,
            // but export options only project template text in Template mode.
        }
```

- 问题：条件里 `chapterNameModeIndex != 2` 重复出现，块体为空。这不是纯风格问题：它看起来是一段未完成的逻辑（注释描述了预期行为但没有代码），后续维护者无法判断意图是否已实现。
- 修复方向：删除该块或补全意图（当前模板保留行为实际由 `CreateExportOptions` 的 `ChapterNameModeIndex == 2` 条件保证，块可安全删除）。

#### HOST-13（低）Wasm 大文件加载内存放大：文件字节最多同时存在 3 份

- 证据：`src/ChapterTool.Wasm/Pages/Home.razor:1061-1071`（`OpenReadStream` → `MemoryStream` → `memory.ToArray()`，复制 2 份）；`src/ChapterTool.Wasm/Services/WasmWorkspace.cs:1044`（`lastLoadedSource = new LoadedSourceSnapshot(fileName, content)` 长期持有整个文件用于 Reload）；上限为 64 MiB（`WasmWorkspace.cs:20` / `PortableInputPolicy.MaxBytes`）。拖放路径还要经 `download.js:118` 的 `file.arrayBuffer()` + 互操作传输一份。
- 问题：接近上限的文件在 32 位 WASM 堆里瞬时占用可达 ~200MB（Uint8Array + MemoryStream + ToArray + 快照），在内存受限的浏览器/移动设备上可能触发 OOM。功能上是设计取舍（Reload 需要快照），但复制次数可以减少。
- 修复方向：`LoadFileAsync` 预分配 `file.Size` 大小的数组直接读入（省掉 MemoryStream+ToArray 双份）；评估 Reload 快照是否可改为仅保留小文件。

#### HOST-14（低）64 MiB 输入上限在 JS 侧两处硬编码，与 Core 常量无同步机制

- 证据：`src/ChapterTool.Wasm/wwwroot/js/download.js:82`（`const maxBytes = 64 * 1024 * 1024;`）；`packages/chaptertool/src/utils/input.ts:6`（`export const MAX_INPUT_BYTES = 64 * 1024 * 1024;`）；真源是 `src/ChapterTool.Core/Boundaries/PortableInputPolicy.cs:7`。
- 问题：三处魔法数字独立维护。Core 上限调整后，JS 侧检查会静默失配：要么提前拒绝合法输入，要么放行后在 .NET 侧才失败（拖放路径会先把超限字节整体传过互操作边界）。
- 修复方向：Wasm 可在启动时把 `WasmWorkspace.MaxLoadBytes` 传给 `registerDropZone`；npm 包可在构建时生成常量或在运行时从 `NodeApi` 读取。

#### HOST-15（低）`package.json` 的 `exports` 缺少 `require`/`default` 条件，且无顶层 `main`/`types` 回退

- 证据：`packages/chaptertool/package.json:6-11`：

```6:11:packages/chaptertool/package.json
  "exports": {
    ".": {
      "types": "./dist/index.d.ts",
      "import": "./dist/index.mjs"
    }
  },
```

- 问题：Node ≥22 已支持 `require(esm)`，但条件解析对 `require` 调用只匹配 `require`/`default` 条件；这里两者都缺，`require("@chaptertool/node")` 直接 `ERR_PACKAGE_PATH_NOT_EXPORTED` 而不是给出可用的 ESM。同时缺顶层 `types` 字段，`moduleResolution: node10` 的旧 TS 项目找不到类型声明。
- 修复方向：给 `"."` 增加 `"default": "./dist/index.mjs"`，并补顶层 `"types"`（可选 `"main"`）。

#### HOST-16（低）跨宿主输出编码/BOM 默认值不一致：同一转换在不同宿主产出不同字节

- 证据：CLI 固定 UTF-8 无 BOM 且无可配置项：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Convert.cs:165`：

```165:165:src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Convert.cs
        await File.WriteAllTextAsync(targetPath, export.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
```

  Wasm 默认 UTF-8 + BOM（`src/ChapterTool.Wasm/Services/WasmWorkspace.cs:85-86` `EmitBom = true`，并提供 5 种编码选项）；Node 默认同样 `EmitBom: true`（`src/ChapterTool.Node/NodeApi.cs:265-266`）。
- 问题：同一份章节文件转同一格式，CLI 结果无 BOM，浏览器下载/Node 导出默认带 BOM；CLI 也没有 `--encoding`/`--bom` 选项对齐其他宿主能力。对逐字节比较或对 BOM 敏感的下游工具（如某些 qpfile 消费者）会产生差异。
- 修复方向：统一默认值（建议全部无 BOM 的 UTF-8），并为 CLI 增补 `--encoding`/`--bom` 选项以对齐 Wasm/Node 的能力面。

## 已排查、无问题

1. **CLI 参数规则合规**：所有参数定义/解析/绑定均经由 DotMake.CommandLine（`[CliCommand]`/`[CliOption]`/`Cli.Parse`，`ChapterToolCliSupport.cs:13-17`）；`Program.cs` 与 CLI 支撑文件中没有任何手写 `args` 识别或分发。
2. **Node 的 sync-over-async 不会死锁**：`NodeApi.Import` 的 `GetAwaiter().GetResult()`（`NodeApi.cs:37`）作用于 `ChapterContentService.ImportAsync`，其内部只操作 `MemoryStream` 上的纯托管 importer，任务同步完成，单线程 WASM 上无阻塞风险。
3. **Wasm 行索引对齐**：网格 `rows` 来自 `ChapterOutputProjectionService.Project` 的 `Info.Chapters`，投影保持章节数量与顺序（含分隔行，`ChapterOutputProjectionService.cs:42-58`），与 `BaseChapterSet.Chapters` 按索引一致，编辑/删除不会错位。
4. **Wasm 生命周期无泄漏**：`Home.razor` 的 `dropZoneRef`（`DotNetObjectReference`）在 `Dispose` 中释放，`Workspace.Changed` 与 `L.CultureChanged` 均成对退订（`Home.razor:803-808`）；JS 互操作使用全局对象而非 module，无 `IJSObjectReference` 需要释放；拖放监听绑定在组件自身元素上，随 DOM 移除。
5. **download.js 编码器正确性**：`encodeText` 的 UTF-16 按 code unit（`charCodeAt`）、UTF-32 按 code point（`for...of` + `codePointAt`）编码，BOM 字节序均正确；`encodingId` 的可能取值由 `OutputTextEncodings.Id` 封闭为 5 个已处理分支，不存在未知 id 落入错误分支的路径。`URL.revokeObjectURL` 紧跟同步 `click()` 是主流浏览器安全的惯用法。

## 修复优先级建议

1. **先修 HOST-01（CLI 控制台编码）**：影响 Windows 上所有非英语输出与 `--stdout` 管道数据正确性，修复成本一行级。
2. **HOST-04 + HOST-05（Node/npm 边界契约）**：一起修，属于同一个 `NodeApi` 错误契约问题；npm 包已发布版本号，契约修正越早代价越小。
3. **HOST-03（partial 静态初始化顺序）**：现在能跑但属于定时炸弹，重构一处文件即可消除。
4. **HOST-02（Wasm 快捷键）**：影响浏览器端核心交互（保存/加载/刷新），需要 JS 侧配合，工作量中等。
5. **低级项**：HOST-15（package.json）与 HOST-08（NaN 校验）改动极小可顺手修；HOST-16（跨宿主 BOM 默认）建议在下一次 CLI 功能迭代中随 `--encoding` 选项一并处理；其余按维护节奏安排。
