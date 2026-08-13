# ChapterTool.Infrastructure 与 Contracts 代码审查

审查日期：2026-08-13
审查范围：`src/ChapterTool.Infrastructure`（36 文件）、`src/ChapterTool.Contracts`（12 文件）
审查目标：进程执行安全与健壮性、设置持久化、正确性、资源管理、架构依赖方向、错误处理。

## 审查过程

1. 先阅读 `docs/code-map/infrastructure.md` 和 `docs/code-map/contracts.md`，确认模块所有权与入口。
2. 逐一通读两个项目的全部 48 个源码文件（含 `.csproj`），无抽样。
3. 对关键疑点做跨层验证：
   - 追踪 `ChapterImportRequest.Path` 的产生位置（`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Import.cs`、`src/ChapterTool.Avalonia/Services/RuntimeChapterLoadService.cs`），确认 CLI 直接传入用户原始（可能是相对）路径。
   - 追踪 `IShellService.OpenAsync` 的调用方（`MainWindowViewModel`、`SettingsToolViewModel`），确认调用侧无异常保护。
   - 核对 `ChapterTool.Contracts.csproj` 与 `ChapterTool.Core` 的引用方向，确认 Core 不反向引用 Contracts。
   - 检索 `DotNetHost` 的全仓引用，确认其无调用方。
4. 对进程执行路径专门验证：双流并发读取是否规避经典死锁、截断后是否继续排空管道、取消/超时路径的 Kill 与收尾等待逻辑。

## 模块概览（简短）

- `Processes/ProcessRunner.cs`：唯一的外部进程执行器。使用 `ArgumentList` 传参、UTF-8 重定向、有界输出捕获（默认 100 万字符）、超时与取消联动、`Kill(entireProcessTree)` 收尾。
- `Tools/`：`ExternalToolLocator`（配置路径 → 搜索目录 → 平台默认位置 → MKVToolNix 安装探测，带正/负结果缓存）、`ExternalToolPathResolver`、`MkvToolNixInstallProbe`（注册表 / /Applications 探测）。
- `Configuration/ChapterToolSettingsStore.cs`：唯一设置持久化实现。按规范化路径共享进程内 `SemaphoreSlim` 锁；temp 文件 + `File.Move(overwrite)` 原子替换；schema 版本升级；损坏文件改名保留（`CorruptSettingsFile`）；拒绝未来版本；按（mtime, length）戳缓存。
- `Importing/`：ffprobe（进程）、ATL（托管 MP4 回退）、mkvextract（进程）、BDMV（纯托管解析），由 `RuntimeChapterImporterRegistry` 按扩展名路由并提供降级链。
- `Platform/`：`ShellService`（打开文件/资源管理器定位/终端）、日志面板 Provider、原生依赖查找、测试替身。
- `Contracts`：设置模型（record + `Normalize` 收敛非法值）、`ISettingsStore<T>`、剪贴板/Shell/日志/工具定位契约。无 Avalonia 依赖，引用 Core（`ChapterDiagnosticCode`、`OutputTextEncodings`、`ChapterSavePath`）。

## 发现

### 高

#### INFRA-01：相对输入路径 + `WorkingDirectory = GetDirectoryName(path)` 使外部工具解析到错误文件

`src/ChapterTool.Infrastructure/Importing/Media/FfprobeMediaChapterReader.cs` 第 27–31 行：

```csharp
var request = new ProcessRunRequest(
    location.Path,
    ["-v", "quiet", "-print_format", "json", "-show_chapters", path],
    Path.GetDirectoryName(path),
    DefaultTimeout);
```

`src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs` 第 44–48 行结构相同（`["chapters", request.Path]` + `Path.GetDirectoryName(request.Path)`）。

- 问题：路径原样作为子进程参数传递，同时把工作目录设为该路径的父目录。子进程用自己的工作目录解析相对路径。当 `path` 是带目录段的相对路径时，两者叠加产生错误路径。
- 触发条件：CLI 直接把用户输入传入导入器（`src/ChapterTool.CommandLine/Cli/ChapterToolCliApplication.Import.cs` 第 29 行 `new ChapterImportRequest(inputPath)`，前置校验只有 `File.Exists`，接受相对路径）。例如在仓库根执行 `chaptertool inspect media/movie.mkv`：工作目录为 `media`，参数仍是 `media/movie.mkv`，ffprobe/mkvextract 实际查找 `media/media/movie.mkv`。结果是"工具退出码非零"这类误导性失败；若巧合存在同名嵌套文件，还会读取错误文件。
- 附带风险：以 `-` 开头的相对文件名会被 ffprobe/mkvextract 当作选项解析。
- 修复方向：在构造 `ProcessRunRequest` 之前执行 `path = Path.GetFullPath(path)`（或在 CLI 入口统一规范化）。绝对路径同时消除 `-` 前缀被误认为选项的问题。

### 中

#### INFRA-02：`ProcessRunner` 成功路径等待输出无超时，管道被孙进程持有时永久挂起

`src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs` 第 31–33 行：

```csharp
await process.WaitForExitAsync(linkedCts.Token);
var stdout = await stdoutTask;
var stderr = await stderrTask;
```

- 问题：`WaitForExitAsync` 只等待进程退出。若被调用工具派生了继承 stdout/stderr 句柄的子进程（孙进程）并在父进程退出后存活，管道不会到达 EOF，`await stdoutTask` 无任何超时或取消手段，调用方永久挂起。`Timeout` 只作用于 `WaitForExitAsync`，此时已经通过。取消路径（第 50–51 行）有 `WaitAsync(KillWaitTimeout)` 保护，成功路径没有对应保护，二者不对称。
- 触发条件：外部工具（当前是 ffprobe/mkvextract，将来任何经 `IProcessRunner` 执行的工具）产生持有句柄的后台子进程。ffprobe 通常不会，但该类是通用执行器，契约上无法约束被执行程序的行为。
- 修复方向：进程退出后对 `stdoutTask`/`stderrTask` 也使用 `WaitAsync`（有界宽限，例如复用 `KillWaitTimeout` 或剩余超时预算），超时则返回已捕获内容并标记截断。

#### INFRA-03：`KillProcess` 只捕获 `InvalidOperationException`，`Kill(entireProcessTree: true)` 的其他异常从取消路径逃逸

`src/ChapterTool.Infrastructure/Processes/ProcessRunner.cs` 第 91–103 行：

```csharp
try
{
    if (!process.HasExited)
    {
        process.Kill(entireProcessTree: true);
    }
}
catch (InvalidOperationException)
{
}
```

- 问题：`Process.Kill(bool)` 还会抛 `Win32Exception`（访问被拒、进程正在终止）和 `AggregateException`（进程树中部分后代无法终止）。这些异常在 `catch (OperationCanceledException)` 块内抛出（第 48 行调用点），会替换原本"超时/取消"的返回语义直接向上传播。上层导入器只捕获 `IOException / UnauthorizedAccessException / Win32Exception / InvalidOperationException`（`FfprobeMediaChapterReader.cs` 第 38 行）：`Win32Exception` 会被误报为"ffprobe could not be started"（实际上已运行并超时），`AggregateException` 则完全不被捕获，直接冲到 CLI/UI 层。
- 触发条件：超时或取消后 Kill 进程树失败（权限不足、后代进程处于不可终止状态）。
- 修复方向：`KillProcess` 补充捕获 `Win32Exception`、`AggregateException`、`NotSupportedException`，保证取消路径始终返回结构化的 `ProcessRunResult`。

#### INFRA-04：`ShellService.RevealInFolderAsync` 在 Windows 上手工引号与 `ArgumentList` 自动引号叠加，含空格路径定位失效

`src/ChapterTool.Infrastructure/Platform/ShellService.cs` 第 41–45 行与第 101–115 行：

```csharp
// explorer /select,"path" highlights the file in Explorer
Run("explorer", $"/select,\"{filePath}\"");
...
foreach (var arg in arguments)
{
    startInfo.ArgumentList.Add(arg);
}
```

- 问题：`ArgumentList` 会按 MSVCRT 规则自动加引号并把内部 `"` 转义为 `\"`，最终命令行是 `explorer "/select,\"C:\My File.txt\""`。explorer.exe 自行解析原始命令行，不理解 `\"` 转义，含空格路径无法被正确定位（通常回退为打开默认目录），不含空格路径也会带上多余的字面引号。
- 触发条件：Windows 上对任何路径调用"在资源管理器中显示"，含空格路径（如 `C:\Program Files\...`、含空格的用户目录）必然失效。
- 修复方向：把整个开关和路径作为一个不含内嵌引号的 `ArgumentList` 项传入（`Add($"/select,{filePath}")`，交给运行时统一加引号），或直接设置 `Arguments = $"/select,\"{filePath}\""` 绕开自动转义。修复后按 AGENTS 要求补行为测试。

#### INFRA-05：`ShellService.OpenAsync` 无异常保护，与同类方法行为不一致，异常穿透到 ViewModel

`src/ChapterTool.Infrastructure/Platform/ShellService.cs` 第 24–34 行：

```csharp
public ValueTask OpenAsync(string target, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    using var p = Start(new ProcessStartInfo
    {
        FileName = target,
        UseShellExecute = true
    });
    return ValueTask.CompletedTask;
}
```

- 问题：`RevealInFolderAsync`、`OpenTerminalAsync` 都用 try/catch + 日志兜底（第 39–60、68–96 行），`OpenAsync` 没有。ShellExecute 启动失败（文件无关联程序、Linux 上缺少 xdg-open、目标已被删除）抛出 `Win32Exception`/`InvalidOperationException`。调用方未设防：`src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs` 第 1019 行、`SettingsToolViewModel.cs` 第 535/543 行都直接 `await shellService.OpenAsync(...)`。
- 触发条件：打开相关媒体文件/设置目录/仓库链接时目标无处理程序或平台组件缺失。
- 修复方向：与同类方法保持一致——捕获启动异常并记录日志（契约 `IShellService` 事实上是"尽力而为"语义），或在契约中明确声明会抛异常并让所有调用方处理。

#### INFRA-06：`BdmvPlaylistScanner` 枚举播放列表只捕获 `IOException`，`UnauthorizedAccessException` 使整个导入操作崩溃

`src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvPlaylistScanner.cs` 第 32–45 行：

```csharp
try
{
    paths =
    [
        .. Directory.EnumerateFiles(directory, "*.mpls", SearchOption.TopDirectoryOnly)
        ...
    ];
}
catch (IOException exception)
{
    diagnostics.Add(DiagnosticSeverity.Warning, ChapterDiagnosticCode.BdmvScanRejected, ...);
    return [];
}
```

- 问题：`Directory.EnumerateFiles` 抛出的 `UnauthorizedAccessException` 不继承 `IOException`，不会被捕获；同样，第 71 行对单个播放列表的 `catch` 列表（`InvalidDataException / EndOfStreamException / IOException`）也不含 `UnauthorizedAccessException`。异常沿 `BdmvImporter.ImportAsync` 原样上抛，导入操作以未分类异常告终，而该模块的设计是把所有失败转成 `ChapterDiagnostic`。
- 触发条件：BDMV 目录位于权限受限的网络共享/外置卷，或个别 `.mpls` 文件 ACL 拒绝读取。
- 修复方向：两处 `catch` 补充 `UnauthorizedAccessException`，与现有诊断降级路径一致。

### 低

#### INFRA-07：工具定位探测的目录枚举异常未防护，且 `LocateAsync` 调用位于导入器异常保护之外

`src/ChapterTool.Infrastructure/Tools/MkvToolNixInstallProbe.cs` 第 52 行 `Directory.EnumerateDirectories(root, "MKVToolNix*.app")` 无 try/catch（`UnauthorizedAccessException`、`IOException` 可抛出）；异常经 `ExternalToolLocator.LocateAsync` 上抛，而两个导入器都把 `LocateAsync` 放在 try 块之外（`MatroskaChapterImporter.cs` 第 36 行、`FfprobeMediaChapterReader.cs` 第 19 行），不会被转成诊断。触发面小（macOS 上 `/Applications` 通常可读），但与模块"失败必转诊断"的约定不符。修复方向：探测枚举加异常防护，或把 `LocateAsync` 纳入导入器的异常映射范围。

#### INFRA-08：设置写入缺少落盘刷新（fsync），掉电窗口内可能留下空文件

`src/ChapterTool.Infrastructure/Configuration/ChapterToolSettingsStore.cs` 第 147–156 行：`File.Create(tempPath)` 序列化后直接 `File.Move(tempPath, settingsPath, overwrite: true)`，未对流执行 `Flush(flushToDisk: true)`。文件系统可能把 rename 元数据先于数据落盘，掉电后 `settings.json` 变为零长/半截文件。有 `CorruptSettingsFile` 兜底（保留损坏文件并回退默认值），后果限于丢设置，故定为低。修复方向：`new FileStream(...)` + `stream.Flush(true)` 后再 Move。

#### INFRA-09：`CorruptSettingsFile.Preserve` 中 `File.Move` 失败会吞掉原始损坏诊断

`src/ChapterTool.Infrastructure/Configuration/CorruptSettingsFile.cs` 第 36–39 行：`File.Move(path, backupPath)` 在跨进程并发读（Windows 上另一进程持有打开句柄）时抛 `IOException`，该异常从 `LoadActiveAsync` 的 `catch (JsonException)` 处理器内传播，调用方收到的是裸 `IOException` 而不是含备份路径与 JSON 错误详情的 `CorruptSettingsFileException`。进程内并发已被路径锁串行化，仅 GUI + CLI 同时运行时可触发。修复方向：Move 失败时仍抛 `CorruptSettingsFileException`（备份路径标记为不可用），把 Move 异常挂到 InnerException。

#### INFRA-10：`MatroskaChapterImporter.SupportedExtensions` 与注册表路由不一致

`src/ChapterTool.Infrastructure/Importing/Matroska/MatroskaChapterImporter.cs` 第 28–32 行只声明 `.mkv`、`.mka`；`RuntimeChapterImporterRegistry.cs` 第 61 行把 `.mks`、`.webm` 也路由给它。CLI 的格式列表（`ChapterToolCliApplication.cs` 第 95 行）基于 `SupportedExtensions` 输出，会向用户少报两个实际支持的扩展名。修复方向：把 `.mks`、`.webm` 加入 `SupportedExtensions`，以路由表为准。

#### INFRA-11：`DotNetHost` 是无引用的死代码，且暗示 shell 间接执行

`src/ChapterTool.Infrastructure/Processes/DotNetHost.cs` 全文返回 `cmd.exe` / `/bin/sh`。全仓无调用方（覆盖率报告同样显示 0%）。若将来被误用为 `ProcessRunRequest.FileName`，参数会经 shell 解释，重新引入注入面。修复方向：删除。

#### INFRA-12：`IApplicationLogService` 的默认空事件实现会静默丢弃订阅

`src/ChapterTool.Contracts/PlatformPorts/IApplicationLogService.cs` 第 7–18 行给 `EntryAdded`/`Cleared` 提供了 `add { } remove { }` 默认实现。未显式声明这两个事件的实现类（当前的 `ApplicationLogPanelProvider` 声明了，无实际故障）会编译通过但静默吞掉所有订阅，属于易踩的契约陷阱。修复方向：移除默认实现，强制实现方声明事件。

#### INFRA-13：`ExternalToolLocator` 对无效的用户配置路径静默降级，缺少诊断

`src/ChapterTool.Infrastructure/Tools/ExternalToolLocator.cs` 第 37–43 行：配置路径存在但不可执行（Unix 缺执行位）或文件不存在时，直接落入搜索目录/默认位置，最终 not-found 消息（第 75–79 行）只说 "External tool 'x' was not found."，不提示"你配置的路径已被检查并拒绝"。用户排障时无法区分"没配置"与"配置错了"。修复方向：not-found 结果的 `Message` 中附带被拒绝的配置路径及原因。

## 已排查、无问题

1. **stdout/stderr 同步双流死锁**：`ProcessRunner.cs` 第 24–29 行在 `WaitForExitAsync` 之前并发启动两个异步读取任务，且 `ReadBoundedAsync`（第 128–156 行）达到上限后继续排空管道而非停读，不会出现经典的双管道互塞死锁。
2. **命令行参数注入与含空格路径**：`ProcessRunner.CreateProcess` 第 83–86 行全部经 `ArgumentList` 逐项传参，`UseShellExecute = false`，无字符串拼接，无 shell 中转（`ShellService` 的 explorer 一处例外已单列为 INFRA-04）。
3. **设置原子写入与并发**：temp 文件 + `File.Move(overwrite: true)` 同卷原子替换，写失败清理 temp（`ChapterToolSettingsStore.cs` 第 140–164 行）；进程内所有读写按 `Path.GetFullPath` 规范化路径经静态 `SemaphoreSlim` 串行（第 13–14、220 行），跨实例、大小写差异均收敛到同一把锁。
4. **`SaveAsync` 在文件损坏/未来版本时先抛异常而不覆盖**：第 40–43 行的前置加载是有意设计——保留损坏文件证据、拒绝覆盖未来 schema 版本；`Preserve` 已把损坏文件改名，用户重试保存即可成功。
5. **缓存戳（mtime + length）漏检**：理论上外部进程在同一时间戳粒度内写入等长内容可骗过 `FileStamp` 缓存，但本仓库所有写入都经同一 store（写后即更新缓存），外部手改设置文件属极端场景，不构成实际缺陷。

## 修复优先级建议

1. **INFRA-01**（高）：在两处导入器（或 CLI 入口）对路径做 `Path.GetFullPath` 规范化。改动小、修复直接的功能错误，建议最先做。
2. **INFRA-03 + INFRA-02**（中）：同属 `ProcessRunner` 收尾健壮性，建议一次修复——扩大 `KillProcess` 捕获范围、给成功路径的输出等待加有界 `WaitAsync`。
3. **INFRA-04 + INFRA-05**（中）：`ShellService` 的 Windows 定位引号修复与 `OpenAsync` 异常兜底，一个文件内一并处理，并补 Windows 行为测试。
4. **INFRA-06**（中）与 **INFRA-07**（低）：导入/定位路径统一补 `UnauthorizedAccessException` 的诊断降级。
5. 其余低级别项（INFRA-08 ~ INFRA-13）可随下次触碰对应文件时顺带处理；其中 INFRA-11（删除 `DotNetHost`）零风险，可即时清理。
