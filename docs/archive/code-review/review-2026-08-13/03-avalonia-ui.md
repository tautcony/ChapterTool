# ChapterTool Avalonia UI 与桌面宿主代码审查

- 审查日期：2026-08-13
- 审查范围：`src/ChapterTool.Avalonia.UI`（共享 Avalonia 视图 / ViewModel / 工作流 / 资源 / 平台端口）、`src/ChapterTool.Avalonia`（桌面宿主与 Autofac 组合根）
- 审查方式：静态阅读源码（.cs 与 .axaml），以缺陷为先；每个发现均给出文件路径、行号与代码证据

## 审查过程

1. 先读 `docs/code-map/avalonia.md`，确定模块归属与入口。
2. 系统阅读 ViewModel 层：`MainWindowViewModel`（含全部 partial 分部）、`SettingsToolViewModel`、`LogToolViewModel`、`TextToolViewModel`、`ExpressionToolViewModel`、`LanguageToolViewModel`、`ForwardShiftToolViewModel`、`UiCommand`、`ShortcutRouter`。
3. 阅读视图层：`MainView.axaml(.cs)`、全部 `Views/Tools/*.axaml`、`Views/Controls/ExpressionEditor.axaml(.cs)`，并逐项核对 axaml 绑定路径与 ViewModel 属性。
4. 阅读桌面宿主：`App.axaml.cs`、`Composition/*`（`AppCompositionRoot`、`AvaloniaPlatformModule`、`LoggingModule`、`AuxiliaryToolsModule`）、`Services/*`（`AvaloniaWindowService`、`StandardToolCatalogFactory`、`AvaloniaFontFamilyCatalog` 等）。
5. 针对可疑点做全仓验证：`rg` 搜索 `Task.Run` / `ConfigureAwait(false)`（无匹配）、`Canvas` 绝对定位（无匹配）、`new AppLocalizationManager`（4 处）、`Dispose` 链路（工具宿主两条路径均覆盖）。
6. 本地化资源文件按 UTF-8 校验，未发现非法编码。

## 模块概览（简短）

- `ChapterTool.Avalonia.UI`：`MainWindowViewModel` 为核心状态机（partial 分部拆分导入导出、编辑、状态日志等）；`UiCommand` 统一实现 `ICommand`（`IsExecuting` 门控 + 可选 `ErrorHandler`）；辅助工具（设置/日志/表达式/语言/区间/平移/模板名）以 `ToolDescriptor` 目录驱动，宿主可为嵌入面板（`EmbeddedAuxiliaryToolHost`）或原生窗口（`AvaloniaWindowService`）。本地化通过 `AppLocalizationManager` + `AvaloniaLocalizationResourceAdapter` 把词条写入 Avalonia 动态资源，语言切换用 `DynamicResource` 自动刷新。
- `ChapterTool.Avalonia`：`App` → `AppCompositionRoot`（Autofac）构建单例图；`AvaloniaPlatformModule` 注册本地化、主题、字体、Shell 服务；`StandardToolCatalogFactory` 组装工具目录；`LoggingModule` 桥接 Serilog。

## 发现（按严重级别分组）

### 高

#### UI-01 `AppLocalizationManager` 构造函数改写线程 Culture，多处以 `"en-US"` 实例化导致用户所选语言的 Culture 被静默覆盖

证据一——构造函数副作用（`src/ChapterTool.Avalonia.UI/Localization/AppLocalizationManager.cs` 第 9-16、77-87 行）：

```csharp
public AppLocalizationManager(
    string? initialCultureName = null, ...)
{
    ...
    ApplyCulture(CurrentCultureName, raiseEvent: false);   // 第 15 行
}

private void ApplyCulture(string cultureName, bool raiseEvent)
{
    var culture = CultureInfo.GetCultureInfo(cultureName);
    CultureInfo.CurrentCulture = culture;                   // 第 80 行
    CultureInfo.CurrentUICulture = culture;                 // 第 81 行
    ...
}
```

证据二——多处以 `"en-US"` 作为字段初始化器构造新实例：

- `src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.StatusLog.cs` 第 13 行：`private readonly IAppLocalizer logContentLocalizer = new AppLocalizationManager("en-US");`
- `src/ChapterTool.Avalonia.UI/Workflows/StatusDiagnosticsPresenter.cs` 第 21 行：同上
- `src/ChapterTool.Avalonia.UI/ViewModels/Tools/LogToolViewModel.cs` 第 456 行：`private readonly IAppLocalizer contentLocalizer = new AppLocalizationManager("en-US");`

问题：这些实例的用途只是"用英文资源格式化日志文案"，但构造函数会把**当前线程（UI 线程）**的 `CurrentCulture` / `CurrentUICulture` 重置为 en-US。共享单例 localizer 在用户切换语言时通过 `SetCulture` 把线程 Culture 设为所选语言（如 zh-CN），随后任何一次上述构造都会把它悄悄改回 en-US。

触发条件：用户选择非英语语言后，打开日志工具（构造 `LogToolViewModel`），或应用重启后主 ViewModel 构造（`StatusDiagnosticsPresenter` 在 `MainWindowViewModel` 构造函数内创建，早于设置加载，会先污染再被 `LoadSettingsAsync` 纠正；日志窗口则在运行期任意时刻污染且无人纠正）。

影响：所有依赖线程 Culture 的行为退回 en-US——共享 localizer 的 `Format` 用 `CultureInfo.CurrentUICulture` 格式化参数（`AppLocalizationManager.cs` 第 66 行）、`AvaloniaFontFamilyCatalog` 用 `CultureInfo.CurrentUICulture` 选择字体显示名、以及一切未显式指定 Culture 的数字/日期 `ToString`。

修复方向：把 `ApplyCulture` 的线程 Culture 写入从构造函数移除（仅 `SetCulture` 显式切换时执行），或为构造函数增加 `applyThreadCulture: false` 之类参数；"固定英文日志文案"场景只需要资源查找，不需要也不应该改写线程 Culture。

### 中

#### UI-02 `SettingsToolViewModel` 构造函数发起的自动加载任务未观察异常，非预期异常静默丢失

证据（`src/ChapterTool.Avalonia.UI/ViewModels/SettingsToolViewModel.cs` 第 119 行）：

```csharp
InitializationTask = autoLoad ? InitializeAsync() : Task.CompletedTask;
```

`InitializeAsync`（第 424 行）直接 `await LoadAsync(...)`；`LoadAsync`（第 369-399 行）内部只有 `LoadSettingsOrDefaultAsync`（第 401-422 行）捕获 `IOException` / `UnauthorizedAccessException` / `CorruptSettingsFileException` 三类。`ApplyAppSettingsToFields`、`Appearance.ApplyToServices`、`ChapterToolSettings.Normalize` 等任何其他异常都会传播进 `InitializationTask`，而生产代码没有任何位置 `await` 或检查该任务。

触发条件：加载链路抛出上述三类之外的异常（例如主题/字体应用失败、设置模型不变量被破坏）。

影响：设置界面停留在默认值且无状态提示、无日志；异常成为 unobserved task exception。

修复方向：在 `InitializeAsync` 内加 try/catch 并走统一异常上报（如 `ReportUnexpectedUiException` 端口），或由宿主 `await InitializationTask` 并处理失败。

#### UI-03 模板加载失败的恢复路径经由 `ChapterNameModeIndex` setter，无条件清掉 `AutoGenerateNames`

证据一——setter 副作用（`src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs` 第 621-637 行）：

```csharp
set
{
    ...
    AutoGenerateNames = false;          // 第 628 行，无条件执行
    UseTemplateNames = value is 1 or 2;
    ...
}
```

证据二——失败恢复（`src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.ImportExport.cs` 第 30、48-53 行）：

```csharp
var previousMode = ChapterNameModeIndex;
...
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
{
    ChapterNameModeIndex = previousMode;   // 第 51 行
    ...
}
```

问题：用户开启"自动生成章节名"（`AutoGenerateNames = true`）时 `ChapterNameModeIndex` 的 getter 返回 0；模板读取失败后恢复 `previousMode == 0` 会经过 setter 把 `AutoGenerateNames` 强制置 false——一次失败的模板加载让用户已开启的自动命名被关闭。

触发条件：`ChapterNameTemplateReader.ReadAsync` 抛 IO/权限/参数异常（文件被占用、被删除、无权限）。

修复方向：恢复时同时记录并还原 `AutoGenerateNames`，或让 setter 只在模式真正变化时清除相关状态。

#### UI-04 工具 ViewModel 的命令未注入 `ErrorHandler`，`BrowseScriptAsync` 的文件读取异常被完全吞掉

证据一——无保护的文件读取（`src/ChapterTool.Avalonia.UI/ViewModels/Tools/ExpressionToolViewModel.cs` 第 122 行）：

```csharp
var text = await File.ReadAllTextAsync(path, cancellationToken);   // 无 try/catch
```

证据二——`UiCommand.Execute` 默认吞异常（`src/ChapterTool.Avalonia.UI/ViewModels/UiCommand.cs` 第 93-103 行）：

```csharp
public async void Execute(object? parameter)
{
    try { await ExecuteAsync(parameter); }
    catch { /* The exception is exposed through ExecutionError ... */ }
}
```

证据三——工厂只给 TextTool 传了 `ErrorHandler`（`src/ChapterTool.Avalonia/Services/StandardToolCatalogFactory.cs` 第 24-28、79-82、100-102 行）：`ExpressionToolViewModel`、`LanguageToolViewModel`、`ForwardShiftToolViewModel` 构造后其 `UiCommand.ErrorHandler` 均为 null。

触发条件：从表达式工具选择 Lua 脚本后，文件在读取前被删除/移动/失去权限。

影响：`Execute` 捕获异常仅存入 `ExecutionError`，无状态栏提示、无日志——用户点击后无任何反馈。

修复方向：`StandardToolCatalogFactory` 为工具 ViewModel 的命令统一注入 `context.Session.ReportUnexpectedUiException`；或在 `BrowseScriptAsync` 内捕获 IO 类异常写入 `StatusText`。

#### UI-05 `MainView` 拆除逻辑单向且破坏性：订阅仅在构造函数建立，Detach 时置空 `Content`

证据（`src/ChapterTool.Avalonia.UI/Views/MainView.axaml.cs`）：

```csharp
// 构造函数（第 71、73 行）——仅此一处建立订阅
embeddedToolPresenter.ContentChanged += OnSecondarySurfaceChanged;
...
SubscribeViewModelCommandState();

// 第 430-436 行
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
{
    UnsubscribeViewModelCommandState();
    embeddedToolPresenter.ContentChanged -= OnSecondarySurfaceChanged;
    Content = null;
    base.OnDetachedFromVisualTree(args);
}
```

问题：`OnAttachedToVisualTree`（第 78-82 行）只补建 `filePickerService`，不会重建上述订阅，更不会恢复 `Content`。视图一旦从视觉树分离（宿主重排、窗口重建、被移动到另一容器）再附加：界面空白（`Content = null` 不可逆）、适配器命令的 `CanExecute` 不再随 ViewModel 刷新、嵌入式工具面板不再更新。

触发条件：任何 detach → reattach 生命周期；桌面单窗口常规退出不受影响，但该类位于共享 UI 程序集，供多宿主复用。

修复方向：订阅/退订成对放入 `OnAttachedToVisualTree` / `OnDetachedFromVisualTree`；不要在 Detach 中置空 `Content`（如需断开嵌入面板，仅清 `SecondarySurface.Content`）。

### 低

#### UI-06 `MainWindowViewModel` 订阅单例 localizer 的 `CultureChanged` 且永不退订

证据（`src/ChapterTool.Avalonia.UI/ViewModels/MainWindowViewModel.cs` 第 143 行）：

```csharp
Localizer.CultureChanged += (_, _) => RefreshLocalizedState();
```

`MainWindowViewModel` 未实现 `IDisposable`，lambda 无法退订；单例 `IAppLocalizer`（`AvaloniaPlatformModule.cs` 第 15 行注册为 `SingleInstance`）将永久持有 ViewModel。生产中两者同为应用级生命周期，影响有限；但在测试或未来多实例宿主中会累积泄漏。对比：`SettingsToolViewModel` / `LanguageToolViewModel` 都以命名 handler + `Dispose` 正确退订。

#### UI-07 Ctrl+O 两条路由语义不一致，`ShortcutRouter` 的映射不可达

证据：`MainView.axaml.cs` 第 236-237 行拦截 `"Ctrl+O"` 执行 `BrowseAndLoadAsync()`（弹文件选择器）；`src/ChapterTool.Avalonia.UI/ViewModels/ShortcutRouter.cs` 第 9 行 `"Ctrl+O" => viewModel.LoadCommand.ExecuteAsync(...)`（直接按 `SourcePath` 加载）。键盘路径永远走前者，Router 中的映射成为不可达的第二语义；若其他入口复用 Router 会得到不同行为。建议删除 Router 中的 `Ctrl+O` 分支或统一两处语义。

#### UI-08 以异常类型名字符串识别 `CorruptSettingsFileException`

证据（`src/ChapterTool.Avalonia.UI/ViewModels/SettingsToolViewModel.cs` 第 417 行）：

```csharp
catch (Exception exception) when (exception.GetType().Name == "CorruptSettingsFileException")
```

类型重命名或出现同名类型时该过滤器会静默失效/误捕。建议把该异常类型下沉到共享契约程序集后强类型捕获。

#### UI-09 `FormatBox` 硬编码 9 个 `ComboBoxItem`，与 `ChapterExportFormats.All` 顺序隐式索引耦合

证据（`src/ChapterTool.Avalonia.UI/Views/MainView.axaml` 第 484-498 行）：

```xml
<ComboBox x:Name="FormatBox" SelectedIndex="{Binding SaveFormatIndex}" ...>
  <ComboBoxItem Content="TXT" />
  ...
  <ComboBoxItem Content="Celltimes" />
</ComboBox>
```

`SaveFormatIndex` 与 `MainView.axaml.cs` 第 253 行的 Alt+数字映射（`ChapterExportFormats.All.Count`）都按索引对齐该枚举顺序；新增/调序导出格式需同步修改多处，容易产生错位。建议改为 `ItemsSource` 绑定 `ChapterExportFormats.All` 的显示名集合（`TextToolFormatSelector.Labels` 已有同类实现）。

## 已排查、无问题

1. **导入进度回调线程安全**：全仓无 `Task.Run` / `ConfigureAwait(false)`（rg 验证），`LoadSourceAsync` 的进度回调（`MainWindowViewModel.ImportExport.cs` 第 76-80 行）沿 UI 线程同步上下文续行，`Progress` 属性更新不跨线程。
2. **辅助工具 ViewModel 的释放链路**：`EmbeddedAuxiliaryToolHost.DisposeContent`（第 128-137 行）与 `AvaloniaWindowService` 的 `window.Closed` / `Dispose`（第 117-122、142-156 行）均释放 `IDisposable` DataContext；`TextToolView` 在 DataContext 变更与 Detach 时调用 `DetachLiveRefresh`，`EntryAdded` 订阅可正确退订。
3. **`AppCompositionRoot` 的 fire-and-forget 启动**（第 118 行 `_ = mainView.InitializeAsync(startupPath)`）：`InitializeAsync` 内部由 `UiOperationBoundary.RunAsync` 捕获全部非取消异常并经 `ReportUnexpectedUiException` 上报，无异常丢失。
4. **`ExpressionEditor` 的两个 `DispatcherTimer`**：均为"启动后首个 Tick 即 `Stop()`"的单次模式（第 439-456、647-653 行），不会长期运行保活已分离控件；无持久泄漏。
5. **Serilog 释放**：`LoggingModule` 使用 `AddSerilog(dispose: true)` 且 Logger 注册为 `ExternallyOwned`，释放责任归 `ILoggerFactory`，无双重释放或漏释放。

另：全部 `.axaml` 无 `Canvas` 绝对定位；主窗口四工作区（顶部加载/保存与帧率、中部章节表格、底部选项区、状态/进度条）完整；底部选项区使用星号列并随窗宽切换 2/3 列布局（`ApplyAdvancedOptionsLayout`）；DataGrid 列均设 `MinWidth`；本地化资源为合法 UTF-8。

## 修复优先级建议

1. **UI-01**（高）：移除 `AppLocalizationManager` 构造函数的线程 Culture 副作用——影响面最广、触发最频繁，且修复面很小（一处构造函数 + 四处调用点语义确认）。
2. **UI-03 / UI-04**（中）：两者都是"用户操作失败后状态错误或无反馈"，直接影响可感知的正确性；修复局部、风险低。
3. **UI-02**（中）：为设置自动加载补异常上报，消除 unobserved task exception。
4. **UI-05**（中）：把 `MainView` 订阅生命周期改为 attach/detach 成对，去掉破坏性 `Content = null`；建议配 Headless 测试覆盖 detach→reattach。
5. **UI-06 ~ UI-09**（低）：随相关模块的下次改动顺带处理即可。

## 主要验证命令

- `rg -n "Task\.Run|ConfigureAwait\(false\)" src`（无匹配）
- `rg -n "Canvas" src --glob '*.axaml'`（无匹配）
- `rg -n "new AppLocalizationManager" src`（4 处调用点）
