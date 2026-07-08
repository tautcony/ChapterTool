# 06 - 輔助 UI、平台服務、配置、資源與更新

## 模塊目的與 Avalonia 重寫邊界

本模塊覆蓋 ChapterTool 主窗口以外的輔助窗口、自定義控件、日誌/通知/更新、配置存儲、語言資源、Windows 平台整合與構建資源。它不是章節解析、時間轉換或導入導出算法的核心模塊；重寫時應把這些 WinForms 直接依賴拆成 Avalonia View、ViewModel 與可替換服務，使主流程只依賴抽象接口。

Avalonia 重寫邊界：

- 必須重寫為 Avalonia UI：`FormPreview`、`FormLog`、`FormColor`、`FormAbout`、`FormUpdater`、`cTextBox` 的快捷鍵行為、`HiLightTextBox` 的文本高亮能力。
- 必須抽象為平台服務：消息框/輸入框/剪貼板、進程啟動、管理員提權、文件關聯、硬鏈接、DPI、系統菜單、托盤/通知、更新下載、配置文件與語言切換。
- 可保留或遷移為資源/配置：`Properties/Resources*.resx` 的文本與圖片、`Images/*` 圖標、`color-config.json`、`chaptertool.json` 的語義。
- 不應直接移植 WinForms/HWND 細節：`System.Windows.Forms.MessageBox`、`NotifyIcon`、`SystemMenu` 的 `WM_SYSCOMMAND` 掛鉤、`ProgressBar.SetState`、`Control.Handle`、Designer `.resx` 佈局。
- Windows 專有能力需在 Avalonia 中走 Windows-only implementation；非 Windows 平台需降級、隱藏入口或提示不支持。

## 審閱過的文件清單

- 輔助窗口：`Time_Shift/Forms/FormPreview.cs`、`FormPreview.Designer.cs`、`FormPreview.resx`、`FormLog.cs`、`FormLog.Designer.cs`、`FormLog.resx`、`FormColor.cs`、`FormColor.Designer.cs`、`FormColor.resx`、`FormAbout.cs`、`FormAbout.Designer.cs`、`FormAbout.resx`、`FormUpdater.cs`、`FormUpdater.Designer.cs`、`FormUpdater.resx`
- 自定義控件：`Time_Shift/Controls/cTextBox.cs`、`cTextBox.resx`、`HiLightTextBox.cs`
- 平台/支援服務：`Time_Shift/Util/Logger.cs`、`Notification.cs`、`NativeMethods.cs`、`SystemMenu.cs`、`TaskAsync.cs`、`Updater.cs`、`LanguageHelper.cs`、`LanguageSelectionContainer.cs`、`ToolKits.cs`
- 資源與配置：`Time_Shift/Properties/Resources.resx`、`Resources.en-US.resx`、`Resources.Designer.cs`、`Settings.settings`、`Settings.Designer.cs`、`Time_Shift/Images/*`、`Time_Shift/Resources/arrow_drop_down.bmp`、`Time_Shift/app.manifest`、`App.config`、`FodyWeavers.xml`、`FodyWeavers.xsd`
- 主窗口調用點：`Time_Shift/Forms/Form1.cs`、`Form1.Designer.cs`、`Form1.resx`、`Form1.en-US.resx` 中的輔助窗口、配置、語言、通知、平台服務入口。

## 輔助窗口行為

### 預覽窗口

`FormPreview` 是無邊框、置頂、只讀多行文本窗口。主窗口點擊 Preview 時創建單例窗口，調用 `UpdateText(GetOutputText())` 後顯示；再次關閉時不銷毀，而是取消關閉並 `Hide()`。窗口在 `Load` 和主窗口 `Move` 事件中定位到主窗口左側：`Location = main.X - Width, main.Y`。預覽文本超過 20 行或行寬超過 40 時啟用垂直或雙向滾動；雙擊文本框關閉/隱藏窗口。

Avalonia 建議：

- `PreviewWindow` + `PreviewViewModel`，由主窗口服務持有單例或用窗口管理器復用。
- 位置跟隨主窗口可用 `IWindowPlacementService`，在多屏、縮放與非 Windows 平台上測試；不要依賴 WinForms `Point`。
- `Topmost = true`、無邊框、只讀等可直接映射到 Avalonia Window/TextBox 屬性。

### 日誌窗口

`FormLog` 顯示 `Logger.LogText`。窗口激活時刷新文本；文本變更後滾動到末尾，並把分組標題更新成 `Log (lineCount)`。按鈕包括刷新、複製選區、關閉；關閉同樣取消並隱藏，避免釋放單例。標題包含執行程序集版本。

Avalonia 建議：

- `LogWindow` + `LogViewModel` 訂閱 `ILogService.LogLineAdded`，用 `ObservableCollection<LogEntry>` 或只讀文本流。
- 複製選區走 `IClipboardService`；自動滾動交給 View 行為處理。
- 保留“關閉即隱藏”的窗口生命週期，或改成每次可重新創建，但需保持主窗口入口語義一致。

### 配色窗口

`FormColor` 是固定對話框，顯示六個色塊按鈕：窗體背景、文字背景、按鍵默認/懸停/按下、邊框、文字前景。打開時從主窗口 `CurrentColor` 讀取當前值；點擊色塊打開 `ColorDialog`，選中後立即寫回主窗口屬性。關閉時調用 `CurrentColor.SaveColor()`，保存到程序目錄下 `color-config.json`，然後隱藏。

六個顏色槽位的現有順序必須保持兼容：

1. `BackChange`：主窗口背景與狀態欄背景。
2. `TextBack`：表格背景、數值框、下拉框、語言/保存類型控件背景。
3. `MouseOverColor`：主操作按鈕懸停背景。
4. `MouseDownColor`：主操作按鈕按下背景。
5. `BorderBackColor`：主操作按鈕邊框與表格網格線。
6. `TextFrontColor`：主窗口、輸入控件、表格前景色。

Avalonia 建議：

- 建立 `ThemeColorSettings` 模型，保留上述槽位與 JSON 兼容格式，內部可用 `#RRGGBB`。
- `ColorSettingsViewModel` 不直接改 View 控件，而是更新主窗口共享的 theme state/resource dictionary。
- WinForms `ColorDialog` 替換為 Avalonia 色彩選擇器或自定義色盤。

### About 窗口

`FormAbout` 是無邊框、置頂、無控制框窗口。構造時讀取程序集 Product、Version 和可執行文件最後寫入時間；隨機選擇 `_poi` 介於 1 到 4，只有點中對應按鈕才會淡出並真正 `Close()`，其餘關閉路徑會隱藏。窗口支持拖動，最小化會隱藏並顯示托盤氣泡；點擊托盤圖標恢復。產品鏈接按鼠標左/右/中鍵分別打開 GitHub、Bitbucket、ChangeLog URL。

Avalonia 建議：

- `AboutWindow` + `AboutViewModel` 暴露版本、產品名、構建/文件時間與鏈接命令。
- 無邊框拖動使用 Avalonia Window drag API；淡出用動畫，不在 UI 線程中 `Thread.Sleep`。
- 托盤氣泡是平台能力，放到 `INotificationTrayService`，非 Windows 可省略或用桌面通知。
- 現有 `_poi` 隨機按鈕邏輯屬於彩蛋；是否保留需產品確認。

### Updater 窗口

`Updater.CheckUpdate()` 使用 `wininet.dll` 檢查網絡，再請求 `http://tautcony.github.io/tcupdate.html`，解析 meta 標籤中的遠端版本與 BaseUrl。若遠端版本較新，詢問用戶後顯示 `FormUpdater`。`FormUpdater` 使用 `WebClient.DownloadFileAsync` 下載 `http://{baseUrl}/bin/ChapterTool.v{version}.exe` 到當前 exe 的 `.new`，進度更新進度條。取消時刪除 `.new` 並關閉；完成時刪除舊 `.bak`、把當前 exe 改名為 `.bak`、把 `.new` 改名為 exe，然後 `Application.Restart()`。

主窗口目前把 weekly update log/check 調用註釋掉，系統菜單中的 update command 也被註釋；但完整更新實作仍需在重寫時決定保留、替換或移除。

Avalonia 建議：

- `IUpdateService` 負責檢查版本、下載、校驗與準備更新；`UpdaterViewModel` 只顯示版本、進度、取消命令。
- 改用 `HttpClient`、HTTPS、取消令牌、完整錯誤狀態與簽名/校驗；不要直接覆蓋正在運行的可執行文件。
- 自更新在 .NET/Avalonia 發佈模式下需重新設計，可能改為安裝器、外部 helper、MSIX/winget/GitHub Releases 或提示手動下載。

## 自定義控件行為

`cTextBox` 繼承 WinForms `TextBox`，設置雙緩衝，覆寫 `OnKeyDown`：

- `Ctrl+A`：全選。
- `Ctrl+C`：把選中內容以 UnicodeText 放入剪貼板。
- `Alt+A`：從總行數的一半開始選到文本末尾。
- 每次按鍵最後都設置 `e.Handled = true`，包括未匹配快捷鍵；這可能吞掉普通按鍵，因現有使用場景多為只讀文本框，重寫時要按實際文本輸入場景驗證。

`HiLightTextBox` 繼承 `RichTextBox`，維護 `HashSet<Pattern>`，每次文字變更都啟動 `BackgroundWorker`，把 RTF 載入臨時 `RichTextBox`，先重置全局字色與字體，再按正則匹配區間設置顏色，完成後回寫 RTF。它的異步回寫可能和快速輸入競態，且依賴 WinForms RTF 控件。

Avalonia 建議：

- `cTextBox` 行為可實作為 TextBox attached behavior/key binding，而不是自定義控件。
- 高亮改為基於 `TextMate`、AvaloniaEdit、`TextBlock.Inlines` 或自定義 document model；不要用 RTF 作中間格式。
- 若高亮控件在現有 UI 未被實際使用，可先保留接口與測試，延後實作。

## 配置、語言與平台服務

### 配置存儲

`RegistryStorage` 名稱仍叫 registry，但目前主要寫 `chaptertool.json` 到執行程序集目錄。它是 JSON-backed settings store，鍵名格式為 `{subKey}.{name}`，保存值是 string。現有 JSON key 包括：

- `Software\ChapterTool.SavingPath`：保存輸出目錄。
- `Software\ChapterTool.Language`：保存 UI 語言切換值，空字符串代表默認中文，`en-US` 代表英文。
- `Software\ChapterTool.DoVersionCheck`：記錄是否跳過 .NET runtime 版本提示。
- `Software\ChapterTool.Location` / `Software\ChapterTool.location`：主窗口位置；當前代碼保存大寫 `Location`、讀取小寫 `location`，JSON dictionary 大小寫敏感，遷移需兼容兩者並寫入新規範鍵。
- `Software\ChapterTool.`：主窗口啟動彩蛋/佔位寫入。
- `Software\ChapterTool\Statistics.Count`：啟動計數。
- `Software\ChapterTool.LastCheck`：更新檢查時間。
- `Software\ChapterTool.mkvToolnixPath`：Matroska importer 的 MKVToolNix 路徑。
- `Software\ChapterTool.eac3toPath`：BDMV importer 的 eac3to 路徑。

真 Windows registry 使用點不要混同於 `chaptertool.json`：

- `Program.cs` 讀 HKLM .NET Framework `Release`，並用 JSON `DoVersionCheck` 記錄是否跳過提示。
- `Form1.cs` 讀 HKLM CPU name 僅用於 log。
- `MatroskaData.cs` 讀 HKLM/HKCU 尋找 MKVToolNix/mkvmergeGUI。
- `RegistryStorage.SetOpenMethod` 寫 Windows `Registry.ClassesRoot`，用於 `.mpls` 文件關聯。
- NSIS installer/uninstaller 寫刪 HKLM `Software\ChapterTool` 與 uninstall keys。

Avalonia 建議：

- 拆分 `ISettingsStore` 和 `IFileAssociationService`。前者跨平台使用 JSON，位置改為用戶配置目錄；後者 Windows-only。
- 為舊版 `chaptertool.json` 和 `color-config.json` 提供遷移讀取；新格式可使用強類型 settings。
- `Settings.settings` 目前沒有配置項，不應作為新 Avalonia 設置來源。

### 顏色配置

`ToolKits.SaveColor` 將六個顏色保存為簡單 JSON 字符串陣列，例如 `["#RRGGBB", ...]`；`LoadColor` 從當前工作目錄讀 `color-config.json`，用正則提取至少六個 hex 值後直接套用主窗口屬性。注意保存路徑使用 exe 目錄，讀取時使用相對路徑，當工作目錄不同時可能讀不到。

Avalonia 建議以 `IThemeSettingsStore` 統一讀寫，讀取舊格式時容忍 exe 目錄與當前目錄兩種位置。

### `ToolKits` 共享 helper

`ToolKits.cs` 除配置/顏色外，也是跨模塊 helper：

- 時間格式與解析：`Time2String`、`ToTimeSpan`、`ToCueTimeStamp`、`RTimeFormat`。
- 幀率映射：`ConvertFr2Index`。
- 文本編碼：`GetUTFString`。
- 文件保存：`SaveAs(..., bom = true/false)`，目前用 UTF-8，可選 BOM。
- 平台/測試輔助：`IsRunningOnMono`、`GetDataReceivedEventArgs`、`ReadStreamPerCharacter`。
- `RegistryAddTime` 增加 `Software\ChapterTool\Statistics.Count` 啟動計數。

Avalonia 重寫時應將時間/編碼/保存 helper 保留在 Core/shared utility，不綁到 UI service；配置、顏色、registry、進程與 shell 相關能力則拆到 Infrastructure/platform services。

### 語言切換

`LanguageHelper.SetLang` 通過設置 `CurrentUICulture` 並對 WinForms 控件遞歸 `ApplyResources` 完成界面文字切換，支持 `MenuStrip`、`ContextMenuStrip`、`DataGridViewColumn`。主窗口構造初期先讀取 `Language`，再套用 Form1 資源。系統菜單在存在 `en-US/ChapterTool.resources.dll` 時加入 `Resources.Menu_Switch_Language`，切換後保存 `Language` 並重啟進程。

`LanguageSelectionContainer` 提供 XML language dropdown 的語言表、ISO 639-2/B/T 與 ISO2 查詢，以及 `LoadLang(ComboBox)` 的 WinForms 下拉填充。`DualDictionary` 是它的雙向 lookup helper，由模塊 02 主責資料結構；本模塊只關心 UI 下拉、本地化與 resource service 交界。

Avalonia 建議：

- 使用 `IStringLocalizer`/resource service 或 Avalonia resource dictionaries；不要移植 WinForms `ComponentResourceManager.ApplyResources`。
- 保留 `zh-CN`/默認與 `en-US` 的語義，確認 satellite assembly 發佈方式。
- 語言切換入口不應只放 Windows 系統菜單；需要普通菜單/設置入口。

### 系統菜單

`SystemMenu` 通過 `GetSystemMenu`、`AppendMenu` 和 `WM_SYSCOMMAND` 為 WinForms 主窗口添加命令。主窗口目前只在非 Mono 且存在英文資源目錄時加入語言切換命令；更新命令被註釋。

Avalonia 建議：

- 主功能入口用 Avalonia 菜單或設置菜單承載。
- 如需 Windows 標題欄系統菜單，封裝 `IWindowSystemMenuService` 並僅 Windows 啟用。

### 通知、對話框與剪貼板

`Notification.ShowError`/`ShowInfo` 使用 `MessageBox.Show`。Release 模式下錯誤框使用 Yes/No，若選 No，再詢問是否複製 stack trace；Debug 模式直接顯示 stack trace 並只有 OK。`InputBox` 動態創建 WinForms 對話框，返回輸入文本或空字符串。日誌、區域命令和控件快捷鍵直接使用 `Clipboard`。

Avalonia 建議：

- `IDialogService`：錯誤、信息、確認、文本輸入。
- `IClipboardService`：讀寫文本。
- `INotificationService`：普通桌面通知或托盤提示。
- 對話框返回值不要沿用 WinForms `DialogResult`；用明確 enum 或 record。

### 外部進程與硬鏈接

`TaskAsync.RunProcessAsync` 啟動無窗口進程，重定向 stdout/stderr，按行追加 stdout 到 `StringBuilder`，進程退出時返回；stderr 只啟動讀取但未收集。`ToolKits.ReadStreamPerCharacter` 用於逐字符讀取 stdout 並合成行事件。`NativeMethods.CreateHardLinkCMD` 執行 `fsutil hardlink create`，主窗口讀 MP4 時使用它創建臨時硬鏈接，避免部分路徑或庫問題。

Avalonia 建議：

- `IProcessRunner` 支持 stdout/stderr、exit code、取消、working directory 與超時。
- `IFileLinkService` 優先使用 .NET API/平台 API；Windows `fsutil` 作 fallback，並在權限不足時明確錯誤。

### 管理員、DPI、文件關聯與 Shell

`NativeMethods` 包含多個 Windows P/Invoke：

- `IsUserAnAdmin` 檢查管理員；`ToolKits.RunAsAdministrator` 用 `ProcessStartInfo.Verb = "runas"` 重新啟動自己並退出。
- `SetShieldIcon` 給 WinForms Button 加 UAC 盾牌圖標。
- `RefreshNotify` 通知 Shell 刷新，用於文件關聯變更後。
- `GetDpiForMonitor` 讀屏幕 DPI；失敗時記錄並回退 96。
- `FindWindow`、`SetForegroundWindow` 可用於激活已有窗口。
- `CreateHardLink` P/Invoke 和 `CreateHardLinkCMD` 命令行硬鏈接。
- `ProgressBar.SetState` 發送 Windows 進度條狀態消息。

文件關聯入口在 `btnPreview_MouseUp`：右鍵 Preview 按鈕，先 `RunAsAdministrator()`，再確認是否把 `.mpls` 打開方式設置為 ChapterTool。

Avalonia 建議：

- `IPrivilegeService`：檢查/請求提權。
- `IFileAssociationService`：Windows 寫 registry，macOS/Linux 可不支持或走對應桌面標準。
- `IShellService`：打開 URL/文件、激活窗口、刷新 shell。
- `IDpiService`：通常交給 Avalonia 平台層；只有特殊計算才暴露服務。
- UAC 盾牌和 Windows 進度條狀態不應成為跨平台 UI 必需項。

## 資源、圖標、manifest、Fody 與 App.config 影響

`Properties/Resources.resx` 與 `Resources.en-US.resx` 保存主流程文本、消息、日誌模板、文件過濾器、菜單文本，以及 `arrow_drop_down`、`arrow_drop_up`、`unfold_more` 圖片。`Resources.Designer.cs` 是 WinForms/.NET Framework 強類型資源生成物；Avalonia 可重用文本內容和圖片資產，但不應依賴生成類。

`Form1.resx`/`Form1.en-US.resx` 包含大量 WinForms 控件佈局、本地化文本、列標題、tooltip、圖片等。重寫時需要提取用戶可見文本與少量圖片，丟棄 WinForms 佈局數據。輔助窗口 `.resx` 大多是 Designer 資源，`FormAbout` 和 `FormLog` 有 icon/notify icon 之類資源。

`Images/icon.ico` 是應用圖標，`Images/about.ico` 供 About/托盤類資源，`Images/arrow_drop_down.png`、`arrow_drop_up.png`、`unfold_more.png` 與 `Resources/arrow_drop_down.bmp` 是 UI 展開/收起圖像。Avalonia 發佈需確認圖標 build action、跨平台窗口圖標格式和單文件發佈時資源可訪問性。

`app.manifest` 指定 `requestedExecutionLevel=asInvoker` 和 DPI aware。Avalonia 重寫若保留 Windows manifest，仍可使用 asInvoker；提權操作應按需重啟，而非整個程序默認要求管理員。DPI 由 Avalonia 處理，但 Windows manifest 仍影響桌面縮放行為。

`App.config` 指向 .NET Framework runtime，開啟 WinForms HighDPI auto resizing，包含 assembly binding redirects 和 ClientSettingsProvider。Avalonia/.NET 現代項目通常不使用此配置；binding redirects、WinForms HighDPI 和 System.Web client settings 不應遷移。若保留 Jil/Sigil 等依賴，應改由 SDK-style project/NuGet 解決。

`FodyWeavers.xml` 啟用 `Costura` 內嵌依賴；`FodyWeavers.xsd` 是配置 schema。Avalonia 重寫如採用 .NET 8/9 SDK-style，可考慮不用 Costura，改用 self-contained、single-file 或標準發佈。需要單獨確認原有 mp4v2 native DLL 是否仍要按 x86/x64 複製，這不屬於 Costura 管理。

## Avalonia 建議服務接口與 ViewModel/窗口映射

建議接口：

- `ILogService`：`Log(string message)`、`IObservable/ObservableCollection<LogEntry>`、`LogText`。
- `IDialogService`：錯誤、信息、確認、文本輸入。
- `IClipboardService`：`SetTextAsync`、`GetTextAsync`。
- `ISettingsStore`：強類型 app settings，兼容讀取舊 `chaptertool.json`。
- `IThemeSettingsStore`：讀寫六槽位顏色配置，兼容 `color-config.json`。
- `ILocalizationService`：當前 culture、字符串查詢、切換語言與重啟/刷新策略。
- `IWindowManager`：打開/聚焦/隱藏輔助窗口，管理單例窗口生命週期。
- `IWindowPlacementService`：保存/恢復主窗口位置，定位 Preview 跟隨主窗口。
- `IUpdateService`：檢查版本、下載、取消、準備安裝。
- `IProcessRunner`：外部命令執行與 stdout/stderr 收集。
- `IExternalToolLocator`：mkvextract/eac3to 路徑查找、舊 JSON key 遷移、Windows registry discovery optional。
- `IShellService`：打開 URL/文件、打開進程、激活窗口。
- `IPrivilegeService`：是否管理員、請求提權重啟。
- `IFileAssociationService`：文件關聯查詢/註冊。
- `INativeLibraryService` 或 MP4 adapter：處理 `libMP4V2.dll` 載入、x86/x64 發佈與錯誤提示。
- `INotificationTrayService`：托盤圖標和氣泡通知，Windows-only optional。
- `IPlatformInfoService`：OS、DPI、Mono/兼容層判斷、程序路徑。

referenced=06 平台交界：

| 文件 | 06 相關交界 |
| --- | --- |
| `Time_Shift/Program.cs` | runtime check、startup args、`DoVersionCheck` setting |
| `Time_Shift/Util/ChapterData/MatroskaData.cs` | MKVToolNix path JSON + Windows registry discovery |
| `Time_Shift/Util/ChapterData/BDMVData.cs` | `eac3toPath` JSON + input dialog + external process |
| `Time_Shift/Knuckleball/NativeMethods.cs` | `libMP4V2.dll` P/Invoke/native library deployment |
| `dist/windows/*.nsi` | HKLM install location、uninstall keys、installer language、UAC macro |
| root metadata | README/ChangeLog/LICENSE 對平台依賴、授權與發佈說明的約束 |

窗口與 ViewModel 映射：

- `FormPreview` -> `PreviewWindow` / `PreviewViewModel`：`Text`、`IsTopmost`、`CloseAsHideCommand`、跟隨主窗口位置。
- `FormLog` -> `LogWindow` / `LogViewModel`：`Entries` 或 `LogText`、`LineCount`、`RefreshCommand`、`CopySelectionCommand`、`HideCommand`。
- `FormColor` -> `ColorSettingsWindow` / `ColorSettingsViewModel`：六個顏色屬性、`PickColorCommand`、`SaveOnClose`。
- `FormAbout` -> `AboutWindow` / `AboutViewModel`：`ProductName`、`Version`、`BuildTime`、`OpenProjectLinkCommand`、可選彩蛋/淡出命令。
- `FormUpdater` -> `UpdaterWindow` / `UpdaterViewModel`：`RemoteVersion`、`ProgressPercent`、`Status`、`CancelCommand`。
- `cTextBox` -> TextBox behavior：`Ctrl+A`、`Ctrl+C`、可選 `Alt+A`。
- `HiLightTextBox` -> `HighlightedTextView` 或 AvaloniaEdit adapter：pattern collection、原始顏色、高亮 document。

## 未確定或需測試驗證的點

- `HiLightTextBox` 是否仍被實際 UI 使用；若沒有使用，可延後或刪除遷移需求。
- `cTextBox.OnKeyDown` 對所有按鍵都 `Handled = true` 是否是 bug；Avalonia 重寫應驗證普通輸入控件不受影響。
- `LanguageHelper.SetAllLang` 裏的 `Assembly.Load("CameraTest")` 看起來是殘留錯誤，需確認是否完全未使用。
- 更新機制目前入口被註釋，且使用 HTTP、直接替換 exe；需確認是否保留自更新。
- `Updater` 在 Debug 下請求 `http://127.0.0.1:4000/tcupdate.html`，Release 下請求 `http://tautcony.github.io/tcupdate.html`；重寫測試需保留可替換 update endpoint。
- `RegistryStorage` 寫配置到程序目錄，在受限安裝目錄可能失敗；新版本配置位置和舊配置遷移策略需測試。
- `LoadColor` 讀相對路徑而 `SaveColor` 寫 exe 目錄，工作目錄不同時可能不一致。
- `.mpls` 文件關聯寫 `HKCR`，需要管理員和 Windows Shell 刷新；非 Windows 行為需明確設計。
- `FormPreview` 固定移到主窗口左側，在多屏、負坐標、高 DPI、窗口靠左時可能不可見。
- `FormAbout` 關閉邏輯同時存在隱藏與真正關閉彩蛋，重寫需決定是否保留這種不一致生命週期。
- `TaskAsync.RunProcessAsync` 不返回 exit code，也不收集 stderr；遷移後外部工具失敗狀態需要補測。
- `App.config` 中 target runtime、binding redirects 與 HighDPI 設置在 Avalonia/.NET SDK-style 下是否全部廢棄需由新項目文件確認。
- `Fody/Costura` 是否仍需要取決於新發佈方案與 native DLL 分發方案。

## 本模塊覆蓋的源文件列表

- `Time_Shift/Forms/FormPreview.cs`
- `Time_Shift/Forms/FormPreview.Designer.cs`
- `Time_Shift/Forms/FormPreview.resx`
- `Time_Shift/Forms/FormLog.cs`
- `Time_Shift/Forms/FormLog.Designer.cs`
- `Time_Shift/Forms/FormLog.resx`
- `Time_Shift/Forms/FormColor.cs`
- `Time_Shift/Forms/FormColor.Designer.cs`
- `Time_Shift/Forms/FormColor.resx`
- `Time_Shift/Forms/FormAbout.cs`
- `Time_Shift/Forms/FormAbout.Designer.cs`
- `Time_Shift/Forms/FormAbout.resx`
- `Time_Shift/Forms/FormUpdater.cs`
- `Time_Shift/Forms/FormUpdater.Designer.cs`
- `Time_Shift/Forms/FormUpdater.resx`
- `Time_Shift/Controls/cTextBox.cs`
- `Time_Shift/Controls/cTextBox.resx`
- `Time_Shift/Controls/HiLightTextBox.cs`
- `Time_Shift/Util/Logger.cs`
- `Time_Shift/Util/Notification.cs`
- `Time_Shift/Util/NativeMethods.cs`
- `Time_Shift/Util/SystemMenu.cs`
- `Time_Shift/Util/TaskAsync.cs`
- `Time_Shift/Util/Updater.cs`
- `Time_Shift/Util/LanguageHelper.cs`
- `Time_Shift/Util/LanguageSelectionContainer.cs`
- `Time_Shift/Util/DualDictionary.cs`（語言表 lookup 輔助，資料結構主覆蓋見模塊 02）
- `Time_Shift/Util/ToolKits.cs`
- `Time_Shift/Properties/Resources.resx`
- `Time_Shift/Properties/Resources.en-US.resx`
- `Time_Shift/Properties/Resources.Designer.cs`
- `Time_Shift/Properties/Settings.settings`
- `Time_Shift/Properties/Settings.Designer.cs`
- `Time_Shift/Images/icon.ico`
- `Time_Shift/Images/about.ico`
- `Time_Shift/Images/arrow_drop_down.png`
- `Time_Shift/Images/arrow_drop_up.png`
- `Time_Shift/Images/unfold_more.png`
- `Time_Shift/Resources/arrow_drop_down.bmp`
- `Time_Shift/app.manifest`
- `Time_Shift/App.config`
- `Time_Shift/FodyWeavers.xml`
- `Time_Shift/FodyWeavers.xsd`
- `Time_Shift/Forms/Form1.cs` 的本模塊調用點
- `Time_Shift/Forms/Form1.Designer.cs` 的本模塊控件/資源調用點
- `Time_Shift/Forms/Form1.resx`
- `Time_Shift/Forms/Form1.en-US.resx`
