# ChapterTool 测试工程代码审查

审查日期：2026-08-13
审查范围：`tests/` 下全部测试工程（只审查测试代码与覆盖缺口，未运行测试）

## 审查过程

1. 阅读 `docs/code-map/testing.md` 与仓库根目录 `AGENTS.md` 中的测试规则。
2. 枚举 `tests/` 下 92 个 `.cs` 文件，按行数排序确定阅读优先级。
3. 对全部测试工程执行规则符合性扫描：`[AvaloniaFact]`/`[AvaloniaTheory]` 归属、`AvaloniaHeadlessTestCollection` 标注、把源码/配置文件当文本读取、`Skip`/注释掉的测试、`Task.Delay`/`Thread.Sleep`、`Dispatcher.UIThread.Invoke`、`SettingsToolViewModel` 的 `autoLoad` 参数、临时目录与环境变量依赖。
4. 精读关键文件：CLI 测试全文、Wasm 测试全文、`ProcessRunnerTests`、`SettingsMigrationTests`（尾部）、`ExternalToolLocatorTests`（尾部）、Headless 全部主要文件、两个守卫测试、`BdmvImporterTests`、`StcAwarePtsTests`、`RuntimeChapterSaveServiceTests`、`LocalizationTests`、`PlatformServiceTests` 等；抽样阅读 `MainWindowViewModelTests`、`TextImporterTests`、`DiscImporterTests`、`LuaExpressionScriptServiceTests`。
5. 将 `src/` 各工程的类型清单与测试引用交叉比对（`rg` 引用计数），确认覆盖缺口；对疑似缺口（`SecureXmlLoader`、`CueTextDecoder`、`ExternalToolPathResolver` 等）核实是否有间接覆盖。

## 测试布局概览（简短）

| 工程 | 文件数 | 说明 |
| --- | --- | --- |
| `tests/ChapterTool.Core.Tests` | 38 | 解析/编辑/变换/导出/会话；`Fixtures/` + `FixtureResolver` |
| `tests/ChapterTool.Avalonia.Tests` | 20 | ViewModel/CLI 端口/服务/本地化单元测试；含 `NoAvaloniaHeadlessAttributeGuardTests` 守卫 |
| `tests/ChapterTool.Avalonia.Headless.Tests` | 16 | Headless UI 测试，独立进程；含 `HeadlessTestCollectionGuardTests` 守卫 |
| `tests/ChapterTool.Infrastructure.Tests` | 16 | 进程执行、外部工具定位、设置持久化、工具型导入器（含 mkvextract/ffprobe 集成测试） |
| `tests/ChapterTool.CommandLine.Tests` | 1 | DotMake CLI 绑定与 convert/inspect/formats 工作流 |
| `tests/ChapterTool.Wasm.Tests` | 1 | 浏览器工作区行为（普通 net10.0 testhost 上运行） |
| `tests/MplsVerify` | 0 | 仅剩 `bin/`、`obj/`，无源码、未列入 `ChapterTool.slnx` |

整体印象：测试质量高于平均水平。Headless 测试以用户行为/工作流为断言目标，普遍使用 `try/finally` + `CloseWindowAsync` 清理；两条 AGENTS.md 关键规则均有编译期守卫测试自动保护；临时文件清理纪律良好（`try/finally`、`IDisposable`、`TempDirectory`）。未发现"高"级别问题；下列发现集中在个别假绿模式、环境脆弱性与弱断言。

## 发现（按严重级别分组）

### 高

无。两条最易导致挂起/假绿的仓库规则（非 Headless 工程禁用 `[AvaloniaFact]`、Headless 类必须入 `AvaloniaHeadlessTestCollection`）均已由守卫测试强制执行且当前无违例（见"已排查"）。

### 中

#### TEST-01 环境变量门控测试静默返回，永久假绿且依赖个人机器路径

- 位置：`tests/ChapterTool.Infrastructure.Tests/Importing/BdmvImporterTests.cs:183-198`

```183:198:tests/ChapterTool.Infrastructure.Tests/Importing/BdmvImporterTests.cs
    [Fact]
    public async Task FullDiscPlanValuesCanBeVerifiedOptIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CHAPTERTOOL_RUN_FULL_DISC_PARITY"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var discRoot = @"D:\Downloads\[BDMV][アニメ][131213] 劇場版 STEINS;GATE 負荷領域のデジャヴ\BDISO";
```

- 问题：未设置环境变量时测试直接 `return`，在所有报表中显示为"通过"，但没有执行任何断言（假绿）。即使设置了环境变量，断言依赖硬编码的 Windows 个人磁盘路径 `D:\Downloads\...`，在 CI 与其他开发机上必然失败。
- 建议：改用 xUnit v3 的 `Assert.Skip("CHAPTERTOOL_RUN_FULL_DISC_PARITY not set")`（同工程 `MatroskaIntegrationTests.RequireMkvToolNix` 已是正确示范），并把磁盘根路径改为从环境变量读取，避免绑定个人机器。

#### TEST-02 ffprobe 集成测试在工具缺失时硬失败，与 Matroska 集成测试的跳过策略不一致

- 位置：`tests/ChapterTool.Infrastructure.Tests/Importing/FfprobeMediaChapterIntegrationTests.cs:67-74`

```67:74:tests/ChapterTool.Infrastructure.Tests/Importing/FfprobeMediaChapterIntegrationTests.cs
    private static async ValueTask<ExternalToolLocation> LocateFfprobeAsync()
    {
        var searchDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var locator = new ExternalToolLocator(new EmptySettingsStore(), searchDirectories, new EmptyMkvToolNixInstallProbe());
        var location = await locator.LocateAsync("ffprobe", TestContext.Current.CancellationToken);
        Assert.True(location.Found, location.Message ?? "External tool 'ffprobe' was not found.");
```

- 问题：`mkvextract` 缺失时 `MatroskaIntegrationTests` 用 `Assert.Skip` 明确跳过（`MatroskaIntegrationTests.cs:89-95`），而 `ffprobe` 缺失时此处 `Assert.True(location.Found)` 直接判失败。未安装 ffmpeg 的开发机运行整套 Infrastructure 测试会红，属于环境脆弱且策略不一致。
- 建议：与 Matroska 集成测试对齐，改为 `Assert.Skip`，并在消息中说明安装方式。

#### TEST-03 Headless 主题服务测试违反"禁止冗余 Dispatcher.UIThread.Invoke"规则

- 位置：`tests/ChapterTool.Avalonia.Headless.Tests/Services/AvaloniaThemeApplicationServiceTests.cs:17-21、64-68`

```17:21:tests/ChapterTool.Avalonia.Headless.Tests/Services/AvaloniaThemeApplicationServiceTests.cs
    [AvaloniaFact]
    public void ApplyWritesSemanticBrushResourcesAndDarkVariant()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
```

- 问题：AGENTS.md 明确规定"In `[AvaloniaFact]` and `[AvaloniaTheory]`, do not use redundant `Dispatcher.UIThread.Invoke`. The runner already dispatches to the UI thread."。该类两个测试的整段测试体都包裹在 `Dispatcher.UIThread.Invoke` 中。当前 Avalonia 的 `Invoke` 在同线程时同步内联执行，不会挂起，但这是仓库规则的直接违例，且是全仓库唯一违例点。
- 建议：删除两处 `Dispatcher.UIThread.Invoke(() => { ... })` 包装，测试体直接执行；保留 `finally` 中的主题还原。

#### TEST-04 CLI 导入错误路径与 fallback 导入器完全未测试

- 位置（被测代码）：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Import.cs:18-48`

```29:47:src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Import.cs
        var result = await importer.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
        if (!result.Success)
        {
            var fallback = importerRegistry.ResolveFallback(inputPath, importer, result);
            if (fallback is not null)
            {
                result = await fallback.ImportAsync(new ChapterImportRequest(inputPath), cancellationToken);
                if (result.Success)
                {
                    var diagnostics = result.Diagnostics.Concat([
                        new ChapterDiagnostic(
                            DiagnosticSeverity.Info,
                            ChapterDiagnosticCode.ImporterFallbackUsed,
```

- 问题：`tests/ChapterTool.CommandLine.Tests/Cli/ChapterToolCliApplicationTests.cs` 的 convert/inspect 测试全部使用同一个成功导入的 XML fixture（`XmlFixture()`，见该文件 409-417 行）。`InputNotFound`、`UnsupportedInput`、fallback 导入器成功并追加 `ImporterFallbackUsed` 诊断这三条分支没有任何 CLI 级测试；CLI 对非文本输入（如 `.mpls`、`.cue`）的端到端 convert 也未覆盖。
- 建议：补三个测试——不存在的路径断言 `Cli.Error.InputNotFound` 输出与退出码；无法识别的扩展名断言 `UnsupportedInput`；用 `Fixtures/Importing/Disc/Mpls` 下现成 fixture 走一次 `convert` 验证二进制输入路径。

#### TEST-05 Wasm Append 测试名称与实际验证内容不符，成功合并路径无覆盖

- 位置：`tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs:68-95`

```68:95:tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs
    [Fact]
    public async Task AppendMplsMergesGroupsAndKeepsSessionOnFailure()
    {
        // ...
        // Seed via public load of text first, then inject MPLS groups through Append path by loading synthetic binary is hard.
        // Instead exercise Append against a workspace prepared with a successful text load and replace via Append failure path,
        // then verify non-MPLS append is rejected without clearing the session.
        // ...
        // Direct segment append contract covered by Core tests; browser workspace surfaces CanAppend only for MPLS sessions.
        _ = existing;
        _ = appended;
    }
```

- 问题：测试名声称 "MergesGroups"，实际只验证了拒绝路径；`CreateMplsImport` 构造的 `existing`/`appended` 变成 `_ =` 丢弃的死代码。`WasmWorkspace.AppendMplsAsync`（`src/ChapterTool.Wasm/Services/WasmWorkspace.cs:582` 起）的成功合并路径在浏览器工作区层没有测试（Core 层的 `ClipSessionTransitions.Append` 有覆盖，见 `tests/ChapterTool.Core.Tests/Session/ClipSessionTests.cs:75-97`）。
- 建议：仓库 `Fixtures/Importing/Disc/Mpls` 下有多个真实 `.mpls` 文件（同文件 36、60、222 行的测试已在使用），可先 `LoadAsync` 一个 MPLS 再 `AppendMplsAsync` 第二个，断言分组合并与行数变化；删除死代码与 `CreateMplsImport` 辅助方法，或将测试更名为只描述拒绝行为。

#### TEST-06 Wasm 分区（zones）断言以运行时条件为前提，可能静默不执行

- 位置：`tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs:125-131`

```125:131:tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs
        workspace.SelectedFrameRateIndex = 1; // pick a fixed rate when available
        workspace.ApplyOptionsAndRefresh();
        if (workspace.FramesPerSecond > 0)
        {
            var zones = workspace.CreateZonesForSelection();
            Assert.False(string.IsNullOrWhiteSpace(zones));
        }
```

- 问题：`CreateZonesForSelection` 的断言包在 `if (workspace.FramesPerSecond > 0)` 里。若帧率选项行为改变导致条件为假，zones 断言静默跳过，测试仍通过（局部假绿）。且断言本身只验证"非空白字符串"，未验证 zones 内容。
- 建议：先 `Assert.True(workspace.FramesPerSecond > 0)` 把前提变成显式断言，再对 zones 输出内容（帧号区间格式）做具体断言。

### 低

#### TEST-07 测试基建重复：五套仓库根定位/夹具解析逻辑 + 两份相同的 TestApplicationLogger

- 证据：
  - `tests/ChapterTool.Core.Tests/FixtureResolver.cs` 与 `tests/ChapterTool.Infrastructure.Tests/FixtureResolver.cs` 除命名空间与固定路径段外近乎相同（diff 验证）。
  - `tests/ChapterTool.CommandLine.Tests/Cli/ChapterToolCliApplicationTests.cs:419-434`（`RepositoryRoot()` 以 `openspec`+`src` 目录判定根）。
  - `tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs:369-403`（`LocateFixture` 两轮向上遍历，以 `ChapterTool.slnx` 判定根）。
  - `tests/ChapterTool.Avalonia.Headless.Tests/Headless/MainWindowHeadlessTestHost.cs:296` 附近又一套向上遍历。
  - `tests/ChapterTool.Avalonia.Tests/TestApplicationLogger.cs` 与 `tests/ChapterTool.Avalonia.Headless.Tests/TestApplicationLogger.cs` 内容相同（仅命名空间/BOM 差异）。
- 问题：CommandLine、Wasm、Headless 三个工程各自实现"向上找仓库根再伸手拿 `ChapterTool.Core.Tests/Fixtures`"，判定条件互不相同（`openspec` vs `ChapterTool.slnx`）。目录结构调整或从发布目录运行时会以不同方式失败，修复需改 5 处。
- 建议：抽一个共享的测试工具源文件（如以 `<Compile Include>` 链接或独立 `ChapterTool.TestSupport` 工程）统一 `RepositoryRoot`/`Fixture` 解析与 `TestApplicationLogger`。

#### TEST-08 MatroskaIntegrationTests 初始化创建的临时设置目录从不清理

- 位置：`tests/ChapterTool.Infrastructure.Tests/Importing/MatroskaIntegrationTests.cs:17-36`

```17:36:tests/ChapterTool.Infrastructure.Tests/Importing/MatroskaIntegrationTests.cs
    public async ValueTask InitializeAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        // ...
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
```

- 问题：每次运行都在 `%TMP%/ChapterTool.Tests/` 下留下一个 GUID 目录；`DisposeAsync` 为空。同套件其他文件（如 `RuntimeChapterSaveServiceTests`、`StcAwarePtsTests`）都用 `finally`/`Dispose` 删除，此处是清理纪律的唯一遗漏点。
- 建议：把 `root` 存字段，在 `DisposeAsync` 中删除。

#### TEST-09 存在只断言"非空"的构造冒烟测试与恒真谓词断言

- 位置 1：`tests/ChapterTool.Avalonia.Tests/ViewModels/MainWindowViewModelTests.cs:34-62`（`ConstructsDocumentedCommands` 对 22 个命令逐一 `Assert.NotNull`，只验证构造不抛异常，不验证任何行为；命令行为在其他测试中已覆盖，本测试增量价值接近零）。
- 位置 2：`tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs:277-280`：

```277:280:tests/ChapterTool.Wasm.Tests/WasmWorkspaceTests.cs
        Assert.Contains(workspace.Diagnostics, diagnostic =>
            diagnostic.Code.Contains("Expression", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Code.Contains("Lua", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Message.Length > 0);
```

  谓词以 `|| diagnostic.Message.Length > 0` 结尾，任何有消息的诊断都满足，等价于 `Assert.NotEmpty(Diagnostics)`（该弱化被随后的 282 行 StatusText 断言部分弥补）。
- 建议：位置 1 可删除或改为对代表性命令断言 `CanExecute` 初始状态；位置 2 删除恒真分支，只保留 `Expression`/`Lua` 前缀匹配。

#### TEST-10 死目录：tests/MplsVerify 只剩构建产物；Avalonia.Tests 下有三个空目录

- 证据：
  - `tests/MplsVerify/` 仅含 `bin/`、`obj/`，无 `.csproj`、无源码，也未列入 `ChapterTool.slnx`（已核对 slnx 项目清单），任何文档/工程文件均无引用。
  - `tests/ChapterTool.Avalonia.Tests/Cli/`、`Composition/`、`Session/` 三个目录为空（`rg --files` 无输出）。
- 问题：孤儿构建产物与空目录误导导航（本次审查任务描述也把 MplsVerify 当作在用的工具工程）。
- 建议：删除 `tests/MplsVerify` 与三个空目录；若 MplsVerify 仍有历史意义，在 `docs/code-map/testing.md` 说明其去向。

#### TEST-11 SettingsToolHeadlessTests 使用无 GUID 的固定临时路径

- 位置：`tests/ChapterTool.Avalonia.Headless.Tests/Headless/SettingsToolHeadlessTests.cs:114`

```114:114:tests/ChapterTool.Avalonia.Headless.Tests/Headless/SettingsToolHeadlessTests.cs
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "ChapterTool-settings-folder-test");
```

- 问题：与全仓库"`ChapterTool.Tests/<GUID>`"的命名惯例不一致。当前该路径只作为字符串传给 `FakeShellService` 断言，不落盘，无实际风险；但若将来 ViewModel 开始校验/创建该目录，固定路径会引入跨运行状态。纯一致性问题。
- 建议：改用 `Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"))`。

## 覆盖缺口

按风险排序（前两条已在发现 TEST-04、TEST-05 中给出证据与建议）：

1. **CLI 导入错误与 fallback 分支**（中，= TEST-04）：`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Import.cs:18-48` 的 `InputNotFound`/`UnsupportedInput`/`ImporterFallbackUsed` 全部未测；CLI 对二进制输入格式（`.mpls`/`.cue` 等，经 `RuntimeChapterImporterRegistry` 解析）无端到端测试。
2. **Wasm 浏览器工作区 Append 成功路径**（中，= TEST-05）：`src/ChapterTool.Wasm/Services/WasmWorkspace.cs:582` 起的 `AppendMplsAsync` 成功合并仅在 Core 层验证，浏览器工作区包装层（字节上限检查 + 导入 + 会话过渡 + 状态文案）无成功用例。
3. **ShellService 的 macOS/Linux 启动分支**（低）：`src/ChapterTool.Infrastructure/Platform/ShellService.cs` 的 `OpenAsync`/`RevealInFolderAsync`/`OpenTerminalAsync` 有 Windows/macOS/其他三分支；`tests/ChapterTool.Infrastructure.Tests/PlatformServiceTests.cs:41-65` 只覆盖 Windows `cmd.exe` StartInfo 组装与失败日志。建议参照 `CreateWindowsCommandPromptStartInfo` 的做法，把 macOS/Linux 的 StartInfo 组装提为可测的静态方法并补断言（进程真实启动可不测）。
4. **CLI 本地化文化切换的命令级行为**（低）：`CliLocalizationManager` 有独立测试（`ChapterToolCliApplicationTests.cs:12-27`），但没有测试证明 convert/inspect 输出会随 CLI 语言设置切换（`src/ChapterTool.CommandLine/Cli/CliLocalizationManager.cs` 与命令管线的接线）。

## 已排查、无问题

1. **非 Headless 工程无 `[AvaloniaFact]`/`[AvaloniaTheory]`**：全仓扫描确认所有该属性只出现在 `tests/ChapterTool.Avalonia.Headless.Tests`；且 `tests/ChapterTool.Avalonia.Tests/NoAvaloniaHeadlessAttributeGuardTests.cs:11-25` 用反射在编译产物层面持续强制该规则。
2. **Headless 测试类全部在 `AvaloniaHeadlessTestCollection` 中**：14 个含 Avalonia 测试属性的类均带 `[Collection(AvaloniaHeadlessTestCollection.Name)]`；`HeadlessTestCollectionGuardTests.cs:8-22` 提供反射守卫。
3. **无"把源码/配置当文本读取断言"的违例**：全部 `ReadAllText`/`ReadAllLines` 命中点均为测试自己产出的输出文件或数据 fixture（如 `LuaExpressionScriptServiceTests.cs:141-142` 读取 `UVa-12803.in/.out` 数据夹具、`RuntimeChapterSaveServiceTests.cs:32` 读取被测服务写出的 cue 文件）。`FixtureLayoutTests` 只枚举目录布局，不读文件内容。
4. **`SettingsToolViewModel` 构造均传 `autoLoad: false`**：全部 10 处构造点（`SettingsToolViewModelTests.cs:734-743` 的工厂、`ToolViewModelPortConstructionTests.cs:21`、`SettingsToolHeadlessTests` 全部用例）均显式传参并手动 `LoadAsync`。
5. **无 `Task.Delay`/`Thread.Sleep`/被跳过或注释掉的测试**：全仓扫描零命中；异步 UI 等待统一走 `Dispatcher.UIThread.RunJobs()` + `Task.Yield`（如 `AvaloniaWindowServiceHeadlessTests.cs:295-300` 的 `DrainUiAsync`）。`ProcessRunnerTests` 的超时/取消用例（100ms 超时 vs 5s sleep）裕量合理，不构成时序脆弱。

## 修复优先级建议

1. **先修 TEST-01 与 TEST-02（一致性 + 假绿）**：两者改法相同且极小——统一用 `Assert.Skip` 表达"环境不满足"；TEST-01 另需把个人磁盘路径参数化。改完后测试报表中的"通过"数才真实可信。
2. **修 TEST-03**：删除两处冗余 `Dispatcher.UIThread.Invoke` 包装，消除 AGENTS.md 规则的唯一违例点，避免被后续贡献者当作模板复制。
3. **补 TEST-04 的三个 CLI 测试**：CLI 是对外发布的 NuGet 工具（`chaptertool` 命令），错误消息与退出码属于对外契约，当前完全无覆盖的分支风险最高。
4. **修 TEST-05/TEST-06**：补 Wasm Append 成功用例并清理死代码；把条件断言改为显式前提断言。
5. **低优先级批量清理（TEST-07 ~ TEST-11）**：统一夹具解析基建、补 `MatroskaIntegrationTests` 清理、删除 `tests/MplsVerify` 与空目录。可合并为一次独立的测试卫生 PR。
